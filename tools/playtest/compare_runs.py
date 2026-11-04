"""Compare playtest batches by tag.

Usage:  python tools/playtest/compare_runs.py [--swings] <tagA> <tagB> [<tagC> ...]
        python tools/playtest/compare_runs.py --curve <tagA> [<tagB> ...]

Reads every user://playtest/*.json report whose run name ends with _<tag>
(the runner's --tag; seeds append _s<N>, so a tag also matches its seeded
runs when passed as e.g. "mbefore*") and prints one table per bot with
completion, deaths, damage taken, standard-mob kill times and burst kills.

Since 2026-10-11 (D6d) each tag also gets a "combat" table (combat swings,
connect / contact-without-landing / whiff rates, finisher re-approach frames,
standard-mob carry and landing gap, both over the finishers whose re-approach
ended in a swing) and a "meter and presses" table (meter at level start, gain
per minute, clean bar-fill seconds, Ultimates per run, Special
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

MM-BOTS (2026-10-31 meter and Defy update) adds two tables per tag, "defy" and "momentum".
Batches from before and after that update are NOT comparable: the Momentum gauge replaced the
Special cooldowns, Story meter gain is x0.5, and Defy is an active burst the play bot presses
(the old defy/run column counted the passive proc and is gone; its report key defy_procs with it).
  "defy" - the active Defy (the report's defy block): presses and executions per run, hits
    taken in the 180 frames before and after an execution (per execution), targets blasted and HP
    healed (per execution), and the executions after which the hero died within 3 s.
  "momentum" - the Momentum gauge (the momentum block): pips at level start (2.0 on a fresh
    level), gained per run by source, Specials cast per minute, presses refused for want of a
    pip, and the share of ticks starved (under one pip) or full.

GU-BOT (2026-10-12 gameplay update) adds two tables per tag:
  "locks" - encounter locks per run, the share cleared, runs ended
    stuck_lock:<id>, reverts and leaves with members alive (the 2026-10-12
    meaning of "passed": the hero left an uncleared lock's area with members
    alive), members planned / killed, relocations (fall net + stall watchdog),
    force-clears, and the median frames from a lock's first engage to its clear;
    since FIX-BOT (2026-10-13) locks and cleared count distinct lock IDs, and
    "reloads" is the lock instances a scene reload (an Act III Anchor Snap)
    rebuilt, each reported as its own instance;
  "crowd" - the engagement director's effect on the hero's crowd: mobs in
    Attacking within 250 px of the hero (max over runs, mean pooled over the
    engaged frames), the melee-class ones and the share of engaged frames with
    more of them than the difficulty's slot total, slot holders over the total
    (the director's own invariant, should be 0), slot hand-offs per run, the
    pooled waiter-gap medians (all waiters and the front waiter per side, from
    8 px histograms), the share of waiter samples more than 600 px off the hero
    (far_waiter%: still walking in across a wide area, or held off the ring by a
    ledge, wall or barrier; the report's far_waiters log tells which) and ranged
    attacks in flight.
The main table gains left_lock_alive and passed_out (old-definition passes of
mobs no lock owns). Turn-taking, the 1.5 camera, locks, the -20% speed and
static knockback all landed together, so batches from before and after
2026-10-12 are not comparable on any of these.

CM-BOT (decision 7.2, 2026-10-16) adds a "rehits" table per tag, from each
report's rehits block (absent before CM-BOT: those runs print "-"): the hero's
damaging hits per run (status ticks apart), cross-source re-hits within 30
frames of another source's hit (xsrc30, and its share of the hits), the 20-frame
share, the same-source contrast (same30%, same20%), same-frame doubles from two
sources (sf), the share of hit damage the cross-source re-hits dealt (xsrc_dmg%),
the median gap back to the other source (pooled from the gap histograms), the
three kinds (standard, elite, boss, mirror, projectile, hazard, other) with the
most cross-source re-hits as the second hit, status ticks per run, and
unobserved hits (HP losses no raise site saw; 0 unless a non-hit death).
CM-FIXA-PRES (2026-10-16): every periodic tick (status, zone pulse, drown
tick, stage-hazard DPS) is kept out of hits and every re-hit figure and counted
apart (ticks/run); xsrc30c and xsrc30c% are the combat-only figure (hazard
contacts left out on both sides, over the combat hits). by_kind books a re-hit
under the second hit's kind, so only xsrc30c removes a hazard predecessor.
Reports from before the fix print "-" for both, and their hits include zone
and periodic environmental ticks.

GR-CROWD (gameplay review, 2026-11-03) extends "crowd" with the run's slot table
(--slot-table, not in the run name, so an A/B uses two tags), the melee candidates
per engaged frame (cand_mean, the supply) and the frames with a slot free and a
candidate ringing (slot_idle%, slot_idle_f: the direct test of whether the slot
rules limit pressure), and adds a "swarms" table per tag: swarm waves per run, the
share cleared, frames to clear, peak living, hero damage per swarm, and holders,
attackers within 250 px and candidates per swarm frame. "-" on older reports.

--curve prints only the target curve (tools/playtest/target_curve.json, ruling 4c+)
per tag: completions by level and difficulty for the file's bot, with an 80 % Wilson
interval, flagged "low" or "high" only when the interval misses the target. Hard has
no target and prints "-". Levels 6, 7 and 10 carry their bot-gap notes, and Levels 1-4
print as their own table, because the schedule shift changed their kit. Flags only:
nothing is tuned against them.
"""
import fnmatch
import glob
import json
import os
import statistics
import sys

