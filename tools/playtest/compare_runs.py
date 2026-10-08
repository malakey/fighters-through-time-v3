"""Compare playtest batches by tag.

Usage:  python tools/playtest/compare_runs.py <tagA> <tagB> [<tagC> ...]

Reads every user://playtest/*.json report whose run name ends with _<tag>
(the runner's --tag; seeds append _s<N>, so a tag also matches its seeded
runs when passed as e.g. "mbefore*") and prints one table per bot with
completion, deaths, damage taken, standard-mob kill times and burst kills.
"""
import fnmatch
import glob
import json
import os
import statistics
import sys

OUT = os.path.join(os.environ.get("APPDATA", ""), "Godot", "app_userdata", "Fighters Through Time", "playtest")


def reports(tag_pattern):
    for path in glob.glob(os.path.join(OUT, "L*.json")):
        name = os.path.basename(path)[:-5]
        tag = name.split("_", 4)[-1] if name.count("_") >= 4 else ""
        if fnmatch.fnmatch(tag, tag_pattern):
            try:
                with open(path, encoding="utf-8-sig") as handle:
                    yield json.load(handle)
            except (OSError, ValueError):
                continue


def med(values):
    return round(statistics.median(values)) if values else "-"


def summarize(tag_pattern):
    by_bot = {}
    for r in reports(tag_pattern):
        by_bot.setdefault(r.get("bot", "?"), []).append(r)
    rows = []
    for bot, runs in sorted(by_bot.items()):
        std_ttk, elite_ttk, std_killed, std_burst, std_special = [], [], 0, 0, 0
        hits_after_pass, passed = 0, 0
        for r in runs:
            for e in r.get("enemies", []):
                kind = e.get("kind")
                ttk = e.get("frames_to_kill", -1)
                if ttk is None or ttk < 0 or e.get("damage_taken", 0) <= 0:
                    pass
                elif kind == "standard":
                    std_ttk.append(ttk)
                    std_killed += 1
                    if e.get("max_burst_2s", 0) >= e.get("max_hp", 1):
                        std_burst += 1
                    by_action = e.get("damage_by_bot_action", {}) or {}
                    special = sum(v for k, v in by_action.items() if "special" in k or k in ("specials", "ultimate"))
                    if special * 2 >= max(1, e.get("damage_taken", 0)):
                        std_special += 1
                elif kind == "elite":
                    elite_ttk.append(ttk)
        for r in runs:
            summary = r.get("enemy_summary") or {}
            passed += summary.get("passed_while_alive", 0) or 0
            hits_after_pass += summary.get("passed_then_hit_player", 0) or 0
        n = len(runs)
        rows.append({
            "bot": bot,
            "runs": n,
            "completed": sum(1 for r in runs if r.get("level_completed")),
            "boss_down": sum(1 for r in runs if (r.get("boss_defeated_frame", -1) or -1) >= 0),
            "deaths": round(sum(r["player"]["deaths"] for r in runs) / max(1, n), 2),
            "dmg_taken": round(sum(r["player"]["damage_taken"] for r in runs) / max(1, n)),
            "std_killed": std_killed,
            "std_ttk_med": med(std_ttk),
            "std_burst_kills": f"{std_burst} ({round(100 * std_burst / max(1, std_killed))}%)",
            "std_special_majority": f"{std_special} ({round(100 * std_special / max(1, std_killed))}%)",
            "elite_ttk_med": med(elite_ttk),
            "passed_alive": passed,
            "passed_then_hit": hits_after_pass,
        })
    return rows


def main(tags):
    keys = ["bot", "runs", "completed", "boss_down", "deaths", "dmg_taken", "std_killed", "std_ttk_med",
            "std_burst_kills", "std_special_majority", "elite_ttk_med", "passed_alive", "passed_then_hit"]
    for tag in tags:
        print(f"\n### {tag}\n")
        print("| " + " | ".join(keys) + " |")
        print("|" + "---|" * len(keys))
        for row in summarize(tag):
            print("| " + " | ".join(str(row[k]) for k in keys) + " |")


if __name__ == "__main__":
    main(sys.argv[1:])
