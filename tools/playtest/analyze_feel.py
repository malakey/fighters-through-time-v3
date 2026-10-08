"""Turn a feel-probe CSV (user://playtest/feel_*.csv) into feel metrics.

Usage:  python tools/playtest/analyze_feel.py <feel.csv> [<feel2.csv> ...]

Every number is in 60 Hz game frames unless marked otherwise. The probe's
segments (run, stop, jump, whiff_string, mash_string, hit_string, special1,
special2, ultimate) are measured independently; see PlaytestFeel.cs for the
input timeline that produced them.
"""
import csv
import sys

JUMP, ATTACK, S1, S2, ULT = 1, 4, 8, 16, 128


def rows_of(path):
    with open(path, newline="", encoding="utf-8-sig") as handle:
        rows = list(csv.DictReader(handle))
    for r in rows:
        for key in ("t", "move_x", "held", "pressed", "anim_frame", "hitstop", "hitbox_active",
                    "combo", "hp", "enemy_hp", "enemy_hitstop", "on_floor", "capture"):
            try:
                r[key] = int(r[key])
            except (KeyError, ValueError):
                r[key] = 0
        for key in ("x", "y", "vx", "vy", "anim_fps", "anim_speed_scale", "meter", "enemy_x", "enemy_vx"):
            try:
                r[key] = float(r[key])
            except (KeyError, ValueError):
                r[key] = 0.0
    return rows


def segment(rows, name):
    return [r for r in rows if r["segment"] == name]


def runs_of(values):
    """Collapse consecutive equal values into (value, length) runs."""
    out = []
    for v in values:
        if out and out[-1][0] == v:
            out[-1][1] += 1
        else:
            out.append([v, 1])
    return out


def movement(rows):
    lines = []
    run = segment(rows, "run")
    if run:
        top = max(abs(r["vx"]) for r in run)
        t95 = next((i for i, r in enumerate(run) if abs(r["vx"]) >= 0.95 * top), None)
        t50 = next((i for i, r in enumerate(run) if abs(r["vx"]) >= 0.50 * top), None)
        lines.append(f"- Run: top speed {top:.0f} px/s; 50% at frame {t50}, 95% at frame {t95} after the stick is pushed.")
    stop = segment(rows, "stop")
    if stop:
        start_x = stop[0]["x"]
        halt = next((i for i, r in enumerate(stop) if abs(r["vx"]) < 1.0), None)
        end_x = stop[halt]["x"] if halt is not None else stop[-1]["x"]
        lines.append(f"- Stop: zero speed {halt} frames after release, sliding {abs(end_x - start_x):.0f} px.")
    jump = segment(rows, "jump")
    if jump:
        leave = next((i for i, r in enumerate(jump) if not r["on_floor"]), None)
        apex = None
        if leave is not None:
            apex = next((i for i in range(leave + 1, len(jump)) if jump[i]["vy"] >= 0), None)
        land = None
        if apex is not None:
            land = next((i for i in range(apex, len(jump)) if jump[i]["on_floor"]), None)
        base_y = jump[0]["y"]
        peak = min(r["y"] for r in jump)
        lines.append(f"- Jump (held 14f): leaves floor at frame {leave}, apex at {apex}, lands at {land}; "
                     f"height {base_y - peak:.0f} px; air time {None if land is None or leave is None else land - leave} frames.")
        anims = runs_of([r["anim"] for r in jump])
        lines.append(f"  Jump animation sequence: {', '.join(f'{a}x{n}' for a, n in anims)}")
    return lines


def presses(seg, button):
    return [i for i, r in enumerate(seg) if r["pressed"] & button]


def swing_report(seg, label, button=ATTACK):
    lines = [f"### {label}"]
    if not seg:
        return lines + ["(segment missing)"]
    press = presses(seg, button)
    active_edges = [i for i in range(1, len(seg)) if seg[i]["hitbox_active"] and not seg[i - 1]["hitbox_active"]]
    lines.append(f"- Presses at {press}; hitbox-active rising edges at {active_edges}.")
    if press and active_edges:
        lines.append(f"- First press -> first active frame: {active_edges[0] - press[0]} frames.")
    for a, b in zip(active_edges, active_edges[1:]):
        lines.append(f"  Hit-to-hit gap: {b - a} frames.")
    end = next((i for i in range(len(seg) - 1, -1, -1) if seg[i]["state"] in ("Attacking", "UsingSpecial", "UsingUltimate")), None)
    if press and end is not None:
        lines.append(f"- Locked in an attack state from frame {press[0]} to {end} ({end - press[0] + 1} frames).")
    hitstop = sum(1 for r in seg if r["hitstop"] > 0)
    enemy_hitstop = sum(1 for r in seg if r["enemy_hitstop"] > 0)
    lines.append(f"- Frames with player hitstop > 0: {hitstop}; enemy hitstop > 0: {enemy_hitstop}.")
    # Animation cadence: how many distinct sprite frames are shown per animation
    # and how long each one is held.
    anim_runs = runs_of([(r["anim"], r["anim_frame"]) for r in seg])
    per_anim = {}
    for (anim, frame), length in anim_runs:
        per_anim.setdefault(anim, []).append((frame, length))
    for anim, frames in per_anim.items():
        if anim in ("idle", ""):
            continue
        fps = next((r["anim_fps"] for r in seg if r["anim"] == anim), 0)
        holds = ", ".join(f"f{f}x{n}" for f, n in frames)
        lines.append(f"  anim `{anim}` @ {fps:.0f} fps: {holds}")
    states = runs_of([r["state"] for r in seg])
    lines.append(f"- State sequence: {', '.join(f'{s}x{n}' for s, n in states)}")
    hp = [r["enemy_hp"] for r in seg if r["enemy_hp"] >= 0]
    if hp:
        drops = [(i, seg[i - 1]["enemy_hp"] - seg[i]["enemy_hp"]) for i in range(1, len(seg))
                 if seg[i - 1]["enemy_hp"] >= 0 and seg[i]["enemy_hp"] >= 0 and seg[i]["enemy_hp"] < seg[i - 1]["enemy_hp"]]
        lines.append(f"- Enemy HP {hp[0]} -> {hp[-1]}; damage events (frame, dmg): {drops}")
        estates = runs_of([r["enemy_state"] for r in seg])
        lines.append(f"- Enemy states: {', '.join(f'{s}x{n}' for s, n in estates)}")
    return lines


