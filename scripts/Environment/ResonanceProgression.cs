using System;
using System.Collections.Generic;
using FTT.Core;
using Godot;

namespace FTT.Environment {

    /// <summary>Story-only currency, unlock, and stat resolution boundary.</summary>
    public static class ResonanceProgression {
        public static ResonanceUnlockResult TryUnlock(
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
            save.DepositedChronalDust[grid.CharacterID] = balance - node.UnlockCost;
            unlocked.Add(node.NodeID);
            EventBus.Instance?.RaiseTalentNodeUnlocked(node.NodeID);
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
            foreach (ResonanceNodeData node in grid.Nodes ?? Array.Empty<ResonanceNodeData>()) {
                if (node == null || !unlocked.Contains(node.NodeID)) continue;
                switch (node.StatModifierKey) {
                    case "MaxHP": maxHP += Mathf.RoundToInt(node.StatModifierValue); break;
                    case "BlockCharges": blockCharges += Mathf.RoundToInt(node.StatModifierValue); break;
                    case "MoveSpeed": moveSpeed += PercentValue(node); break;
                    case "JumpForce": jumpForce += PercentValue(node); break;
                    case "BasicAttackDamage": basicDamage += PercentValue(node); break;
                    case "SpecialDamage": specialDamage += PercentValue(node); break;
                }
            }
            return new StoryStatProfile(maxHP, blockCharges, moveSpeed, jumpForce, basicDamage, specialDamage);
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

        public static bool TryResolveActive(string characterID, out StoryStatProfile profile) {
            profile = StoryStatProfile.Default;
            if (SaveManager.Instance == null || GameManager.Instance == null) return false;
            int slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            if (slot < 0 || slot >= SaveManager.Instance.SaveSlots.Length) return false;
            StorySaveData save = SaveManager.Instance.SaveSlots[slot];
            if (save == null || save.SelectedCharacterID != characterID) return false;
            ResonanceGridData grid = ResourceLoader.Load<ResonanceGridData>(
                $"res://resources/Resonance/{characterID}_grid.tres");
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
