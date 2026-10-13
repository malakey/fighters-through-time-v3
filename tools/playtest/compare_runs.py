"""Compare playtest batches by tag.

Usage:  python tools/playtest/compare_runs.py [--swings] <tagA> <tagB> [<tagC> ...]

Reads every user://playtest/*.json report whose run name ends with _<tag>
(the runner's --tag; seeds append _s<N>, so a tag also matches its seeded
runs when passed as e.g. "mbefore*") and prints one table per bot with
completion, deaths, damage taken, standard-mob kill times and burst kills.

Since 2026-10-11 (D6d) each tag also gets a "combat" table (combat swings,
connect / contact-without-landing / whiff rates, finisher re-approach frames,
standard-mob carry and landing gap, both over the finishers whose re-approach
ended in a swing) and a "meter and presses" table (meter at level start, gain
per minute, clean bar-fill seconds, Ultimates and Defy procs per run, Special
and Ultimate presses refused). Raw samples are pooled across
runs, never medians of medians; reports without the new keys contribute
nothing, and a cell with no data prints "-". --swings adds a per-hit table.

C3 (2026-10-11) adds to "meter and presses" the Special press policy
(--special-press), the two most common readable conditions on refused Special
presses (top_refusals), the refusals whose only readable reason was the
lockout (sole_lockout), group A's lockout-refusal events (lk_events, "-"
before group A) and the Ultimate's top refusal condition; and a "mobs" table
(natural mob MeleeStrike/ChargeDash swings per run, whiff %, out-of-reach share
of the whiffs, the whiff gap median and the three enemies that whiff most).
The run name does not carry the policy, so compare policies under different tags.

C2 (2026-10-11) adds a "scale" column to "meter and presses": the run's D5 Story
meter-gain scale (--story-meter-scale, "on" / "off", "-" for older reports). The
run name does not carry it either, so an A/B uses two tags.
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


# Must match PlaytestCombatMath.CombatSwingGapPx (scripts/Diagnostics/PlaytestCombatStats.cs).
COMBAT_SWING_GAP_PX = 200


def pct(part, whole):
    return round(100 * part / whole, 1) if whole else "-"


def mean(values, digits=2):
    return round(sum(values) / len(values), digits) if values else "-"


def p90(values):
    """Nearest-rank 90th percentile, matching PlaytestCombatMath.Percentile."""
    if not values:
        return "-"
    ordered = sorted(values)
    rank = -(-90 * len(ordered) // 100) - 1
    return ordered[max(0, min(len(ordered) - 1, rank))]


def num(value, default=-1):
    return default if value is None else value


def is_combat_swing(entry):
    return 0 <= num(entry.get("gap")) <= COMBAT_SWING_GAP_PX


def by_bot(tag_pattern):
    groups = {}
    for r in reports(tag_pattern):
        groups.setdefault(r.get("bot", "?"), []).append(r)
    return sorted(groups.items())


def combat_rows(tag_pattern):
    """D6d: one row per bot from the pooled swing and finisher logs."""
    rows = []
    for bot, runs in by_bot(tag_pattern):
        combat_counts, connected, no_landing, whiffed, in_reach = [], 0, 0, 0, 0
        whiff_gaps, reapproach, std_carry, std_land_gap = [], [], [], []
        finishers, orphans, bases = 0, 0, set()
        for r in runs:
            swings = r.get("swings")
            if isinstance(swings, dict):
                combat_counts.append(num(swings.get("combat"), 0))
                connected += num(swings.get("connected"), 0)
                no_landing += num(swings.get("contact_no_landing"), 0)
                whiffed += num(swings.get("whiffed"), 0)
                in_reach += num(swings.get("whiff_in_reach"), 0)
                orphans += num(swings.get("orphan_basic_hits"), 0)
                if swings.get("connect_basis"):
                    bases.add(swings["connect_basis"])
            for entry in r.get("swing_log") or []:
                if entry.get("out") == "w" and is_combat_swing(entry) and num(entry.get("gmin")) >= 0:
                    whiff_gaps.append(entry["gmin"])
            fin = r.get("finishers")
            if isinstance(fin, dict):
                finishers += num(fin.get("connected"), 0)
                for entry in fin.get("log") or []:
                    if entry.get("outcome") == "swing" and num(entry.get("frames")) >= 0:
                        reapproach.append(entry["frames"])
                    # Spec C §1.4(b): carry and landing-gap medians use swing outcomes only.
                    if entry.get("kind") == "standard" and entry.get("outcome") == "swing":
                        if num(entry.get("carry")) >= 0:
                            std_carry.append(entry["carry"])
                        if num(entry.get("land_gap")) >= 0:
                            std_land_gap.append(entry["land_gap"])
        combat = sum(combat_counts)
        have = bool(combat_counts)
        rows.append({
            "bot": bot,
            "runs": len(runs),
            "swings/run": mean(combat_counts, 1),
            "conn%": pct(connected, combat) if have else "-",
            "x%": pct(no_landing, combat) if have else "-",
            "whiff%": pct(whiffed, combat) if have else "-",
            "in_reach%": pct(in_reach, whiffed) if have else "-",
            "whiff_gap_med": med(whiff_gaps),
            "fin": finishers if have else "-",
            "reapp_med": med(reapproach),
            "reapp_p90": p90(reapproach),
            "std_carry_med": med(std_carry),
            "std_land_gap_med": med(std_land_gap),
            "orphans": orphans if have else "-",
            "basis": "/".join(sorted(bases)) if bases else "-",
        })
    return rows


def add_counts(into, table):
    for key, value in (table or {}).items():
        into[key] = into.get(key, 0) + num(value, 0)


def top_counts(counts, n):
    """The n largest entries as "label:count ..." (ties by label), or "-" when empty."""
    ordered = sorted(((k, v) for k, v in counts.items() if v > 0), key=lambda kv: (-kv[1], kv[0]))
    return " ".join(f"{k}:{v}" for k, v in ordered[:n]) if ordered else "-"


def meter_rows(tag_pattern):
    """D6d: one row per bot from the meter and ability_presses blocks.

    C3 adds the press policy, the two most common readable conditions on a
    refused Special press (refused_with, Special 1 + 2), the refused Special
    presses whose only readable reason was the lockout, group A's own lockout
    refusal events (- before group A) and the Ultimate's top refusal condition.
    """
    rows = []
    for bot, runs in by_bot(tag_pattern):
        starts, gains, fills, dirty, ults, defy = [], [], [], 0, [], []
        s_pressed = s_refused = ult_pressed = ult_refused = 0
        have_presses = False
        policies, s_with, ult_with = set(), {}, {}
        scales = set()
        sole_lockout, have_refusals = 0, False
        lk_events, have_lk_events = 0, False
        for r in runs:
            presses = r.get("ability_presses")
            if isinstance(presses, dict):
                policy = presses.get("policy") or r.get("special_press_policy")
                if policy:
                    policies.add(policy)
                for slot in ("special1", "special2", "ultimate"):
                    counts = presses.get(slot) or {}
                    if isinstance(counts.get("refused_with"), dict):
                        have_refusals = True
                        add_counts(ult_with if slot == "ultimate" else s_with, counts["refused_with"])
                        if slot != "ultimate":
                            sole_lockout += num((counts.get("refused_sole") or {}).get("lockout"), 0)
                    if slot != "ultimate" and counts.get("lockout_events") is not None:
                        have_lk_events = True
                        lk_events += num(counts.get("lockout_events"), 0)
        for r in runs:
            # C2: the run's D5 meter scale (--story-meter-scale); absent before C2.
            if isinstance(r.get("story_meter_scale"), bool):
                scales.add("on" if r["story_meter_scale"] else "off")
            meter = r.get("meter")
            if isinstance(meter, dict):
                if num(meter.get("level_start")) >= 0:
                    starts.append(meter["level_start"])
                if num(meter.get("gain_per_min")) >= 0:
                    gains.append(meter["gain_per_min"])
                fills.extend(meter.get("fill_frames") or [])
                dirty += num(meter.get("fills_dirty"), 0)
                ults.append(num(meter.get("ultimates_cast"), 0))
                defy.append(num(meter.get("defy_procs"), 0))
            presses = r.get("ability_presses")
            if isinstance(presses, dict):
                have_presses = True
                for slot in ("special1", "special2"):
                    counts = presses.get(slot) or {}
                    s_pressed += num(counts.get("pressed"), 0)
                    s_refused += num(counts.get("refused"), 0)
                counts = presses.get("ultimate") or {}
                ult_pressed += num(counts.get("pressed"), 0)
                ult_refused += num(counts.get("refused"), 0)
        rows.append({
            "bot": bot,
            "runs": len(runs),
            "meter0": mean(starts, 1),
            "gain/min": mean(gains, 1),
            "bar_s_med": round(statistics.median(fills) / 60, 1) if fills else "-",
            "dirty_fills": dirty if ults else "-",
            "ults/run": mean(ults),
            "defy/run": mean(defy),
            "s_pressed": s_pressed if have_presses else "-",
            "s_refused": s_refused if have_presses else "-",
            "ult_pressed": ult_pressed if have_presses else "-",
            "ult_refused": ult_refused if have_presses else "-",
            "policy": "/".join(sorted(policies)) if policies else "-",
            "top_refusals": top_counts(s_with, 2) if have_refusals else "-",
            "sole_lockout": sole_lockout if have_refusals else "-",
            "lk_events": lk_events if have_lk_events else "-",
            "ult_top_refusal": top_counts(ult_with, 1) if have_refusals else "-",
            "scale": "/".join(sorted(scales)) if scales else "-",
        })
    return rows


def mob_rows(tag_pattern):
    """C3: one row per bot from the mob_swings blocks (natural closes only).

    mob_swings/run and mob_whiff% come from each report's aggregates; the gap
    median pools the natural whiffs in the logs (at most 500 per run); the
    three enemies with the most whiffs come from by_enemy.
    """
    rows = []
    for bot, runs in by_bot(tag_pattern):
        natural_counts, whiffed, out_of_reach, gaps, by_enemy = [], 0, 0, [], {}
        for r in runs:
            mobs = r.get("mob_swings")
            if not isinstance(mobs, dict):
                continue
            natural_counts.append(num(mobs.get("natural"), 0))
            whiffed += num(mobs.get("whiffed"), 0)
            out_of_reach += num(mobs.get("whiff_out_of_reach"), 0)
            for entry in mobs.get("log") or []:
                if entry.get("natural") and entry.get("out") == "w" and num(entry.get("gap")) >= 0:
                    gaps.append(entry["gap"])
            for enemy, stats in (mobs.get("by_enemy") or {}).items():
                by_enemy[enemy] = by_enemy.get(enemy, 0) + num((stats or {}).get("whiffed"), 0)
        natural = sum(natural_counts)
        have = bool(natural_counts)
        rows.append({
            "bot": bot,
            "runs": len(runs),
            "mob_swings/run": mean(natural_counts, 1),
            "mob_whiff%": pct(whiffed, natural) if have else "-",
            "out_of_reach%": pct(out_of_reach, whiffed) if have else "-",
            "mob_whiff_gap_med": med(gaps),
            "top_whiffers": top_counts(by_enemy, 3) if have else "-",
        })
    return rows


def swing_hit_rows(tag_pattern):
    """D6d --swings: one row per hit (aerial hits keyed <hit>_air), pooled combat swings."""
    hits = {}
    for r in reports(tag_pattern):
        for entry in r.get("swing_log") or []:
            if is_combat_swing(entry):
                key = entry.get("hit", "?") + ("_air" if entry.get("air") else "")
                hits.setdefault(key, []).append(entry)
    rows = []
    for hit, entries in sorted(hits.items()):
        connects = [e for e in entries if e.get("out") == "c"]
        whiffs = [e for e in entries if e.get("out") == "w"]
        rows.append({
            "hit": hit,
            "count": len(entries),
            "conn%": pct(len(connects), len(entries)),
            "whiff%": pct(len(whiffs), len(entries)),
            "whiff_gap_med": med([e["gmin"] for e in whiffs if num(e.get("gmin")) >= 0]),
            "connect_gap_med": med([e["gap"] for e in connects if num(e.get("gap")) >= 0]),
        })
    return rows


def print_table(keys, rows):
    print("| " + " | ".join(keys) + " |")
    print("|" + "---|" * len(keys))
    for row in rows:
        print("| " + " | ".join(str(row[k]) for k in keys) + " |")


def main(tags, swings=False):
    keys = ["bot", "runs", "completed", "boss_down", "deaths", "dmg_taken", "std_killed", "std_ttk_med",
            "std_burst_kills", "std_special_majority", "elite_ttk_med", "passed_alive", "passed_then_hit"]
    for tag in tags:
        print(f"\n### {tag}\n")
        print("| " + " | ".join(keys) + " |")
        print("|" + "---|" * len(keys))
        for row in summarize(tag):
            print("| " + " | ".join(str(row[k]) for k in keys) + " |")
        # D6d (2026-10-11): combat, meter and press tables.
        print("\n#### combat\n")
        print_table(["bot", "runs", "swings/run", "conn%", "x%", "whiff%", "in_reach%", "whiff_gap_med", "fin",
                     "reapp_med", "reapp_p90", "std_carry_med", "std_land_gap_med", "orphans", "basis"],
                    combat_rows(tag))
        print("\n#### meter and presses\n")
        print_table(["bot", "runs", "meter0", "gain/min", "bar_s_med", "dirty_fills", "ults/run", "defy/run",
                     "s_pressed", "s_refused", "ult_pressed", "ult_refused", "policy", "top_refusals",
                     "sole_lockout", "lk_events", "ult_top_refusal", "scale"],
                    meter_rows(tag))
        # C3 (2026-10-11): mob MeleeStrike / ChargeDash swings.
        print("\n#### mobs\n")
        print_table(["bot", "runs", "mob_swings/run", "mob_whiff%", "out_of_reach%", "mob_whiff_gap_med",
                     "top_whiffers"],
                    mob_rows(tag))
        if swings:
            print("\n#### swings by hit\n")
            print_table(["hit", "count", "conn%", "whiff%", "whiff_gap_med", "connect_gap_med"], swing_hit_rows(tag))


if __name__ == "__main__":
    args = sys.argv[1:]
    main([a for a in args if a != "--swings"], swings="--swings" in args)
