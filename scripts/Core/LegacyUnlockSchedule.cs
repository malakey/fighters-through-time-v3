using System;
using System.Collections.Generic;

namespace FTT.Core {

    /// <summary>
    /// V7.5 Legacy Unlock Schedule (design-godot.md §3, "The Legacy Unlock
    /// Schedule"; Package 11 A5). The hero's kit is <b>earned across Act I</b>
    /// rather than handed over at Level 0:
    ///
    /// <list type="table">
    /// <item><term>Level 0 (calibration)</term><description>basic three-hit
    /// string, Block, Rally, the death rewind, and the Ultimate Meter <i>with</i>
    /// Defy — none of which this class gates, because they are never locked.
    /// Level 0 stays basics-first: the hero plays it with no Special, and
    /// <i>completing</i> it restores Special 1 (the results beat shows it).</description></item>
    /// <item><term>Complete Level 0</term><description>Special 1 (playable from Level 1)</description></item>
    /// <item><term>Complete Level 1</term><description>Movement Ability</description></item>
    /// <item><term>Complete Level 2</term><description>Special 2</description></item>
    /// <item><term>Complete Level 3</term><description>Ultimate <i>cast</i> — the
    /// meter itself exists from Level 0 so Defy and Level 4's dampening
    /// beams stay meaningful. Level 4 is the first full-kit level.</description></item>
    /// </list>
    ///
    /// <para>The schedule moved one level earlier on 2026-11-03 (gameplay review,
    /// ruling 3b+; ledger <c>USER-LEGACY-SCHEDULE-SHIFT</c>). <b>No save derivation
    /// is needed:</b> the persisted keys (<c>movement</c> / <c>special1</c> /
    /// <c>special2</c> / <c>ultimate</c>) keep their meaning and are unioned with
    /// the completed-level history in <see cref="UnlockedSlotsFor"/>, so a save
    /// only ever gains a slot, never loses one.</para>
    ///
    /// <para><b>Milestone-gated, never dust-gated.</b> The 975-dust Resonance
    /// economy is untouched; this schedule and the grid are orthogonal (a grid
    /// node that modifies a locked ability renders dormant — A4's side of the
    /// handshake calls <c>IsUnlocked</c> here).</para>
    ///
    /// <para><b>Story-only.</b> Nothing in <c>scripts/FighterSim/</c> may consult
    /// this type. Fighter Mode, the hub Holodeck, the Calibration Drills and the
    /// Mirror Paradox clone all run full normalized kits — they travel the
    /// <c>CharacterFactory.CreateCharacter(..., applyStoryProgression: false)</c>
    /// seam, which never installs the locks.</para>
    ///
    /// <para>Engine-free on purpose: the schedule is a pure table so it can be
    /// unit-tested without a scene and consumed from the save layer, the factory
    /// and the UI alike.</para>
    /// </summary>
    public static class LegacyUnlockSchedule {

        /// <summary>The four gated slots, in unlock order.</summary>
        public static readonly IReadOnlyList<AbilitySlot> GatedSlots = new[] {
            AbilitySlot.Special1,
            AbilitySlot.MovementAbility,
            AbilitySlot.Special2,
            AbilitySlot.Ultimate
        };

        /// <summary>
        /// What <see cref="MilestoneLevelFor"/> returns for a slot the schedule does
        /// not gate. A <b>negative</b> sentinel, because Level 0's completion is a
        /// real milestone (Special 1) and 0 can no longer mean "ungated".
        /// <see cref="LevelNumberOf"/> also returns -1 for an unrecognized ID, but
        /// it is only ever compared against a gated slot's milestone (0 or more),
        /// so a stray or retired ID can never grant anything.
        /// </summary>
        public const int NotGated = -1;

        /// <summary>
        /// Persisted slot keys (design-godot.md and plan §2.6: <c>movement</c> /
        /// <c>special1</c> / <c>special2</c> / <c>ultimate</c>). These strings go
        /// into <c>StorySaveData.UnlockedLegacyAbilities</c>, so they are a
        /// serialization contract — never rename one.
        /// </summary>
        public const string MovementKey = "movement";
        public const string SpecialOneKey = "special1";
        public const string SpecialTwoKey = "special2";
        public const string UltimateKey = "ultimate";

        /// <summary>
        /// Campaign level whose completion grants each slot, or <see cref="NotGated"/>
        /// (-1) for a slot the schedule never gates. Level numbers, not
        /// level IDs: the era suffix in <c>level_02_orleans</c> is content, the
        /// <c>02</c> is the milestone. Special 1 is <b>0</b>: it is restored by
        /// completing the Level 0 calibration.
        /// </summary>
        public static int MilestoneLevelFor(AbilitySlot slot) => slot switch {
            AbilitySlot.Special1 => 0,
            AbilitySlot.MovementAbility => 1,
            AbilitySlot.Special2 => 2,
            AbilitySlot.Ultimate => 3,
            _ => NotGated
        };

        /// <summary>The persisted key for a gated slot, or "" for an ungated one.</summary>
        public static string SlotKey(AbilitySlot slot) => slot switch {
            AbilitySlot.MovementAbility => MovementKey,
            AbilitySlot.Special1 => SpecialOneKey,
            AbilitySlot.Special2 => SpecialTwoKey,
            AbilitySlot.Ultimate => UltimateKey,
            _ => ""
        };

        /// <summary>Inverse of <see cref="SlotKey"/>. False for an unknown key.</summary>
        public static bool TryParseSlotKey(string key, out AbilitySlot slot) {
            switch (key) {
                case MovementKey: slot = AbilitySlot.MovementAbility; return true;
                case SpecialOneKey: slot = AbilitySlot.Special1; return true;
                case SpecialTwoKey: slot = AbilitySlot.Special2; return true;
                case UltimateKey: slot = AbilitySlot.Ultimate; return true;
                default: slot = AbilitySlot.Special1; return false;
            }
        }

