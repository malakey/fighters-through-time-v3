"""Print a compact view of a playtest report's once-a-second trace.

Usage: python tools/playtest/trace.py <report.json | run-name> [--every N] [--from S] [--to S] [--swings]

A bare run name (e.g. L02_joan_play_easy_t1) is resolved inside the default
report directory (%APPDATA%/Godot/app_userdata/Fighters Through Time/playtest).

Since 2026-10-11 (D6d) the header also prints the meter, swing, finisher and
ability-press summaries; each trace line shows the Ultimate meter (m=), the
ability lockout (lk=, when running) and, once group A lands, the Special gate
(sg=). --swings lists the hero's swings inside --from/--to instead of the trace.
C3 adds the mob melee swing summary (mob_swings) to the header.

GU-BOT (2026-10-12) adds the encounter locks (one line per lock: state, engage
and clear seconds, planned / killed, reverts, leaves with members alive,
relocations) and the crowd summary (mob_engagement, without its histograms) to
the header; each trace line shows the engaged lock (enc=<lock tail>:<enemies
left>), the mobs attacking within 250 px of the hero (atk=) and its melee slot
holders (slots=). A run that ended stuck_lock:<id> also prints the bot's lock
nav notes. FIX-BOT (2026-10-13): a lock a scene reload rebuilt (an Act III
Anchor Snap) is its own line, its id suffixed #2, #3, ...

CM-BOT (2026-10-16) adds one "rehits" header line: the hero's damaging hits,
cross-source re-hits within 30 / 20 frames, the same-source contrast,
same-frame doubles, status ticks and unobserved hits, then per kind
"kind=hits/xsrc30". CM-FIXA-PRES (2026-10-16): the summary also carries the
periodic ticks (ticks, tick_damage) and the combat-only figures
(xsrc_30_combat, ...), and the line ends with "ticks: kind=n" per kind.
"""
import json
import os
import sys