CURVE_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "target_curve.json")

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
        left_lock, passed_out, have_locks = 0, 0, False
        for r in runs:
            summary = r.get("enemy_summary") or {}
            passed += summary.get("passed_while_alive", 0) or 0
            hits_after_pass += summary.get("passed_then_hit_player", 0) or 0
            # GU-BOT (2026-10-12): the lock meaning of "passed", and old passes of mobs no lock owns.
            if "left_lock_with_members_alive" in summary:
                have_locks = True
                left_lock += summary.get("left_lock_with_members_alive", 0) or 0
                passed_out += summary.get("passed_alive_outside_locks", 0) or 0
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
            "left_lock_alive": left_lock if have_locks else "-",
            "passed_out": passed_out if have_locks else "-",
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
        starts, gains, fills, dirty, ults = [], [], [], 0, []
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


def defy_rows(tag_pattern):
    """MM-BOTS: one row per bot from the defy block (the active Defy; "-" before the meter and Defy update)."""
    rows = []
    for bot, runs in by_bot(tag_pattern):
        blocks = [r["defy"] for r in runs if isinstance(r.get("defy"), dict)]
        if not blocks:
            rows.append({"bot": bot, "runs": len(runs), "policy": "-", "pressed/run": "-", "defies/run": "-",
                         "refused": "-", "hits_before/def": "-", "hits_after/def": "-", "targets/def": "-",
                         "healed/def": "-", "died_3s": "-", "died_3s%": "-", "truncated": "-"})
            continue
        executed = sum(num(b.get("executed"), 0) for b in blocks)
        refused = {}
        for b in blocks:
            add_counts(refused, b.get("refused_by") or {})
        truncated = sum(1 for b in blocks for e in (b.get("log") or []) if e.get("after_truncated"))
        died = sum(num(b.get("died_within_3s"), 0) for b in blocks)

        def per(key):
            return round(sum(num(b.get(key), 0) for b in blocks) / executed, 2) if executed else "-"

        rows.append({
            "bot": bot,
            "runs": len(runs),
            "policy": "/".join(sorted({b.get("policy", "-") for b in blocks})),
            "pressed/run": mean([num(b.get("pressed"), 0) for b in blocks]),
            "defies/run": mean([num(b.get("executed"), 0) for b in blocks]),
            "refused": top_counts(refused, 2),
            "hits_before/def": per("hits_before"),
            "hits_after/def": per("hits_after"),
            "targets/def": per("targets_blasted"),
            "healed/def": per("healed"),
            "died_3s": died,
            "died_3s%": pct(died, executed) if executed else "-",
            "truncated": truncated,
        })
    return rows