def summary(rows):
    """One row of headline feel numbers for a probe run (used by --table)."""
    out = {}
    run = segment(rows, "run")
    if run:
        top = max(abs(r["vx"]) for r in run)
        out["run95"] = next((i for i, r in enumerate(run) if abs(r["vx"]) >= 0.95 * top), -1)
    stop = segment(rows, "stop")
    if stop:
        halt = next((i for i, r in enumerate(stop) if abs(r["vx"]) < 1.0), len(stop))
        out["stop"] = halt
        out["slide"] = round(abs(stop[min(halt, len(stop) - 1)]["x"] - stop[0]["x"]))
    jump = segment(rows, "jump")
    if jump:
        leave = next((i for i, r in enumerate(jump) if not r["on_floor"]), None)
        land = next((i for i in range((leave or 0) + 2, len(jump)) if jump[i]["on_floor"]), None)
        out["air"] = (land - leave) if leave is not None and land is not None else -1
        out["jump_h"] = round(jump[0]["y"] - min(r["y"] for r in jump))

    def edges(seg):
        return [i for i in range(1, len(seg)) if seg[i]["hitbox_active"] and not seg[i - 1]["hitbox_active"]]

    def locked(seg):
        idx = [i for i, r in enumerate(seg) if r["state"] in ("Attacking", "UsingSpecial", "UsingUltimate")]
        return (idx[-1] - idx[0] + 1) if idx else 0

    def max_hold(seg):
        holds = [n for (a, _f), n in runs_of([(r["anim"], r["anim_frame"]) for r in seg]) if a.startswith(("basic_", "special", "up_", "down_"))]
        return max(holds) if holds else 0

    whiff = segment(rows, "whiff_string")
    out["rhythm_hits"] = len(edges(whiff))
    out["whiff_lock"] = locked(whiff)
    out["max_pose_hold"] = max_hold(whiff)
    mash = segment(rows, "mash_string")
    out["mash_hits"] = len(edges(mash))
    out["mash_lock"] = locked(mash)
    hit = segment(rows, "hit_string")
    out["hitstop_frames"] = sum(1 for r in hit if r["hitstop"] > 0)
    xs = [r["enemy_x"] for r in hit if r["enemy_hp"] >= 0]
    out["enemy_push"] = round(max(xs) - min(xs)) if xs else -1
    s1 = segment(rows, "special1")
    out["s1_lock"] = locked(s1)
    burst = hit + s1 + segment(rows, "special2")
    hp = [r["enemy_hp"] for r in burst if r["enemy_hp"] >= 0]
    out["burst"] = f"{hp[0]}->{hp[-1]}" if hp else "-"
    return out


def table(paths):
    keys = ["run95", "stop", "slide", "air", "jump_h", "rhythm_hits", "whiff_lock", "max_pose_hold",
            "mash_hits", "mash_lock", "hitstop_frames", "enemy_push", "s1_lock", "burst"]
    print("| probe | " + " | ".join(keys) + " |")
    print("|---|" + "---|" * len(keys))
    for path in paths:
        s = summary(rows_of(path))
        name = path.replace("\\", "/").split("/")[-1].replace("feel_", "").replace(".csv", "")
        print(f"| {name} | " + " | ".join(str(s.get(k, "")) for k in keys) + " |")


def main(paths):
    if paths and paths[0] == "--table":
        table(paths[1:])
        return
    for path in paths:
        rows = rows_of(path)
        print(f"# Feel report: {path}")
        print(f"{len(rows)} frames logged.\n")
        print("## Movement")
        print("\n".join(movement(rows)))
        print("\n## Attacks")
        print("\n".join(swing_report(segment(rows, "whiff_string"), "Whiffed 3-hit string (press every 12f)")))
        print("\n".join(swing_report(segment(rows, "mash_string"), "Mashed string (press every 6f for 1 s)")))
        hit = segment(rows, "hit_string") + segment(rows, "special1") + segment(rows, "special2")
        print("\n".join(swing_report(segment(rows, "hit_string"), "String on a live enemy")))
        print("\n".join(swing_report(segment(rows, "special1"), "Special 1", S1)))
        print("\n".join(swing_report(segment(rows, "special2"), "Special 2", S2)))
        print("\n".join(swing_report(segment(rows, "ultimate"), "Ultimate", ULT)))
        if hit:
            first = next((r for r in hit if r["enemy_hp"] >= 0), None)
            last = hit[-1]
            if first:
                print(f"\nString + S1 + S2 burst: enemy HP {first['enemy_hp']} -> {last['enemy_hp']} over {len(hit)} frames.")
        print()


if __name__ == "__main__":
    main(sys.argv[1:])
