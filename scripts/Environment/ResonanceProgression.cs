using System;
using System.Collections.Generic;
using FTT.Core;
using Godot;

namespace FTT.Environment {

    /// <summary>Story-only currency, unlock, and stat resolution boundary.</summary>
    public static class ResonanceProgression {

        public const string GridResourceDirectory = "res://resources/Resonance";

        /// <summary>
        /// V7.6 dormant-node gate. A5's Legacy Unlock Schedule installs the
        /// real probe — <c>(characterID, abilitySlot) =&gt; bool</c>. Until it
        /// merges the probe is null and every gated node behaves as unlocked,
        /// which is the pre-V7.6 behaviour.
        /// TODO(A5): wire this to the Legacy Unlock Schedule's
        /// <c>IsAbilityUnlocked</c> at campaign load.
        /// </summary>
        public static Func<string, string, bool> AbilityUnlockProbe;

        /// <summary>
        /// True when a node's gated ability slot is available in the current
        /// Story context. Ungated nodes and an uninstalled probe both pass.
        /// </summary>
        public static bool IsAbilityUnlocked(string characterID, string abilitySlot) {
            if (string.IsNullOrWhiteSpace(abilitySlot)) return true;
            Func<string, string, bool> probe = AbilityUnlockProbe;
            return probe == null || probe(characterID, abilitySlot);
        }

        /// <summary>
        /// The one place a Resonance grid is loaded. Goes through
        /// <see cref="AuthoredResources"/> so the grid and its nine scripted
        /// <c>ResonanceNodeData</c> sub-resources are marshalled once per process
        /// and never torn down - never <c>ResourceLoader.Load</c> a grid directly.
        /// Authored grids are immutable at runtime, so the shared instance is also
        /// the semantically correct thing to hand out.
        /// </summary>
        public static ResonanceGridData LoadGrid(string characterID) {
            if (string.IsNullOrWhiteSpace(characterID)) return null;
            return AuthoredResources.Load<ResonanceGridData>(
                $"{GridResourceDirectory}/{characterID}_grid.tres");
        }

        public static ResonanceUnlockResult TryUnlock(
            ResonanceGridData grid,
            StorySaveData save,
            string nodeID) {
            ResonanceUnlockResult evaluation = EvaluateUnlock(grid, save, nodeID);
            if (evaluation != ResonanceUnlockResult.Unlocked) return evaluation;
            ResonanceNodeData node = FindNode(grid, nodeID);
            int balance = save.DepositedChronalDust.GetValueOrDefault(grid.CharacterID);
            save.DepositedChronalDust[grid.CharacterID] = balance - node.UnlockCost;
            GetUnlockedNodes(save, grid.CharacterID).Add(node.NodeID);
            EventBus.Instance?.RaiseTalentNodeUnlocked(node.NodeID);
            return ResonanceUnlockResult.Unlocked;
        }

        /// <summary>
        /// Reports what <see cref="TryUnlock"/> would return for a node without
        /// mutating the save. <see cref="ResonanceUnlockResult.Unlocked"/> means
        /// the node is currently purchasable. Used by the grid UI for
        /// disabled-state explanations and tooltips.
        /// </summary>
        public static ResonanceUnlockResult EvaluateUnlock(
            ResonanceGridData grid,
            StorySaveData save,
            string nodeID) {
            if (grid == null || save == null || grid.CharacterID != save.SelectedCharacterID) {
                return ResonanceUnlockResult.WrongCharacter;
            }
            ResonanceNodeData node = FindNode(grid, nodeID);
            if (node == null) return ResonanceUnlockResult.MissingNode;
            List<string> unlocked = GetUnlockedNodes(save, grid.CharacterID);
            if (unlocked.Contains(node.NodeID)) return ResonanceUnlockResult.AlreadyUnlocked;
            if (!IsAbilityUnlocked(grid.CharacterID, node.GatedAbilityID)) {
                return ResonanceUnlockResult.AbilityLocked;
            }
            if (!PrerequisitesSatisfied(node, unlocked)) {
                return ResonanceUnlockResult.MissingPrerequisite;
            }
            int balance = save.DepositedChronalDust.GetValueOrDefault(grid.CharacterID);
            if (balance < node.UnlockCost) return ResonanceUnlockResult.InsufficientDust;
            return ResonanceUnlockResult.Unlocked;
        }

