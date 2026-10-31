using System;
using System.Collections.Generic;
using FTT.Core;
using Godot;

namespace FTT.Environment {

    /// <summary>Story-only currency, unlock, and stat resolution boundary.</summary>
    public static class ResonanceProgression {

        public const string GridResourceDirectory = "res://resources/Resonance";

        /// <summary>
        /// The floor every cooldown multiplier resolves to, character-wide and
        /// ability-scoped alike: no stack of CooldownReduction nodes can take a
        /// cooldown below 5% of its authored length.
        /// </summary>
        public const float MinimumCooldownMultiplier = 0.05f;

        /// <summary>
        /// Test override for the V7.6 dormant-node gate,
        /// <c>(characterID, abilitySlot) =&gt; bool</c>. Production leaves it
        /// null, and the gate then reads the Legacy Unlock Schedule state of the
        /// save being evaluated (<see cref="IsAbilityUnlocked"/>). A test that
        /// installs one must restore the previous value in a <c>finally</c>.
        /// </summary>
        public static Func<string, string, bool> AbilityUnlockProbe;

        /// <summary>
        /// V7.6 dormant-node gate (A4's side of the A5 handshake): true when the
        /// node's gated ability slot is available. An ungated node always passes.
        /// With no test override installed the answer is the evaluated save's
        /// own Legacy Unlock Schedule state for <paramref name="characterID"/>
        /// (<see cref="LegacyUnlockSchedule.IsUnlockedFor"/>: the persisted
        /// unlock keys, backstopped by the completed-level history), which is
        /// exactly the gate <c>CharacterFactory</c> installs on the campaign
        /// hero. The grid is only ever opened over the active Story save (the
        /// hub Repository, an Act III Warden Beacon), so Fighter Mode, the
        /// Holodeck, the drills and the Test Arena never reach it. An unknown
        /// slot name reads as ungated; <see cref="ValidateGrid"/> rejects one.
        /// </summary>
        public static bool IsAbilityUnlocked(StorySaveData save, string characterID, string abilitySlot) {
            if (string.IsNullOrWhiteSpace(abilitySlot)) return true;
            Func<string, string, bool> probe = AbilityUnlockProbe;
            if (probe != null) return probe(characterID, abilitySlot);
            if (!LegacyUnlockSchedule.TryParseSlotKey(abilitySlot, out AbilitySlot slot)) return true;
            return LegacyUnlockSchedule.IsUnlockedFor(save, characterID, slot);
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
            // "Stat modifiers are applied immediately" (design, Resonance Grid UI
            // Flow, Purchase):
            // the live campaign hero re-resolves its grid from this event.
            EventBus.Instance?.RaiseResonanceProgressionChanged(grid.CharacterID);
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
            if (!IsAbilityUnlocked(save, grid.CharacterID, node.GatedAbilityID)) {
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
            // The respec reverts the live hero at once, like a purchase.
            EventBus.Instance?.RaiseResonanceProgressionChanged(grid.CharacterID);
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

        /// <summary>
        /// Package 11 A3b (V7.6 F02). The dust a Repository screen may actually
        /// spend: the character's <b>deposited</b> balance, never the active
        /// level's undeposited <c>LevelChronalDust</c>.
        ///
        /// <para>"Data crosses the field, matter doesn't." At an Act III Warden
        /// Beacon the player may buy grid nodes and respec for free, but dust
        /// collected in the level they are standing in is not bankable until that
        /// level completes and its completion transaction banks it — so Level
        /// 13's earnings first become spendable at Level 14's Beacon. Restarting
        /// a level clears those undeposited earnings under the existing rule, so
        /// repeating a level cannot bank repeat rewards.</para>
        ///
        /// <para>The hub Repository reads the same number and deposits nothing
        /// itself: since H02 (Package 12 W2) dust banks only in
        /// <c>StoryManager.CommitCompletionTransaction</c> at level completion,
        /// and the old deposit-then-open route is deleted, so an attempt's held
        /// wallet is never spendable at the hub either. This accessor is the one
        /// place that states the rule.</para>
        /// </summary>
        public static int SpendableBalance(StorySaveData save, string characterID) =>
            save == null || string.IsNullOrWhiteSpace(characterID)
                ? 0
                : Math.Max(0, save.DepositedChronalDust.GetValueOrDefault(characterID));

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
            float momentumRegen = 1f;
            Dictionary<ScopedStatKey, float> scoped = null;
            Dictionary<ScopedStatKey, float> scopedCooldownReduction = null;
            foreach (ResonanceNodeData node in grid.Nodes ?? Array.Empty<ResonanceNodeData>()) {
                if (node == null || !unlocked.Contains(node.NodeID)) continue;
                // V7.6 ability-scoped lanes. Additive on top of the neutral 1.0
                // so two nodes scoping the same ability compose the same way
                // the character-wide lanes do.
                if (Array.IndexOf(ResonanceStatKeys.ScopableKeys, node.StatModifierKey) >= 0
                    && !string.IsNullOrWhiteSpace(node.AbilityScope)) {
                    var scopeKey = new ScopedStatKey(node.StatModifierKey, node.AbilityScope);
                    if (node.StatModifierKey == ResonanceStatKeys.CooldownReduction) {
                        // A REDUCTION, not a multiplier: summed here and turned
                        // into max(0.05, 1 - sum) below, exactly like the
                        // character-wide lane. Storing 1 + 0.1 made Joan's
                        // Piercing, Mozart's Wave and Lincoln's Splitting
                        // cooldown minors lengthen their cooldown by 10%,
                        // because BaseSpecial multiplies the cooldown by it.
                        scopedCooldownReduction ??= new Dictionary<ScopedStatKey, float>();
                        scopedCooldownReduction[scopeKey] =
                            scopedCooldownReduction.GetValueOrDefault(scopeKey, 0f) + PercentValue(node);
                        continue;
                    }
                    scoped ??= new Dictionary<ScopedStatKey, float>();
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
                    case "MomentumRegen": momentumRegen += PercentValue(node); break;
                    // An unknown or empty key resolves neutral. Traversal nodes
                    // deliberately carry no stat key at all - they land in
                    // StoryAbilityPerks through AbilityModifierKey instead.
                }
            }
            if (scopedCooldownReduction != null) {
                scoped ??= new Dictionary<ScopedStatKey, float>();
                foreach (KeyValuePair<ScopedStatKey, float> reduction in scopedCooldownReduction) {
                    scoped[reduction.Key] = Mathf.Max(MinimumCooldownMultiplier, 1f - reduction.Value);
                }
            }
            return new StoryStatProfile(
                maxHP, blockCharges, moveSpeed, jumpForce, basicDamage, specialDamage,
                cooldownMultiplier: Mathf.Max(MinimumCooldownMultiplier, 1f - cooldownReduction),
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
                momentumRegenMultiplier: momentumRegen,
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
