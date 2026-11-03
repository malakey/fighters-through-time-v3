"""Fighter CPU matrix analysis.

Reads the matches.csv that tests/Tools/FighterMatrixMeasureTests.cs writes and prints, for every
(stage, difficulty) group: the hero win table, the slot-1 advantage, match length, how matches end, the
HP-versus-blast-zone KO share and per-hero action counts. It also writes, beside the CSV (or into --out):

    win_matrix_<stage>_<difficulty>.md   row hero (slot 1) against column hero (slot 2), win %
    analysis_output.txt                  everything printed

Usage:  py tools/fighter_matrix/analyse.py <matches.csv> [--out DIR]
"""
import argparse
import collections
import csv
import math
import statistics as st
from pathlib import Path

HEROES = ["einstein", "joan", "leonardo", "lincoln", "cleopatra", "tesla", "shakespeare", "mozart", "tubman"]
TEXT_COLUMNS = ("stage", "difficulty", "hero1", "hero2", "end", "winner")


def pct(x, n):
    return 100.0 * x / n if n else float("nan")


def moe(p, n):
    """95 % margin of error in percentage points."""
    return 196 * math.sqrt(max(p * (1 - p), 1e-9) / n) if n else float("nan")


def load(path):
    with open(path, newline="", encoding="utf-8") as handle:
        rows = list(csv.DictReader(handle))
    for row in rows:
        for key, value in row.items():
            if key not in TEXT_COLUMNS:
                row[key] = int(value)
    return rows


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("matches", type=Path, help="the matches.csv the test wrote")
    parser.add_argument("--out", type=Path, default=None, help="output folder (default: beside the CSV)")
    args = parser.parse_args()
    out_dir = args.out or args.matches.parent
    out_dir.mkdir(parents=True, exist_ok=True)

    rows = load(args.matches)
    groups = collections.defaultdict(list)
    for row in rows:
        groups[(row["stage"], row["difficulty"])].append(row)

    out = []

    def say(text=""):
        print(text)
        out.append(text)

    say("groups: " + ", ".join(f"{k[0]}/{k[1]}={len(v)}" for k, v in groups.items()))
    for key, ms in groups.items():
        say(f"\n## {key[0]} {key[1]} ({len(ms)} matches)")
        # hero wins: draws count half; capped matches ("none") are excluded
        wins = collections.Counter()
        games = collections.Counter()
        for row in ms:
            if row["winner"] == "none":
                continue
            for side, hero in ((0, row["hero1"]), (1, row["hero2"])):
                games[hero] += 1
                wins[hero] += 0.5 if row["winner"] == "draw" else (1.0 if int(row["winner"]) == side else 0.0)
        say("hero win % (all appearances, both slots, mirrors count 1 win + 1 loss), best to worst:")
        say("| hero | win% | +-95% | games |")
        say("|---|---|---|---|")
        for hero in sorted(HEROES, key=lambda h: -wins[h] / max(games[h], 1)):
            if not games[hero]:
                continue
            p = wins[hero] / games[hero]
            say(f"| {hero} | {100 * p:.1f} | {moe(p, games[hero]):.1f} | {games[hero]} |")

        # win matrix: row hero in slot 1 against column hero in slot 2
        lines = [f"# Win % of row hero (slot 1) vs column hero (slot 2), {key[0]} {key[1]}", "",
                 "| | " + " | ".join(h[:4] for h in HEROES) + " |", "|---" * 10 + "|"]
        for a in HEROES:
            cells = []
            for b in HEROES:
                sub = [r for r in ms if r["hero1"] == a and r["hero2"] == b and r["winner"] != "none"]
                if not sub:
                    cells.append("-")
                    continue
                x = sum(0.5 if r["winner"] == "draw" else (1 if r["winner"] == "0" else 0) for r in sub)
                cells.append(f"{100 * x / len(sub):.0f}")
            lines.append(f"| {a[:4]} | " + " | ".join(cells) + " |")
        (out_dir / f"win_matrix_{key[0]}_{key[1]}.md").write_text("\n".join(lines) + "\n", encoding="utf-8")

        # slot advantage
        mirrors = [r for r in ms if r["hero1"] == r["hero2"] and r["winner"] not in ("none", "draw")]
        m0 = sum(1 for r in mirrors if r["winner"] == "0")
        decided = [r for r in ms if r["winner"] not in ("none", "draw")]
        a0 = sum(1 for r in decided if r["winner"] == "0")
        say(f"mirror slot-1 win: {m0}/{len(mirrors)} = {pct(m0, len(mirrors)):.1f}% "
            f"(+-{moe(m0 / max(len(mirrors), 1), len(mirrors)):.1f}); "
            f"all matches slot-1 win: {a0}/{len(decided)} = {pct(a0, len(decided)):.1f}% "
            f"(+-{moe(a0 / max(len(decided), 1), len(decided)):.1f})")

        # length and endings
        seconds = sorted(r["ticks"] / 60 for r in ms)

        def quantile(p):
            return seconds[min(len(seconds) - 1, int(p * len(seconds)))]

        stocks_lost = sum(r["lost1"] + r["lost2"] for r in ms)
        say(f"length s: median {st.median(seconds):.0f} p10 {quantile(.1):.0f} p90 {quantile(.9):.0f} "
            f"max {seconds[-1]:.0f}; seconds per stock lost {sum(seconds) / max(stocks_lost, 1):.1f}")
        ends = collections.Counter(r["end"] for r in ms)
        say("end: " + ", ".join(f"{k} {v} ({pct(v, len(ms)):.1f}%)" for k, v in ends.items())
            + f"; draws {sum(1 for r in ms if r['winner'] == 'draw')}"
            + f"; capped/none {sum(1 for r in ms if r['winner'] == 'none')}")
        hp = sum(r["p1_ko_hp"] + r["p2_ko_hp"] for r in ms)
        pit = sum(r["p1_ko_pit"] + r["p2_ko_pit"] for r in ms)
        say(f"KOs: hp {hp} ({pct(hp, hp + pit):.1f}%), blast zone {pit} ({pct(pit, hp + pit):.1f}%)")

        # per-hero actions per match
        acc = collections.defaultdict(collections.Counter)
        count = collections.Counter()
        for r in ms:
            for prefix, hero in (("p1", r["hero1"]), ("p2", r["hero2"])):
                count[hero] += 1
                for field in ("s1", "s2", "ult", "defy", "grab", "blocks", "dmg", "ko_pit"):
                    acc[hero][field] += r[f"{prefix}_{field}"]
                acc[hero]["secs"] += r["ticks"] / 60
        say("per hero per match: | hero | S1 | S2 | Ult | Defy | grab | blocks | dmg dealt | own pit KOs | s/match |")
        say("|---|---|---|---|---|---|---|---|---|---|")
        for hero in HEROES:
            if not count[hero]:
                continue
            c, a = count[hero], acc[hero]
            say(f"| {hero} | " + " | ".join(
                f"{a[f] / c:.1f}" for f in ("s1", "s2", "ult", "defy", "grab", "blocks", "dmg", "ko_pit", "secs")) + " |")

    (out_dir / "analysis_output.txt").write_text("\n".join(out), encoding="utf-8")


if __name__ == "__main__":
    main()
