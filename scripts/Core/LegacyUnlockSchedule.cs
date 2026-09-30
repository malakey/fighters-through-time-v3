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
    /// string, Block, Rally, the death rewind, Time Freeze, and the Ultimate
    /// Meter <i>with</i> Defy History — none of which this class gates, because
    /// they are never locked.</description></item>
    /// <item><term>Complete Level 1</term><description>Movement Ability</description></item>
    /// <item><term>Complete Level 2</term><description>Special 1</description></item>
    /// <item><term>Complete Level 3</term><description>Special 2</description></item>
    /// <item><term>Complete Level 4</term><description>Ultimate <i>cast</i> — the
    /// meter itself exists from Level 0 so Defy History and Level 4's dampening
    /// beams stay meaningful.</description></item>
    /// </list>
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
            AbilitySlot.MovementAbility,
            AbilitySlot.Special1,
            AbilitySlot.Special2,
            AbilitySlot.Ultimate
        };

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
        /// Campaign level whose completion grants each slot. Level numbers, not
        /// level IDs: the era suffix in <c>level_02_orleans</c> is content, the
        /// <c>02</c> is the milestone.
        /// </summary>
        public static int MilestoneLevelFor(AbilitySlot slot) => slot switch {
            AbilitySlot.MovementAbility => 1,
            AbilitySlot.Special1 => 2,
            AbilitySlot.Special2 => 3,
            AbilitySlot.Ultimate => 4,
            _ => 0
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
        /// the schedule. Level 5 is the first FULL-kit level (S27 retired Level
        /// 4A), entered after the Ultimate is granted by Level 4's completion. A
        /// retired <c>level_04a_&lt;hero&gt;</c> ID an old save still carries grants
        /// nothing.
        /// </summary>
        public static bool TryGrantedSlotForLevel(string levelID, out AbilitySlot slot) {
            slot = AbilitySlot.Special1;
            int levelNumber = LevelNumberOf(levelID);
            if (levelNumber <= 0) return false;
            foreach (AbilitySlot candidate in GatedSlots) {
                if (MilestoneLevelFor(candidate) != levelNumber) continue;
                slot = candidate;
                return true;
            }
            return false;
        }

        /// <summary>
        /// True when <paramref name="slot"/> is available given the levels this
        /// save has completed. Ungated slots (anything not in
        /// <see cref="GatedSlots"/>) are always true.
        /// </summary>
        public static bool IsUnlocked(AbilitySlot slot, IReadOnlyCollection<string> completedLevels) {
            int milestone = MilestoneLevelFor(slot);
            if (milestone <= 0) return true;
            if (completedLevels == null) return false;
            foreach (string levelID in completedLevels) {
                if (LevelNumberOf(levelID) == milestone) return true;
            }
            return false;
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