        /// <summary>
        /// V7.6 prerequisite satisfaction. An EMPTY list is root-eligible under
        /// either mode. <c>All</c> needs every listed node; <c>Any</c> needs at
        /// least one. The single shared evaluator behind UI availability,
        /// purchase, respec dependency checks and save validation.
        /// </summary>
        public static bool PrerequisitesSatisfied(ResonanceNodeData node, IReadOnlyList<string> unlocked) {
            string[] prerequisites = node?.PrerequisiteNodeIDs ?? Array.Empty<string>();
            if (prerequisites.Length == 0) return true;
            if (node.PrerequisiteMode == PrerequisiteMode.Any) {
                foreach (string prerequisite in prerequisites) {
                    if (Contains(unlocked, prerequisite)) return true;
                }
                return false;
            }
            foreach (string prerequisite in prerequisites) {
                if (!Contains(unlocked, prerequisite)) return false;
            }
            return true;
        }

        private static bool Contains(IReadOnlyList<string> unlocked, string nodeID) {
            if (unlocked == null) return false;
            for (int i = 0; i < unlocked.Count; i++) {
                if (string.Equals(unlocked[i], nodeID, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>
        /// V7.6 authored-data validation: rejects dangling prerequisite IDs,
        /// self-references, duplicate node IDs, prerequisite cycles, scoped
        /// stat keys with no <c>AbilityScope</c> (and vice versa), unknown stat
        /// keys, and unknown gate slot names. Returns an empty list for a valid
        /// grid. Pure data — no Godot scene dependency — so both the content
        /// tests and a future editor tool can call it.
        /// </summary>
        public static List<string> ValidateGrid(ResonanceGridData grid) {
            var issues = new List<string>();
            if (grid == null) {
                issues.Add("grid is null");
                return issues;
            }
            ResonanceNodeData[] nodes = grid.Nodes ?? Array.Empty<ResonanceNodeData>();
            var byID = new Dictionary<string, ResonanceNodeData>(StringComparer.Ordinal);
            foreach (ResonanceNodeData node in nodes) {
                if (node == null) {
                    issues.Add($"{grid.GridID}: null node entry");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(node.NodeID)) {
                    issues.Add($"{grid.GridID}: node with an empty NodeID");
                    continue;
                }
                if (byID.ContainsKey(node.NodeID)) {
                    issues.Add($"{grid.GridID}: duplicate NodeID '{node.NodeID}'");
                    continue;
                }
                byID[node.NodeID] = node;
            }
            foreach (ResonanceNodeData node in byID.Values) {
                foreach (string prerequisite in node.PrerequisiteNodeIDs ?? Array.Empty<string>()) {
                    if (string.Equals(prerequisite, node.NodeID, StringComparison.Ordinal)) {
                        issues.Add($"{node.NodeID}: lists itself as a prerequisite");
                    } else if (!byID.ContainsKey(prerequisite)) {
                        issues.Add($"{node.NodeID}: dangling prerequisite '{prerequisite}'");
                    }
                }
                bool scoped = Array.IndexOf(ResonanceStatKeys.ScopedKeys, node.StatModifierKey) >= 0;
                if (scoped && string.IsNullOrWhiteSpace(node.AbilityScope)) {
                    issues.Add($"{node.NodeID}: scoped key '{node.StatModifierKey}' with no AbilityScope");
                }
                if (!string.IsNullOrWhiteSpace(node.AbilityScope)
                    && Array.IndexOf(ResonanceStatKeys.ScopableKeys, node.StatModifierKey) < 0) {
                    issues.Add($"{node.NodeID}: AbilityScope '{node.AbilityScope}' on unscopable key '{node.StatModifierKey}'");
                }
                if (!string.IsNullOrWhiteSpace(node.StatModifierKey)
                    && Array.IndexOf(ResonanceStatKeys.All, node.StatModifierKey) < 0) {
                    issues.Add($"{node.NodeID}: unknown StatModifierKey '{node.StatModifierKey}'");
                }
                if (!string.IsNullOrWhiteSpace(node.GatedAbilityID)
                    && Array.IndexOf(ResonanceAbilitySlots.All, node.GatedAbilityID) < 0) {
                    issues.Add($"{node.NodeID}: unknown GatedAbilityID '{node.GatedAbilityID}'");
                }
            }
            foreach (string nodeID in byID.Keys) {
                if (HasCycle(nodeID, byID, new HashSet<string>(StringComparer.Ordinal),
                        new HashSet<string>(StringComparer.Ordinal))) {
                    issues.Add($"{grid.GridID}: prerequisite cycle reachable from '{nodeID}'");
                    break;
                }
            }
            return issues;
        }

        private static bool HasCycle(
            string nodeID,
            Dictionary<string, ResonanceNodeData> byID,
            HashSet<string> onStack,
            HashSet<string> settled) {
            if (settled.Contains(nodeID)) return false;
            if (!onStack.Add(nodeID)) return true;
            if (byID.TryGetValue(nodeID, out ResonanceNodeData node)) {
                foreach (string prerequisite in node.PrerequisiteNodeIDs ?? Array.Empty<string>()) {
                    if (byID.ContainsKey(prerequisite) && HasCycle(prerequisite, byID, onStack, settled)) {
                        return true;
                    }
                }
            }
            onStack.Remove(nodeID);
            settled.Add(nodeID);
            return false;
        }

        /// <summary>
        /// V7.3 free respec (design "Respec — free, always available"): the sum
        /// of every unlocked node's cost on the active character's grid — what
        /// a respec would refund. Zero for a wrong-character pairing.
        /// </summary>
        public static int CalculateSpentDust(ResonanceGridData grid, StorySaveData save) {
            if (grid == null || save == null || grid.CharacterID != save.SelectedCharacterID) return 0;
            List<string> unlocked = GetUnlockedNodes(save, grid.CharacterID);
            int spent = 0;
            foreach (ResonanceNodeData node in grid.Nodes ?? Array.Empty<ResonanceNodeData>()) {
                if (node != null && unlocked.Contains(node.NodeID)) spent += node.UnlockCost;
            }
            return spent;
        }

        /// <summary>
        /// V7.3 free respec: refunds EVERY Chronal Dust point spent on the
        /// active character's grid back into their Repository pool and clears
        /// all unlocked nodes. No fee, no cooldown. Returns the refunded
        /// amount — zero for a wrong-character pairing, an empty grid, or a
        /// second respec in a row.
        /// </summary>
        public static int RespecAll(ResonanceGridData grid, StorySaveData save) {
            int refund = CalculateSpentDust(grid, save);
            if (grid == null || save == null || grid.CharacterID != save.SelectedCharacterID) return 0;
            List<string> unlocked = GetUnlockedNodes(save, grid.CharacterID);
            if (unlocked.Count == 0) return 0;
            int balance = save.DepositedChronalDust.GetValueOrDefault(grid.CharacterID);
            save.DepositedChronalDust[grid.CharacterID] = checked(balance + refund);
            unlocked.Clear();
            return refund;
        }

        /// <summary>
        /// The V7.6 retired node IDs and the price each was sold at, keyed by
        /// character. Every pre-V7.6 grid was three linear branches of three at
        /// 50 / 75 / 200; these are the exact IDs the shipped `.tres` files
        /// carried at commit 31fed14. Used only by
        /// <see cref="MigrateGridProgressToV76"/> to refund a purchase whose
        /// node no longer exists.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, int> RetiredNodeCosts =
            BuildRetiredNodeCosts();

        private static Dictionary<string, int> BuildRetiredNodeCosts() {
            var table = new Dictionary<string, int>(StringComparer.Ordinal);
            void Branch(string character, string branch) {
                table[$"{character}_{branch}1"] = 50;
                table[$"{character}_{branch}2"] = 75;
                table[$"{character}_{branch}3"] = 200;
            }
            Branch("einstein", "u"); Branch("einstein", "o"); Branch("einstein", "d");
            Branch("joan", "r"); Branch("joan", "m"); Branch("joan", "d");
            Branch("leonardo", "a"); Branch("leonardo", "e"); Branch("leonardo", "m");
            Branch("tesla", "c"); Branch("tesla", "p"); Branch("tesla", "w");
            Branch("shakespeare", "t"); Branch("shakespeare", "c"); Branch("shakespeare", "h");
            Branch("mozart", "a"); Branch("mozart", "f"); Branch("mozart", "p");
            Branch("cleopatra", "al"); Branch("cleopatra", "dm"); Branch("cleopatra", "pw");
            Branch("lincoln", "l"); Branch("lincoln", "s"); Branch("lincoln", "r");
            Branch("pocahontas", "fs"); Branch("pocahontas", "wr"); Branch("pocahontas", "pw");
            return table;
        }

        /// <summary>
        /// F08's one-time, atomic, versioned free respec, generalized. Every
        /// V7.6 grid re-authored every node ID, so a loaded save can carry
        /// purchases that no longer name a node: on load, DROP any
        /// <c>GridProgress</c> entry that is not present in that character's
        /// current grid and REFUND it at its recorded retired price into that
        /// character's deposited balance.
        ///
        /// <para>This is strictly stronger than a Shakespeare-only Act II
        /// migration and covers all nine characters. It removes purchased
        /// nodes and their effects, refunds exactly the recorded paid cost
        /// once, preserves campaign progress and undeposited earnings, and
        /// never grants a missing node for free. Idempotent — a second call
        /// finds nothing to drop and refunds zero.</para>
        ///
        /// <para>A4 ships the function; the Phase C closeout calls it from the
        /// single v5 to v6 migration step (plan §2.6).</para>
        /// </summary>
        /// <returns>The total dust refunded across every character on the save.</returns>
        public static int MigrateGridProgressToV76(StorySaveData save) {
            if (save?.GridProgress == null) return 0;
            int refunded = 0;
            foreach (string characterID in new List<string>(save.GridProgress.Keys)) {
                List<string> unlocked = save.GridProgress[characterID];
                if (unlocked == null || unlocked.Count == 0) continue;
                ResonanceGridData grid = LoadGrid(characterID);
                if (grid == null) continue;
                var surviving = new List<string>(unlocked.Count);
                int characterRefund = 0;
                foreach (string nodeID in unlocked) {
                    if (FindNode(grid, nodeID) != null) {
                        surviving.Add(nodeID);
                        continue;
                    }
                    characterRefund += RetiredNodeCosts.GetValueOrDefault(nodeID, 0);
                }
                if (surviving.Count == unlocked.Count) continue;
                save.GridProgress[characterID] = surviving;
                if (characterRefund > 0) {
                    int balance = save.DepositedChronalDust.GetValueOrDefault(characterID);
                    save.DepositedChronalDust[characterID] = checked(balance + characterRefund);
                    refunded += characterRefund;
                }
            }
            return refunded;
        }

        public static int DepositActiveDust(StorySaveData save, string characterID, int carriedDust) {
            if (save == null || characterID != save.SelectedCharacterID || carriedDust <= 0) return 0;
            int balance = save.DepositedChronalDust.GetValueOrDefault(characterID);
            save.DepositedChronalDust[characterID] = checked(balance + carriedDust);
            return carriedDust;
        }

        public static StoryStatProfile Resolve(ResonanceGridData grid, StorySaveData save) {
            if (grid == null || save == null || grid.CharacterID != save.SelectedCharacterID) {
                return StoryStatProfile.Default;
            }
            List<string> unlocked = GetUnlockedNodes(save, grid.CharacterID);
            int maxHP = 0;
            int blockCharges = 0;
            float moveSpeed = 1f;
            float jumpForce = 1f;
            float basicDamage = 1f;
            float specialDamage = 1f;
            float cooldownReduction = 0f;
            float attackRange = 1f;
            float comboSpeed = 1f;
            float blockRecovery = 1f;
            float knockback = 1f;
            float projectileSpeed = 1f;
            float projectileDamage = 1f;
            float glideSpeed = 1f;
            float glideDuration = 1f;
            float zoneRadius = 1f;
            float zoneDuration = 1f;
            float persistentDuration = 1f;
            float persistentRange = 1f;
            float persistentHealth = 1f;
            float statusDuration = 1f;
            float statusIntensity = 1f;
            float rallyEchoFraction = 1f;
            float ultimateBuildRate = 1f;
            float extractorDamage = 1f;
            Dictionary<ScopedStatKey, float> scoped = null;
            foreach (ResonanceNodeData node in grid.Nodes ?? Array.Empty<ResonanceNodeData>()) {
                if (node == null || !unlocked.Contains(node.NodeID)) continue;
                // V7.6 ability-scoped lanes. Additive on top of the neutral 1.0
                // so two nodes scoping the same ability compose the same way
                // the character-wide lanes do.
                if (Array.IndexOf(ResonanceStatKeys.ScopableKeys, node.StatModifierKey) >= 0
                    && !string.IsNullOrWhiteSpace(node.AbilityScope)) {
                    scoped ??= new Dictionary<ScopedStatKey, float>();
                    var scopeKey = new ScopedStatKey(node.StatModifierKey, node.AbilityScope);
                    scoped[scopeKey] = scoped.GetValueOrDefault(scopeKey, 1f) + PercentValue(node);
                    continue;
                }
                switch (node.StatModifierKey) {
                    case "MaxHP": maxHP += Mathf.RoundToInt(node.StatModifierValue); break;
                    case "BlockCharges": blockCharges += Mathf.RoundToInt(node.StatModifierValue); break;
                    case "MoveSpeed": moveSpeed += PercentValue(node); break;
                    case "JumpForce": jumpForce += PercentValue(node); break;
                    case "BasicAttackDamage": basicDamage += PercentValue(node); break;
                    case "SpecialDamage": specialDamage += PercentValue(node); break;
                    case "CooldownReduction": cooldownReduction += PercentValue(node); break;
                    case "AttackRange": attackRange += PercentValue(node); break;
                    case "ComboSpeed": comboSpeed += PercentValue(node); break;
                    case "BlockRecovery": blockRecovery += PercentValue(node); break;
                    case "KnockbackForce": knockback += PercentValue(node); break;
                    case "ProjectileSpeed": projectileSpeed += PercentValue(node); break;
                    case "ProjectileDamage": projectileDamage += PercentValue(node); break;
                    case "GlideSpeed": glideSpeed += PercentValue(node); break;
                    // Ability-scoped minors (audit Low "Kits/talents"): glide
                    // windows and authored ability zones, so re-pointed talents
                    // like "Rift Range +10%" reach their designed target instead
                    // of a kit-wide stat.
                    case "GlideDuration": glideDuration += PercentValue(node); break;
                    case "ZoneRadius": zoneRadius += PercentValue(node); break;
                    case "ZoneDuration": zoneDuration += PercentValue(node); break;
                    case "PersistentDuration": persistentDuration += PercentValue(node); break;
                    case "PersistentRange": persistentRange += PercentValue(node); break;
                    case "PersistentHealth": persistentHealth += PercentValue(node); break;
                    case "StatusDuration": statusDuration += PercentValue(node); break;
                    case "StatusDamage": statusIntensity += PercentValue(node); break;
                    // V7.6 lanes.
                    case "RallyEchoFraction": rallyEchoFraction += PercentValue(node); break;
                    case "UltimateBuildRate": ultimateBuildRate += PercentValue(node); break;
                    case "ExtractorDamage": extractorDamage += PercentValue(node); break;
                    // An unknown or empty key resolves neutral. Traversal nodes
                    // deliberately carry no stat key at all - they land in
                    // StoryAbilityPerks through AbilityModifierKey instead.
                }
            }
            return new StoryStatProfile(
                maxHP, blockCharges, moveSpeed, jumpForce, basicDamage, specialDamage,
                cooldownMultiplier: Mathf.Max(0.05f, 1f - cooldownReduction),
                attackRangeMultiplier: attackRange,
                comboSpeedMultiplier: comboSpeed,
                blockRecoveryMultiplier: blockRecovery,
                knockbackMultiplier: knockback,
                projectileSpeedMultiplier: projectileSpeed,
                projectileDamageMultiplier: projectileDamage,
                glideSpeedMultiplier: glideSpeed,
                persistentDurationMultiplier: persistentDuration,
                persistentRangeMultiplier: persistentRange,
                persistentHealthMultiplier: persistentHealth,
                statusDurationMultiplier: statusDuration,
                statusIntensityMultiplier: statusIntensity,
                glideDurationMultiplier: glideDuration,
                zoneRadiusMultiplier: zoneRadius,
                zoneDurationMultiplier: zoneDuration,
                rallyEchoFractionMultiplier: rallyEchoFraction,
                ultimateBuildRateMultiplier: ultimateBuildRate,
                extractorDamageMultiplier: extractorDamage,
                scopedMultipliers: scoped);
        }

        public static float GetUnlockedStatTotal(
            ResonanceGridData grid,
            StorySaveData save,
            string statModifierKey) {
            if (grid == null
                || save == null
                || grid.CharacterID != save.SelectedCharacterID
                || string.IsNullOrWhiteSpace(statModifierKey)) return 0f;
            List<string> unlocked = GetUnlockedNodes(save, grid.CharacterID);
            float total = 0f;
            foreach (ResonanceNodeData node in grid.Nodes ?? Array.Empty<ResonanceNodeData>()) {
                if (node != null
                    && unlocked.Contains(node.NodeID)
                    && string.Equals(node.StatModifierKey, statModifierKey, StringComparison.Ordinal)) {
                    total += node.StatModifierValue;
                }
            }
            return total;
        }

        public static bool HasUnlockedAbilityModifier(
            ResonanceGridData grid,
            StorySaveData save,
            string abilityModifierKey) {
            if (grid == null
                || save == null
                || grid.CharacterID != save.SelectedCharacterID
                || string.IsNullOrWhiteSpace(abilityModifierKey)) return false;
            List<string> unlocked = GetUnlockedNodes(save, grid.CharacterID);
            foreach (ResonanceNodeData node in grid.Nodes ?? Array.Empty<ResonanceNodeData>()) {
                if (node != null
                    && unlocked.Contains(node.NodeID)
                    && string.Equals(node.AbilityModifierKey, abilityModifierKey, StringComparison.Ordinal)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Collects the ability modifier keys (major perks) currently unlocked on
        /// the grid into <paramref name="destination"/>. Story-only: callers must
        /// never feed these into Fighter loadouts.
        /// </summary>
        public static void CollectUnlockedAbilityModifiers(
            ResonanceGridData grid,
            StorySaveData save,
            ICollection<string> destination) {
            if (destination == null) return;
            destination.Clear();
            if (grid == null || save == null || grid.CharacterID != save.SelectedCharacterID) return;
            List<string> unlocked = GetUnlockedNodes(save, grid.CharacterID);
            foreach (ResonanceNodeData node in grid.Nodes ?? Array.Empty<ResonanceNodeData>()) {
                if (node != null
                    && !string.IsNullOrWhiteSpace(node.AbilityModifierKey)
                    && unlocked.Contains(node.NodeID)) {
                    destination.Add(node.AbilityModifierKey);
                }
            }
        }

        /// <summary>
        /// Resolves the active save slot's unlocked ability modifier keys for the
        /// selected character. Returns false when no active Story context exists.
        /// </summary>
        public static bool TryCollectActiveAbilityModifiers(string characterID, ICollection<string> destination) {
            if (destination == null) return false;
            destination.Clear();
            if (SaveManager.Instance == null || GameManager.Instance == null) return false;
            int slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            if (slot < 0 || slot >= SaveManager.Instance.SaveSlots.Length) return false;
            StorySaveData save = SaveManager.Instance.SaveSlots[slot];
            if (save == null || save.SelectedCharacterID != characterID) return false;
            ResonanceGridData grid = LoadGrid(characterID);
            if (grid == null) return false;
            CollectUnlockedAbilityModifiers(grid, save, destination);
            return true;
        }

        public static bool TryResolveActive(string characterID, out StoryStatProfile profile) {
            profile = StoryStatProfile.Default;
            if (SaveManager.Instance == null || GameManager.Instance == null) return false;
            int slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            if (slot < 0 || slot >= SaveManager.Instance.SaveSlots.Length) return false;
            StorySaveData save = SaveManager.Instance.SaveSlots[slot];
            if (save == null || save.SelectedCharacterID != characterID) return false;
            ResonanceGridData grid = LoadGrid(characterID);
            if (grid == null) return false;
            profile = Resolve(grid, save);
            return true;
        }

        public static List<string> GetUnlockedNodes(StorySaveData save, string characterID) {
            if (!save.GridProgress.TryGetValue(characterID, out List<string> unlocked) || unlocked == null) {
                unlocked = new List<string>();
                save.GridProgress[characterID] = unlocked;
            }
            return unlocked;
        }

        private static ResonanceNodeData FindNode(ResonanceGridData grid, string nodeID) {
            foreach (ResonanceNodeData node in grid.Nodes ?? Array.Empty<ResonanceNodeData>()) {
                if (node?.NodeID == nodeID) return node;
            }
            return null;
        }

        private static float PercentValue(ResonanceNodeData node) =>
            node.StatModifierIsPercent ? node.StatModifierValue : node.StatModifierValue;
    }
}
