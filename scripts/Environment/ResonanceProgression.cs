using System;
using System.Collections.Generic;
using FTT.Core;
using Godot;

namespace FTT.Environment {

    /// <summary>Story-only currency, unlock, and stat resolution boundary.</summary>
    public static class ResonanceProgression {

        public const string GridResourceDirectory = "res://resources/Resonance";

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
            foreach (string prerequisite in node.PrerequisiteNodeIDs ?? Array.Empty<string>()) {
                if (!unlocked.Contains(prerequisite)) return ResonanceUnlockResult.MissingPrerequisite;
            }
            int balance = save.DepositedChronalDust.GetValueOrDefault(grid.CharacterID);
            if (balance < node.UnlockCost) return ResonanceUnlockResult.InsufficientDust;
            return ResonanceUnlockResult.Unlocked;
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
            float persistentDuration = 1f;
            float persistentRange = 1f;
            float persistentHealth = 1f;
            float statusDuration = 1f;
            float statusIntensity = 1f;
            foreach (ResonanceNodeData node in grid.Nodes ?? Array.Empty<ResonanceNodeData>()) {
                if (node == null || !unlocked.Contains(node.NodeID)) continue;
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
                    case "PersistentDuration": persistentDuration += PercentValue(node); break;
                    case "PersistentRange": persistentRange += PercentValue(node); break;
                    case "PersistentHealth": persistentHealth += PercentValue(node); break;
                    case "StatusDuration": statusDuration += PercentValue(node); break;
                    case "StatusDamage": statusIntensity += PercentValue(node); break;
                    // "BlockDurability" (percent shield durability against discrete
                    // integer block charges) and "Armor" (the design's combat rules
                    // explicitly forbid a damage-reducing armor stat) have no
                    // existing behavior to hook and intentionally resolve neutral.
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
                statusIntensityMultiplier: statusIntensity);
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