def momentum_rows(tag_pattern):
    """MM-BOTS: one row per bot from the momentum block (the Momentum gauge; "-" before the update)."""
    rows = []
    for bot, runs in by_bot(tag_pattern):
        blocks = [r["momentum"] for r in runs if isinstance(r.get("momentum"), dict)]
        if not blocks:
            rows.append({"bot": bot, "runs": len(runs), "pips0": "-", "gained/run": "-", "top_sources": "-",
                         "cast/min": "-", "s1/run": "-", "s2/run": "-", "starved_presses": "-",
                         "starved%": "-", "full%": "-"})
            continue
        sources = {}
        for b in blocks:
            for key, value in (b.get("gained_by") or {}).items():
                sources[key] = sources.get(key, 0) + value / len(blocks)
        frames = sum(num(b.get("tracked_frames"), 0) for b in blocks)
        casts = sum(num(b.get("specials_cast"), 0) for b in blocks)
        rows.append({
            "bot": bot,
            "runs": len(runs),
            "pips0": mean([b["pips_start"] for b in blocks if num(b.get("pips_start")) >= 0], 2),
            "gained/run": mean([num(b.get("gained"), 0) for b in blocks]),
            "top_sources": " ".join(f"{k}:{v:.2f}" for k, v in sorted(sources.items(), key=lambda kv: -kv[1])[:3]) or "-",
            "cast/min": round(casts / (frames / 3600), 2) if frames else "-",
            "s1/run": mean([num((b.get("spent") or {}).get("special1"), 0) for b in blocks]),
            "s2/run": mean([num((b.get("spent") or {}).get("special2"), 0) for b in blocks]),
            "starved_presses": sum(num(b.get("starved_presses"), 0) for b in blocks),
            "starved%": pct(sum(num(b.get("frames_starved"), 0) for b in blocks), frames) if frames else "-",
            "full%": pct(sum(num(b.get("frames_full"), 0) for b in blocks), frames) if frames else "-",
        })
    return rows


def rank_rows(tag_pattern):
    """GR-PILOT: one row per bot from the rank block ("-" before it, and on a run that did not complete)."""
    rows = []
    for bot, runs in by_bot(tag_pattern):
        blocks = [r["rank"] for r in runs if isinstance(r.get("rank"), dict) and r["rank"].get("letter")]
        if not blocks:
            rows.append({"bot": bot, "runs": len(runs), "ranked": 0, "S": "-", "A": "-", "B": "-", "C": "-", "D": "-",
                         "points": "-", "damage": "-", "speed": "-", "dust/ranked": "-"})
            continue
        speeds = [num(b.get("speed_score")) for b in blocks if num(b.get("speed_score"), -1) >= 0]
        row = {"bot": bot, "runs": len(runs), "ranked": len(blocks)}
        for letter in "SABCD":
            row[letter] = sum(1 for b in blocks if b.get("letter") == letter)
        row["points"] = mean([num(b.get("points"), 0) for b in blocks])
        row["damage"] = mean([num(b.get("damage_score"), 0) for b in blocks])
        row["speed"] = mean(speeds) if speeds else "-"
        row["dust/ranked"] = mean([num(b.get("dust_paid"), 0) for b in blocks])
        rows.append(row)
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


def hist_median(histogram):
    """Lower median of a {value: count} histogram (PlaytestCombatMath's sorted[n // 2]); "-" when empty."""
    items = sorted((int(k), v) for k, v in histogram.items() if v > 0)
    total = sum(v for _, v in items)
    if total == 0:
        return "-"
    index = total // 2
    for value, count in items:
        if index < count:
            return value
        index -= count
    return "-"