        /// <summary>
        /// The slot a completed level ID grants, if any. Matches on the
        /// <c>level_NN</c> prefix rather than the whole ID, so the era suffix
        /// (<c>_orleans</c>, <c>_chicago</c>) is free to change without touching
        /// the schedule. Level 4 is the first FULL-kit level, entered after the
        /// Ultimate is granted by Level 3's completion (S27 retired Level 4A). A
        /// retired <c>level_04a_&lt;hero&gt;</c> ID an old save still carries grants
        /// nothing.
        /// </summary>
        public static bool TryGrantedSlotForLevel(string levelID, out AbilitySlot slot) {
            slot = AbilitySlot.Special1;
            int levelNumber = LevelNumberOf(levelID);
            if (levelNumber < 0) return false;
            foreach (AbilitySlot candidate in GatedSlots) {
                if (MilestoneLevelFor(candidate) != levelNumber) continue;
                slot = candidate;
                return true;
            }
            return false;
        }

        /// <summary>
        /// True when <paramref name="slot"/> is available given the levels this
        /// save has completed. Ungated slots (<see cref="NotGated"/>, anything not in
        /// <see cref="GatedSlots"/>) are always true.
        /// </summary>
        public static bool IsUnlocked(AbilitySlot slot, IReadOnlyCollection<string> completedLevels) {
            int milestone = MilestoneLevelFor(slot);
            if (milestone < 0) return true;
            if (completedLevels == null) return false;
            foreach (string levelID in completedLevels) {
                if (LevelNumberOf(levelID) == milestone) return true;
            }
            return false;
        }

        /// <summary>
        /// The slots a Story save has restored for <paramref name="characterID"/>:
        /// its persisted <c>UnlockedLegacyAbilities</c> keys, backstopped by its
        /// completed-level history (a payload written before the schedule existed
        /// carries only the history, and the milestone grant reads the same
        /// source, so the two can never disagree). The single statement of the
        /// rule behind both the campaign hero's ability gate
        /// (<c>CharacterFactory.ResolveStoryUnlockedSlots</c>) and the Resonance
        /// grid's dormant nodes (<c>ResonanceProgression.IsAbilityUnlocked</c>).
        /// A null save restores nothing. Engine-free.
        /// </summary>
        public static HashSet<AbilitySlot> UnlockedSlotsFor(StorySaveData save, string characterID) {
            var slots = new HashSet<AbilitySlot>();
            if (save == null) return slots;
            if (characterID != null
                && save.UnlockedLegacyAbilities != null
                && save.UnlockedLegacyAbilities.TryGetValue(characterID, out List<string> keys)) {
                slots.UnionWith(SlotsFromSavedKeys(keys));
            }
            foreach (AbilitySlot slot in GatedSlots) {
                if (IsUnlocked(slot, save.CompletedLevels)) slots.Add(slot);
            }
            return slots;
        }

        /// <summary>
        /// True when <paramref name="slot"/> is available to
        /// <paramref name="characterID"/> on this save (see
        /// <see cref="UnlockedSlotsFor"/>). Ungated slots are always true.
        /// </summary>
        public static bool IsUnlockedFor(StorySaveData save, string characterID, AbilitySlot slot) {
            if (MilestoneLevelFor(slot) < 0) return true;
            return UnlockedSlotsFor(save, characterID).Contains(slot);
        }

        /// <summary>Every slot unlocked by this completed-level set, as persisted keys.</summary>
        public static List<string> UnlockedKeysFor(IReadOnlyCollection<string> completedLevels) {
            var keys = new List<string>(GatedSlots.Count);
            foreach (AbilitySlot slot in GatedSlots) {
                if (IsUnlocked(slot, completedLevels)) keys.Add(SlotKey(slot));
            }
            return keys;
        }

        /// <summary>
        /// Reads a persisted per-character unlock list into a slot set. A missing
        /// character entry means "nothing unlocked yet", which is the correct
        /// reading for a fresh campaign.
        /// </summary>
        public static HashSet<AbilitySlot> SlotsFromSavedKeys(IEnumerable<string> savedKeys) {
            var slots = new HashSet<AbilitySlot>();
            if (savedKeys == null) return slots;
            foreach (string key in savedKeys) {
                if (TryParseSlotKey(key, out AbilitySlot slot)) slots.Add(slot);
            }
            return slots;
        }

        /// <summary>
        /// The <c>NN</c> of a <c>level_NN[...]</c> ID, or -1. Deliberately
        /// tolerant: an unrecognized ID simply grants nothing.
        /// </summary>
        public static int LevelNumberOf(string levelID) {
            if (string.IsNullOrWhiteSpace(levelID)) return -1;
            const string prefix = "level_";
            if (!levelID.StartsWith(prefix, StringComparison.Ordinal)) return -1;
            if (levelID.Length < prefix.Length + 2) return -1;
            char tens = levelID[prefix.Length];
            char ones = levelID[prefix.Length + 1];
            if (!char.IsDigit(tens) || !char.IsDigit(ones)) return -1;
            // "level_04a_einstein" is the retired Level 4A (S27), which never
            // granted a milestone — a trailing letter disqualifies the match, so
            // the dead ID an old save carries in CompletedLevels stays inert.
            int next = prefix.Length + 2;
            if (next < levelID.Length && char.IsLetter(levelID[next])) return -1;
            return (tens - '0') * 10 + (ones - '0');
        }
    }
}
