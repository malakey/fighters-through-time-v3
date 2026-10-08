"""Print a compact view of a playtest report's once-a-second trace.

Usage: python tools/playtest/trace.py <report.json | run-name> [--every N] [--from S] [--to S]

A bare run name (e.g. L02_joan_play_easy_t1) is resolved inside the default
report directory (%APPDATA%/Godot/app_userdata/Fighters Through Time/playtest).
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
    args = argv[2:]
    for i, a in enumerate(args):
        if a == "--every":
            every = int(args[i + 1])
        elif a == "--from":
            start = float(args[i + 1])
        elif a == "--to":
            end = float(args[i + 1])
    with open(resolve(argv[1]), encoding="utf-8") as f:
        report = json.load(f)
    head = {k: report.get(k) for k in ("level", "difficulty", "bot", "end_reason", "seconds", "progress_px", "checkpoints")}
    print(head)
    print("player", report.get("player"))
    for key in ("events", "deaths_detail", "collapse_cause", "objectives_broken_frame", "interactions"):
        if key in report:
            print(key, json.dumps(report[key])[:1500])
    trace = report.get("trace", [])
    for i, t in enumerate(trace):
        sec = t["f"] / 60
        if sec < start or sec > end or i % every:
            continue
        extra = " ".join(f"{k}={v}" for k, v in t.items() if k not in ("f", "x", "y", "hp", "state", "bot", "climb", "target", "nav", "vx", "vy") and v not in ("", None))
        print(f"{sec:7.2f}s x={t['x']:6d} y={t['y']:5d} hp={t['hp']:4d} {t['state']:<14} {t['bot']:<14} v=({t.get('vx',0)},{t.get('vy',0)}) {t.get('target','')} | {t.get('nav', t.get('climb',''))} {extra}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