def lock_rows(tag_pattern):
    """GU-BOT: one row per bot from the locks blocks (and stuck_lock end reasons).

    FIX-BOT (2026-10-13): count and cleared are distinct lock IDs, so a lock an
    Act III Anchor Snap rebuilt (a scene reload) is neither counted twice nor
    read as uncleared when the hero had cleared it before the Snap; reloads is
    the rebuilt instances, and planned / killed sum per instance. A report from
    before the fix has no "reloads" (read as 0)."""
    rows = []
    for bot, runs in by_bot(tag_pattern):
        have = [r for r in runs if isinstance(r.get("locks"), dict)]
        counts, cleared, reverts, left, planned, killed = [], 0, 0, 0, 0, 0
        relocations, stall, forced, to_clear, reloads = 0, 0, 0, [], 0
        for r in have:
            locks = r["locks"]
            counts.append(num(locks.get("count"), 0))
            cleared += num(locks.get("cleared"), 0)
            reloads += num(locks.get("reloads"), 0)
            reverts += num(locks.get("reverts"), 0)
            left += num(locks.get("left_with_members_alive"), 0)
            planned += num(locks.get("members_planned"), 0)
            killed += num(locks.get("members_killed"), 0)
            relocations += num(locks.get("member_relocations"), 0)
            stall += num(locks.get("stall_relocations"), 0)
            forced += num(locks.get("force_cleared"), 0)
            for entry in locks.get("log") or []:
                if num(entry.get("frames_to_clear")) >= 0 and not entry.get("restored_cleared"):
                    to_clear.append(entry["frames_to_clear"])
        total = sum(counts)
        ok = bool(have)
        rows.append({
            "bot": bot,
            "runs": len(runs),
            "locks/run": mean(counts, 1),
            "cleared%": pct(cleared, total) if ok else "-",
            "stuck_lock": sum(1 for r in runs if str(r.get("end_reason", "")).startswith("stuck_lock:")),
            "reloads": reloads if ok else "-",
            "reverts": reverts if ok else "-",
            "left_alive": left if ok else "-",
            "planned": planned if ok else "-",
            "killed": killed if ok else "-",
            "relocations": relocations if ok else "-",
            "stall_reloc": stall if ok else "-",
            "forced": forced if ok else "-",
            "clear_f_med": med(to_clear),
        })
    return rows


def crowd_rows(tag_pattern):
    """GU-BOT: one row per bot from the mob_engagement blocks (raw counts pooled, never medians of medians)."""
    rows = []
    for bot, runs in by_bot(tag_pattern):
        have = [r for r in runs if isinstance(r.get("mob_engagement"), dict)]
        engaged = atk_sum = melee_sum = ranged_sum = 0
        atk_max = melee_max = holders_max = ranged_max = 0
        over_slots = gated_over = holders_over = ranged_over = 0
        handoffs, grants, slots, waiters, front = [], [], set(), {}, {}
        # GR-CROWD: the supply and the idle-slot test; absent before it.
        cand_sum, idle_frames, cand_frames, tables = 0, 0, 0, set()
        far_samples, waiter_samples = 0, 0
        for r in have:
            e = r["mob_engagement"]
            frames = num(e.get("engaged_frames"), 0)
            engaged += frames
            # Raw sums over each run's engaged frames, so the pooled mean is exact.
            sums = e.get("sums") or {}
            atk_sum += num(sums.get("attacking_near"), 0)
            melee_sum += num(sums.get("melee_attacking_near"), 0)
            ranged_sum += num(sums.get("ranged_in_flight"), 0)
            if "melee_candidates" in sums:
                cand_sum += num(sums.get("melee_candidates"), 0)
                idle_frames += num(e.get("slot_idle_with_waiters_frames"), 0)
                cand_frames += frames
            if isinstance(r.get("slot_table"), str):
                tables.add(r["slot_table"])
            atk_max = max(atk_max, num(e.get("attacking_near_max"), 0))
            melee_max = max(melee_max, num(e.get("melee_attacking_near_max"), 0))
            holders_max = max(holders_max, num(e.get("slot_holders_max"), 0))
            ranged_max = max(ranged_max, num(e.get("ranged_in_flight_max"), 0))
            over_slots += num(e.get("frames_melee_over_slots"), 0)
            # FIX-ENEMY: the over-slot frames that are not retaliation (should be 0).
            gated_over += num(e.get("frames_gated_melee_over_slots"), 0)
            holders_over += num(e.get("frames_holders_over_slots"), 0)
            ranged_over += num(e.get("frames_ranged_over_permits"), 0)
            handoffs.append(num(e.get("slot_handoffs"), 0))
            grants.append(num(e.get("slot_grants"), 0))
            if num(e.get("slot_total")) > 0:
                slots.add(str(e["slot_total"]))
            if "far_waiter_samples" in e:
                far_samples += num(e.get("far_waiter_samples"), 0)
                waiter_samples += num(e.get("waiter_gap_samples"), 0)
            for key, count in (e.get("waiter_gap_histogram") or {}).items():
                waiters[key] = waiters.get(key, 0) + num(count, 0)
            for key, count in (e.get("front_waiter_gap_histogram") or {}).items():
                front[key] = front.get(key, 0) + num(count, 0)
        ok = bool(have)
        rows.append({
            "bot": bot,
            "runs": len(runs),
            "slots": "/".join(sorted(slots)) if slots else "-",
            "table": "/".join(sorted(tables)) if tables else "-",
            "cand_mean": round(cand_sum / cand_frames, 2) if cand_frames else "-",
            "slot_idle%": pct(idle_frames, cand_frames) if cand_frames else "-",
            "slot_idle_f": idle_frames if cand_frames else "-",
            "engaged_s": round(engaged / 60, 1) if ok else "-",
            "atk_max": atk_max if ok else "-",
            "atk_mean": round(atk_sum / engaged, 2) if engaged else "-",
            "melee_max": melee_max if ok else "-",
            "melee_mean": round(melee_sum / engaged, 2) if engaged else "-",
            "over_slots%": pct(over_slots, engaged) if engaged else "-",
            "gated_over_f": gated_over if ok else "-",
            "holders_max": holders_max if ok else "-",
            "holders_over_f": holders_over if ok else "-",
            "handoffs/run": mean(handoffs, 1),
            "grants/run": mean(grants, 1),
            "waiter_gap_med": hist_median(waiters),
            "front_gap_med": hist_median(front),
            "far_waiter%": pct(far_samples, waiter_samples) if waiter_samples else "-",
            "ranged_max": ranged_max if ok else "-",
            "ranged_mean": round(ranged_sum / engaged, 2) if engaged else "-",
            "ranged_over_f": ranged_over if ok else "-",
        })
    return rows


