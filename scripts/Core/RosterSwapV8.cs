using System;
using System.Collections.Generic;

namespace FTT.Core {

    /// <summary>
    /// <b>Package 13 W5 — the declared v7 → v8 D4 derivation</b> for the roster
    /// swap Pocahontas → Harriet Tubman. Phase C composes it into the single
    /// v7 → v8 step (plan D8); this class only declares it and never bumps
    /// <c>SaveSchemaMigrator.CurrentVersion</c>.
    ///
    /// <para><b>Pure and engine-free, deliberately.</b> Save derivations run inside
    /// pure-C# GdUnit suites with no Godot runtime (CLAUDE.md failure signature 7),
    /// so nothing here loads a grid, reads the manifest or touches
    /// <c>CharacterRoster</c>. The retired Pocahontas grid prices are therefore
    /// embedded in <see cref="LegacyNodeCosts"/> rather than read from
    /// <c>resources/Resonance/pocahontas_grid.tres</c> (which W5 retires) or from
    /// <c>FTT.Environment.ResonanceProgression.RetiredNodeCosts</c> (whose class
    /// also loads grids, so touching its static state is not guaranteed
    /// engine-free).</para>
    ///
    /// <para>It lives in its own class rather than on <c>SaveSchemaMigrator</c> so
    /// that Phase C, which owns that class's v8 step, is the only editor of it —
    /// the same split W2 used for <c>StoryAttemptState.RetireLegacyLevelV8</c>.</para>
    /// </summary>
    public static class RosterSwapV8 {

        /// <summary>The retired roster ID. Stays readable as dead data where D4 says so.</summary>
        public const string LegacyCharacterID = "pocahontas";

        /// <summary>The roster ID that inherits a Pocahontas save's progression.</summary>
        public const string ReplacementCharacterID = "tubman";

        /// <summary>
        /// The price every retired Pocahontas grid node was sold at, keyed by node
        /// ID (ordinal). The nine V7.6 nodes (tier 1 = 50, tier 2 = 75, tier 3 =
        /// 200; 975 for the full grid) are the exact IDs and <c>UnlockCost</c>s of
        /// <c>pocahontas_grid.tres</c> at the W5 branch point; the nine pre-V7.6
        /// <c>fs/wr/pw</c> branch IDs match <c>ResonanceProgression.RetiredNodeCosts</c>
        /// and may still linger on a save whose v6 grid migration never ran for
        /// them. Any other ID (a hand-edited or unknown entry) refunds 0.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, int> LegacyNodeCosts =
            new Dictionary<string, int>(StringComparer.Ordinal) {
                // V7.6 grid (Package 11 A4).
                ["pocahontas_windstep"] = 50,
                ["pocahontas_staff_edge"] = 50,
                ["pocahontas_forest_vigor"] = 50,
                ["pocahontas_glide_duration"] = 75,
                ["pocahontas_snare_duration"] = 75,
                ["pocahontas_second_glide"] = 75,
                ["pocahontas_tornado_lift"] = 200,
                ["pocahontas_thorn_snare"] = 200,
                ["pocahontas_leaf_barrier"] = 200,
                // Pre-V7.6 linear branches (retired by A4, commit 31fed14).
                ["pocahontas_fs1"] = 50, ["pocahontas_fs2"] = 75, ["pocahontas_fs3"] = 200,
                ["pocahontas_wr1"] = 50, ["pocahontas_wr2"] = 75, ["pocahontas_wr3"] = 200,
                ["pocahontas_pw1"] = 50, ["pocahontas_pw2"] = 75, ["pocahontas_pw3"] = 200,
            };

        /// <summary>True when <paramref name="characterID"/> names the retired hero (trimmed, case-insensitive).</summary>
        public static bool IsLegacyCharacter(string characterID) =>
            characterID != null
            && string.Equals(characterID.Trim(), LegacyCharacterID, StringComparison.OrdinalIgnoreCase);

        /// <summary>The refund one retired node earns: its recorded price, or 0 when unknown.</summary>
        public static int RefundFor(string nodeID) =>
            nodeID != null && LegacyNodeCosts.TryGetValue(nodeID.Trim(), out int cost) ? cost : 0;