def resolve(path):
    if os.path.exists(path):
        return path
    base = os.path.join(os.environ.get("APPDATA", ""), "Godot", "app_userdata", "Fighters Through Time", "playtest")
    candidate = os.path.join(base, path if path.endswith(".json") else path + ".json")
    return candidate


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 2
    every, start, end = 1, 0, 10 ** 9
    swings = False
    args = argv[2:]
    for i, a in enumerate(args):
        if a == "--every":
            every = int(args[i + 1])
        elif a == "--from":
            start = float(args[i + 1])
        elif a == "--to":
            end = float(args[i + 1])
        elif a == "--swings":
            swings = True
    with open(resolve(argv[1]), encoding="utf-8") as f:
        report = json.load(f)
    head = {k: report.get(k) for k in ("level", "difficulty", "bot", "end_reason", "seconds", "progress_px", "checkpoints")}
    print(head)
    print("player", report.get("player"))
    for key in ("events", "deaths_detail", "collapse_cause", "objectives_broken_frame", "interactions"):
        if key in report:
            print(key, json.dumps(report[key])[:1500])
    # D6d summaries (absent from reports older than 2026-10-11).
    if report.get("meter") is not None:
        print("meter", json.dumps(report.get("meter")))
    if isinstance(report.get("swings"), dict):
        print("swings", json.dumps({k: v for k, v in report["swings"].items() if k != "by_hit"}))
    if isinstance(report.get("finishers"), dict):
        print("finishers", json.dumps({k: v for k, v in report["finishers"].items() if k != "log"}))
    if report.get("ability_presses") is not None:
        print("ability_presses", json.dumps(report.get("ability_presses")))
    # MM-BOTS (2026-10-31): the active Defy (log of executions below) and the Momentum gauge.
    if isinstance(report.get("defy"), dict):
        print("defy", json.dumps({k: v for k, v in report["defy"].items() if k != "log"}))
        for d in report["defy"].get("log") or []:
            print(f"  defy {d.get('f', 0) / 60:.1f}s meter={d.get('meter_before')} targets={d.get('targets')} "
                  f"healed={d.get('healed')} hits {d.get('hits_before')}->{d.get('hits_after')} "
                  f"died_within_3s={d.get('died_within_3s')}{' (run ended inside the window)' if d.get('after_truncated') else ''}")
    if isinstance(report.get("momentum"), dict):
        print("momentum", json.dumps(report["momentum"]))
    # C3 (2026-10-11): mob melee swings, without the per-enemy table and the log.
    if isinstance(report.get("mob_swings"), dict):
        print("mob_swings", json.dumps({k: v for k, v in report["mob_swings"].items() if k not in ("by_enemy", "log")}))
    # GU-BOT (2026-10-12): the hero's crowd and the encounter locks.
    if isinstance(report.get("mob_engagement"), dict):
        print("mob_engagement", json.dumps({k: v for k, v in report["mob_engagement"].items()
                                            if not k.endswith("histogram") and k not in ("sums", "far_waiters")}))
        for w in (report["mob_engagement"].get("far_waiters") or [])[:8]:
            print(f"  far waiter {w.get('f', 0) / 60:.1f}s {w.get('foe', '')}@{w.get('x')},{w.get('y')} gap={w.get('gap')}")
    locks = report.get("locks")
    if isinstance(locks, dict):
        print("locks", json.dumps({k: v for k, v in locks.items() if k != "log"}))
        for entry in locks.get("log") or []:
            def sec(frame):
                return f"{frame / 60:.1f}s" if frame is not None and frame >= 0 else "-"
            instance = entry.get("instance") or 1
            label = f"{entry.get('id', '')}#{instance}" if instance > 1 else entry.get("id", "")
            print(f"  lock {label}: {entry.get('state', '')} engage={sec(entry.get('engage_frame'))} "
                  f"clear={sec(entry.get('clear_frame'))} killed={entry.get('killed', 0)}/{entry.get('planned', 0)} "
                  f"living={entry.get('living', 0)} reverts={entry.get('reverts', 0)} "
                  f"left_alive={entry.get('left_with_members_alive', 0)} reloc={entry.get('member_relocations', 0)}"
                  f"{' forced' if entry.get('force_cleared') else ''}{' restored' if entry.get('restored_cleared') else ''}")
    # CM-BOT (2026-10-16): hits on the hero and cross-source re-hits, one line,
    # with by_kind folded to "kind=hits/xsrc30" (no gap histogram, no log).
    rehits = report.get("rehits")
    if isinstance(rehits, dict):
        summary = {k: v for k, v in rehits.items()
                   if k not in ("log", "xsrc_gap_histogram", "by_kind", "ticks_by_kind")}
        kinds = " ".join(f"{kind}={row.get('hits', 0)}/{row.get('xsrc_30', 0)}"
                         for kind, row in sorted((rehits.get("by_kind") or {}).items()))
        # CM-FIXA-PRES: the periodic ticks, counted apart from the hits.
        ticks = rehits.get("ticks_by_kind") or {}
        if ticks:
            kinds += " ticks: " + " ".join(f"{kind}={row.get('ticks', 0)}" for kind, row in sorted(ticks.items()))
        print("rehits", json.dumps(summary), kinds)
    if str(report.get("end_reason", "")).startswith("stuck_lock:"):
        for event in report.get("events") or []:
            if event.get("type") == "nav" and str(event.get("detail", "")).startswith("lock "):
                print("  nav", event.get("detail"))
    if swings:
        for s in report.get("swing_log", []) or []:
            sec = s.get("f", 0) / 60
            if sec < start or sec > end:
                continue
            print(f"{sec:7.2f}s {s.get('hit', ''):<9} {'air' if s.get('air') else '   '} {s.get('out', '')} "
                  f"dmg={s.get('dmg', 0)} n={s.get('n', 0)}/{s.get('n0', 0)} gap={s.get('gap', -1)} "
                  f"gmin={s.get('gmin', -1)} piv={s.get('piv', -1)} {s.get('foe', '')}({s.get('kind', '')}) {s.get('act', '')}")
        return 0
    trace = report.get("trace", [])
    for i, t in enumerate(trace):
        sec = t["f"] / 60
        if sec < start or sec > end or i % every:
            continue
        extra = " ".join(f"{k}={v}" for k, v in t.items() if k not in ("f", "x", "y", "hp", "state", "bot", "climb", "target", "nav", "vx", "vy", "meter", "lock", "sgate", "mo") and v not in ("", None))
        meter = ""
        if t.get("meter") is not None:
            meter = f" m={t['meter']:5.1f}"
            if t.get("mo") is not None:
                meter += f" mo={t['mo']:4.2f}"
            if (t.get("lock") or 0) > 0:
                meter += f" lk={t['lock']}"
            if (t.get("sgate") or 0) > 0:
                meter += f" sg={t['sgate']}"
        print(f"{sec:7.2f}s x={t['x']:6d} y={t['y']:5d} hp={t['hp']:4d}{meter} {t['state']:<14} {t['bot']:<14} v=({t.get('vx',0)},{t.get('vy',0)}) {t.get('target','')} | {t.get('nav', t.get('climb',''))} {extra}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