def swarm_rows(tag_pattern):
    """GR-CROWD: one row per bot from the swarms blocks (raw counts pooled, never medians of medians)."""
    rows = []
    for bot, runs in by_bot(tag_pattern):
        have = [r for r in runs if isinstance(r.get("swarms"), dict)]
        log = [entry for r in have for entry in (r["swarms"].get("log") or [])]
        frames = sum(num(s.get("frames"), 0) for s in log)
        cleared = [s for s in log if num(s.get("frames_to_clear")) >= 0]
        rows.append({
            "bot": bot,
            "runs": len(runs),
            "swarms/run": round(len(log) / len(have), 2) if have else "-",
            "cleared%": pct(len(cleared), len(log)) if log else "-",
            "clear_f_med": med([s["frames_to_clear"] for s in cleared]),
            "peak_med": med([num(s.get("peak_living"), 0) for s in log]),
            "dmg/swarm": round(sum(num(s.get("hero_damage"), 0) for s in log) / len(log), 1) if log else "-",
            "holders_mean": round(sum(num(s.get("holders_sum"), 0) for s in log) / frames, 2) if frames else "-",
            "atk_mean": round(sum(num(s.get("attacking_near_sum"), 0) for s in log) / frames, 2) if frames else "-",
            "cand_mean": round(sum(num(s.get("melee_candidates_sum"), 0) for s in log) / frames, 2) if frames else "-",
        })
    return rows


def wilson(successes, n, confidence):
    """The Wilson score interval of a proportion."""
    z = statistics.NormalDist().inv_cdf(0.5 + confidence / 2)
    p = successes / n
    denominator = 1 + z * z / n
    centre = (p + z * z / (2 * n)) / denominator
    half = z * ((p * (1 - p) / n + z * z / (4 * n * n)) ** 0.5) / denominator
    return max(0.0, centre - half), min(1.0, centre + half)


def curve_rows(tag_pattern, curve):
    """Ruling 4c+: one row per level and difficulty, flagged only when the interval misses the target."""
    tally = {}
    for r in reports(tag_pattern):
        level = r.get("level")
        if r.get("bot") != curve["bot"] or not isinstance(level, int) or not 1 <= level <= 15:
            continue
        key = (level, str(r.get("difficulty", "?")).lower())
        runs, done = tally.get(key, (0, 0))
        tally[key] = (runs + 1, done + (1 if r.get("level_completed") else 0))
    rows = []
    for (level, difficulty), (runs, done) in sorted(tally.items()):
        targets = curve["difficulties"].get(difficulty)
        target = targets.get(str(level)) if targets else None
        low, high = wilson(done, runs, curve["wilson_confidence"])
        flag = "-" if target is None else "low" if high < target else "high" if low > target else "ok"
        rows.append({
            "level": level,
            "difficulty": difficulty,
            "runs": runs,
            "completed": done,
            "rate": round(done / runs, 3),
            "wilson": f"{low:.2f}-{high:.2f}",
            "target": "-" if target is None else target,
            "flag": flag,
            "note": curve["gap_notes"].get(str(level), ""),
        })
    return rows