        /// <summary>
        /// Moves a story payload's Pocahontas progression to Tubman (plan D4).
        ///
        /// <list type="bullet">
        /// <item><b>Selection.</b> A <see cref="StorySaveData.SelectedCharacterID"/>
        /// of <c>pocahontas</c> (trimmed, case-insensitive) becomes <c>tubman</c>.
        /// Any other selection is left alone.</item>
        /// <item><b>Grid → refund.</b> Every node in every <c>pocahontas</c>
        /// <see cref="StorySaveData.GridProgress"/> entry is refunded at its
        /// <see cref="LegacyNodeCosts"/> price and the entry is removed. Tubman
        /// starts with an empty grid: no node is carried over or granted free.</item>
        /// <item><b>Deposited dust.</b> The Pocahontas
        /// <see cref="StorySaveData.DepositedChronalDust"/> balance plus the grid
        /// refund is added (checked arithmetic) to any existing Tubman balance,
        /// and the <c>pocahontas</c> key is removed. The undeposited
        /// <see cref="StorySaveData.LevelChronalDust"/> wallet is not touched: it
        /// belongs to the open attempt, which follows the selection.</item>
        /// <item><b>Legacy unlocks.</b> <see cref="StorySaveData.UnlockedLegacyAbilities"/>
        /// is keyed by character, so the Pocahontas slot keys are unioned into
        /// Tubman's list (order kept, no duplicates) and the <c>pocahontas</c> key
        /// is removed. The slot keys are hero-agnostic (<c>movement</c> /
        /// <c>special1</c> / <c>special2</c> / <c>ultimate</c>), so they carry
        /// meaning unchanged.</item>
        /// <item><b>Migrated regardless of selection.</b> A stray Pocahontas grid,
        /// dust or unlock entry on a save locked to another hero is migrated the
        /// same way, because after the swap no code path can ever read or spend a
        /// <c>pocahontas</c> key again; the dust would otherwise be stranded.</item>
        /// <item><b>Dead data (D4), deliberately untouched:</b>
        /// <see cref="StorySaveData.ViewedDialogueIDs"/> /
        /// <see cref="StorySaveData.LastViewedDialogueID"/> (including any
        /// <c>@pocahontas</c> variant IDs), and every level-keyed dictionary
        /// (<see cref="StorySaveData.IntegrityByLevel"/>, <c>RatingByLevel</c>,
        /// <c>SecretsFoundByLevel</c>, <c>FontUsesConsumed</c>) and
        /// <see cref="StorySaveData.CompletedLevels"/> — none is keyed by hero.
        /// A retired <c>level_04a_pocahontas</c> park is W2's D2 derivation, not
        /// this one; the two commute (D2 banks to the selected hero, and this
        /// derivation moves a <c>pocahontas</c> balance to Tubman either way).</item>
        /// </list>
        ///
        /// <para>Idempotent: after the first call no <c>pocahontas</c> key or
        /// selection remains, so a second call changes nothing and returns 0.
        /// Null-safe on a null save and on null collections.</para>
        /// </summary>
        /// <returns>The dust refunded for retired grid nodes only (not the moved
        /// deposited balance). 0 when there was no Pocahontas grid.</returns>
        public static int MigrateRosterSwapV8(StorySaveData save) {
            if (save == null) return 0;

            if (IsLegacyCharacter(save.SelectedCharacterID)) {
                save.SelectedCharacterID = ReplacementCharacterID;
            }

            int refund = 0;
            if (save.GridProgress != null) {
                foreach (string key in LegacyKeys(save.GridProgress.Keys)) {
                    List<string> nodes = save.GridProgress[key];
                    if (nodes != null) {
                        foreach (string nodeID in nodes) refund = checked(refund + RefundFor(nodeID));
                    }
                    save.GridProgress.Remove(key);
                }
            }

            int moved = 0;
            List<string> dustKeys = save.DepositedChronalDust == null
                ? new List<string>()
                : LegacyKeys(save.DepositedChronalDust.Keys);
            foreach (string key in dustKeys) {
                moved = checked(moved + Math.Max(0, save.DepositedChronalDust[key]));
                save.DepositedChronalDust.Remove(key);
            }
            int credit = checked(moved + refund);
            if (credit > 0 || dustKeys.Count > 0) {
                save.DepositedChronalDust ??= new Dictionary<string, int>();
                int balance = save.DepositedChronalDust.TryGetValue(ReplacementCharacterID, out int existing)
                    ? existing : 0;
                save.DepositedChronalDust[ReplacementCharacterID] = checked(balance + credit);
            }

            if (save.UnlockedLegacyAbilities != null) {
                List<string> legacyKeys = LegacyKeys(save.UnlockedLegacyAbilities.Keys);
                if (legacyKeys.Count > 0) {
                    if (!save.UnlockedLegacyAbilities.TryGetValue(ReplacementCharacterID, out List<string> target)
                        || target == null) {
                        target = new List<string>();
                    }
                    foreach (string key in legacyKeys) {
                        List<string> slots = save.UnlockedLegacyAbilities[key];
                        if (slots != null) {
                            foreach (string slot in slots) {
                                if (!string.IsNullOrEmpty(slot) && !target.Contains(slot)) target.Add(slot);
                            }
                        }
                        save.UnlockedLegacyAbilities.Remove(key);
                    }
                    save.UnlockedLegacyAbilities[ReplacementCharacterID] = target;
                }
            }

            return refund;
        }

        /// <summary>
        /// The global half of D4. Adds <c>tubman</c> to
        /// <see cref="GlobalSaveData.UnlockedCharacters"/> when the list is
        /// non-empty (a payload that has already been through a roster fill, with
        /// or without <c>pocahontas</c>) and lacks it. An empty or null list is left
        /// alone: that is a fresh payload which <c>SaveManager.EnsureRosterUnlocked</c>
        /// fills from the manifest at load anyway.
        ///
        /// <para><c>EnsureRosterUnlocked</c> already grants every manifest
        /// <c>Character</c> row at runtime, so once Tubman's row exists this helper
        /// is belt-and-braces; it exists so Phase C's v8 step can state the grant
        /// explicitly without the engine. The <c>pocahontas</c> entry, and
        /// <see cref="GlobalSaveData.CharacterWins"/> / <see cref="GlobalSaveData.CharacterLosses"/>
        /// and <see cref="GlobalSaveData.SeenDialogueIDs"/>, stay as dead data (D4).</para>
        /// </summary>
        /// <returns>True when <c>tubman</c> was added; false otherwise (idempotent).</returns>
        public static bool EnsureReplacementUnlocked(GlobalSaveData global) {
            List<string> unlocked = global?.UnlockedCharacters;
            if (unlocked == null || unlocked.Count == 0) return false;
            foreach (string id in unlocked) {
                if (id != null && string.Equals(id.Trim(), ReplacementCharacterID, StringComparison.OrdinalIgnoreCase)) {
                    return false;
                }
            }
            unlocked.Add(ReplacementCharacterID);
            return true;
        }

        private static List<string> LegacyKeys(IEnumerable<string> keys) {
            var result = new List<string>();
            foreach (string key in keys) {
                if (IsLegacyCharacter(key)) result.Add(key);
            }
            return result;
        }
    }
}
