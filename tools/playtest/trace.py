"""Print a compact view of a playtest report's once-a-second trace.

Usage: python tools/playtest/trace.py <report.json | run-name> [--every N] [--from S] [--to S] [--swings]

A bare run name (e.g. L02_joan_play_easy_t1) is resolved inside the default
report directory (%APPDATA%/Godot/app_userdata/Fighters Through Time/playtest).

Since 2026-10-11 (D6d) the header also prints the meter, swing, finisher and
ability-press summaries; each trace line shows the Ultimate meter (m=), the
ability lockout (lk=, when running) and, once group A lands, the Special gate
(sg=). --swings lists the hero's swings inside --from/--to instead of the trace.
C3 adds the mob melee swing summary (mob_swings) to the header.
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
    # C3 (2026-10-11): mob melee swings, without the per-enemy table and the log.
    if isinstance(report.get("mob_swings"), dict):
        print("mob_swings", json.dumps({k: v for k, v in report["mob_swings"].items() if k not in ("by_enemy", "log")}))
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
        extra = " ".join(f"{k}={v}" for k, v in t.items() if k not in ("f", "x", "y", "hp", "state", "bot", "climb", "target", "nav", "vx", "vy", "meter", "lock", "sgate") and v not in ("", None))
        meter = ""
        if t.get("meter") is not None:
            meter = f" m={t['meter']:5.1f}"
            if (t.get("lock") or 0) > 0:
                meter += f" lk={t['lock']}"
            if (t.get("sgate") or 0) > 0:
                meter += f" sg={t['sgate']}"
        print(f"{sec:7.2f}s x={t['x']:6d} y={t['y']:5d} hp={t['hp']:4d}{meter} {t['state']:<14} {t['bot']:<14} v=({t.get('vx',0)},{t.get('vy',0)}) {t.get('target','')} | {t.get('nav', t.get('climb',''))} {extra}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