def print_curve(tags):
    with open(CURVE_PATH, encoding="utf-8") as handle:
        curve = json.load(handle)
    keys = ["level", "difficulty", "runs", "completed", "rate", "wilson", "target", "flag", "note"]
    for tag in tags:
        rows = curve_rows(tag, curve)
        print(f"\n### {tag}: target curve ({curve['bot']} bot, {round(curve['wilson_confidence'] * 100)} % Wilson)")
        print("\n#### Levels 1-4 (the shifted schedule's kit)\n")
        print_table(keys, [row for row in rows if row["level"] <= 4])
        print("\n#### Levels 5-15\n")
        print_table(keys, [row for row in rows if row["level"] >= 5])
        flagged = [row for row in rows if row["flag"] in ("low", "high")]
        print(f"\nFlagged: {len(flagged)} of {len(rows)} (flags only; nothing is tuned against them).")


def rehit_rows(tag_pattern):
    """CM-BOT (7.2): one row per bot from the rehits blocks (raw counts pooled, never medians of medians)."""
    rows = []
    for bot, runs in by_bot(tag_pattern):
        have = [r for r in runs if isinstance(r.get("rehits"), dict)]
        hits, damage, x30, x20, s30, s20, sf, xdmg, status, unobserved = 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
        # CM-FIXA-PRES: the combat-only figure and the periodic ticks, from the
        # reports that carry them (absent before the fix).
        combat_hits, x30c, ticks, have_combat = 0, 0, 0, 0
        gaps, by_kind, per_run = {}, {}, []
        for r in have:
            b = r["rehits"]
            if "xsrc_30_combat" in b:
                have_combat += 1
                combat_hits += num(b.get("combat_hits"), 0)
                x30c += num(b.get("xsrc_30_combat"), 0)
                ticks += num(b.get("ticks"), 0)
            run_hits = num(b.get("hits"), 0)
            per_run.append(run_hits)
            hits += run_hits
            damage += num(b.get("hit_damage"), 0)
            x30 += num(b.get("xsrc_30"), 0)
            x20 += num(b.get("xsrc_20"), 0)
            s30 += num(b.get("same_30"), 0)
            s20 += num(b.get("same_20"), 0)
            sf += num(b.get("xsrc_same_frame"), 0)
            xdmg += num(b.get("xsrc_damage_30"), 0)
            status += num(b.get("status_ticks"), 0)
            unobserved += num(b.get("unobserved_hits"), 0)
            add_counts(gaps, b.get("xsrc_gap_histogram"))
            for kind, row in (b.get("by_kind") or {}).items():
                by_kind[kind] = by_kind.get(kind, 0) + num((row or {}).get("xsrc_30"), 0)
        ok = bool(have)
        rows.append({
            "bot": bot,
            "runs": len(runs),
            "hits/run": mean(per_run, 1),
            "xsrc30": x30 if ok else "-",
            "xsrc30%": pct(x30, hits) if ok else "-",
            "xsrc30c": x30c if have_combat else "-",
            "xsrc30c%": pct(x30c, combat_hits) if have_combat else "-",
            "xsrc20%": pct(x20, hits) if ok else "-",
            "same30%": pct(s30, hits) if ok else "-",
            "same20%": pct(s20, hits) if ok else "-",
            "sf": sf if ok else "-",
            "xsrc_dmg%": pct(xdmg, damage) if ok else "-",
            "xgap_med": hist_median(gaps),
            "top_kinds": top_counts(by_kind, 3) if ok else "-",
            "status/run": round(status / len(have), 1) if ok else "-",
            "ticks/run": round(ticks / have_combat, 1) if have_combat else "-",
            "unobserved": unobserved if ok else "-",
        })
    return rows


# Must match BasicComboRules.AirAttackHitboxID: the air strike is airborne by
# definition, so its by-hit key carries no "_air" suffix (PlaytestCombatMath.SwingHitKey).
AIR_ATTACK_HIT = "air_attack"


def swing_hit_rows(tag_pattern):
    """D6d --swings: one row per hit (aerial hits keyed <hit>_air; the GU-A air strike is air_attack), pooled combat swings."""
    hits = {}
    for r in reports(tag_pattern):
        for entry in r.get("swing_log") or []:
            if is_combat_swing(entry):
                hit = entry.get("hit", "?")
                key = hit + ("_air" if entry.get("air") and hit != AIR_ATTACK_HIT else "")
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
            "std_burst_kills", "std_special_majority", "elite_ttk_med", "passed_alive", "passed_then_hit",
            "left_lock_alive", "passed_out"]
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
        print_table(["bot", "runs", "meter0", "gain/min", "bar_s_med", "dirty_fills", "ults/run",
                     "s_pressed", "s_refused", "ult_pressed", "ult_refused", "policy", "top_refusals",
                     "sole_lockout", "lk_events", "ult_top_refusal", "scale"],
                    meter_rows(tag))
        # MM-BOTS (2026-10-31): the active Defy and the Momentum gauge.
        print("\n#### defy\n")
        print_table(["bot", "runs", "policy", "pressed/run", "defies/run", "refused", "hits_before/def", "hits_after/def",
                     "targets/def", "healed/def", "died_3s", "died_3s%", "truncated"],
                    defy_rows(tag))
        print("\n#### momentum\n")
        print_table(["bot", "runs", "pips0", "gained/run", "top_sources", "cast/min", "s1/run", "s2/run",
                     "starved_presses", "starved%", "full%"],
                    momentum_rows(tag))
        # GR-PILOT (2026-11-03): the level rank of the runs that completed.
        print("\n#### rank\n")
        print_table(["bot", "runs", "ranked", "S", "A", "B", "C", "D", "points", "damage", "speed", "dust/ranked"],
                    rank_rows(tag))
        # C3 (2026-10-11): mob MeleeStrike / ChargeDash swings.
        print("\n#### mobs\n")
        print_table(["bot", "runs", "mob_swings/run", "mob_whiff%", "out_of_reach%", "mob_whiff_gap_med",
                     "top_whiffers"],
                    mob_rows(tag))
        # GU-BOT (2026-10-12): encounter locks and the hero's crowd.
        print("\n#### locks\n")
        print_table(["bot", "runs", "locks/run", "cleared%", "stuck_lock", "reloads", "reverts", "left_alive", "planned", "killed",
                     "relocations", "stall_reloc", "forced", "clear_f_med"],
                    lock_rows(tag))
        print("\n#### crowd\n")
        print_table(["bot", "runs", "slots", "table", "engaged_s", "atk_max", "atk_mean", "melee_max", "melee_mean",
                     "cand_mean", "slot_idle%", "slot_idle_f", "over_slots%", "holders_max", "holders_over_f",
                     "handoffs/run", "grants/run", "waiter_gap_med", "front_gap_med", "far_waiter%", "ranged_max",
                     "ranged_mean", "ranged_over_f"],
                    crowd_rows(tag))
        # GR-CROWD (2026-11-03): per-swarm clear time, peak, damage and crowd.
        print("\n#### swarms\n")
        print_table(["bot", "runs", "swarms/run", "cleared%", "clear_f_med", "peak_med", "dmg/swarm", "holders_mean",
                     "atk_mean", "cand_mean"],
                    swarm_rows(tag))
        # CM-BOT (2026-10-16): hits on the hero by source, and cross-source re-hits.
        print("\n#### rehits\n")
        print_table(["bot", "runs", "hits/run", "xsrc30", "xsrc30%", "xsrc30c", "xsrc30c%", "xsrc20%", "same30%",
                     "same20%", "sf", "xsrc_dmg%", "xgap_med", "top_kinds", "status/run", "ticks/run", "unobserved"],
                    rehit_rows(tag))
        if swings:
            print("\n#### swings by hit\n")
            print_table(["hit", "count", "conn%", "whiff%", "whiff_gap_med", "connect_gap_med"], swing_hit_rows(tag))


if __name__ == "__main__":
    args = sys.argv[1:]
    if "--curve" in args:
        print_curve([a for a in args if not a.startswith("--")])
    else:
        main([a for a in args if a != "--swings"], swings="--swings" in args)
