using FTT.Core;
using Godot;
using System;

namespace FTT.Environment {

    public enum StoryPickupKind {
        Healing,
        DamageBuff,
        SpeedBuff
    }

    [GlobalClass]
    public partial class StoryDropProfile : Resource {
        [Export] public Difficulty Difficulty = Difficulty.Normal;
        [Export(PropertyHint.Range, "0,1,0.01")] public float RandomItemChance = 0.15f;
        [Export] public int HealingAmount = 25;
        [Export] public float BuffMultiplier = 1.25f;
        [Export] public float BuffDurationSeconds = 10f;
        [Export] public int HealingWeight = 1;
        [Export] public int DamageBuffWeight = 1;
        [Export] public int SpeedBuffWeight = 1;

        public bool AllowsBuffs => BuffMultiplier > 1f && BuffDurationSeconds > 0f &&
            (DamageBuffWeight > 0 || SpeedBuffWeight > 0);

        public bool ShouldDrop(float zeroToOneRoll) => ShouldDrop(zeroToOneRoll, 1f);

        /// <summary>
        /// Difficulty profile chance scaled by a per-enemy multiplier
        /// (EnemyData.ItemDropChance); 1.0 reproduces the profile exactly.
        /// </summary>
        public bool ShouldDrop(float zeroToOneRoll, float perEnemyMultiplier) {
            float chance = Mathf.Clamp(RandomItemChance, 0f, 1f) * Mathf.Max(0f, perEnemyMultiplier);
            return Mathf.Clamp(zeroToOneRoll, 0f, 1f) < Mathf.Clamp(chance, 0f, 1f);
        }

        public StoryPickupKind ChooseItem(float zeroToOneRoll) {
            int healing = Math.Max(0, HealingWeight);
            int damage = AllowsBuffs ? Math.Max(0, DamageBuffWeight) : 0;
            int speed = AllowsBuffs ? Math.Max(0, SpeedBuffWeight) : 0;
            int total = healing + damage + speed;
            if (total <= 0) return StoryPickupKind.Healing;
            float selection = Mathf.Clamp(zeroToOneRoll, 0f, 0.999999f) * total;
            if (selection < healing) return StoryPickupKind.Healing;
            if (selection < healing + damage) return StoryPickupKind.DamageBuff;
            return StoryPickupKind.SpeedBuff;
        }
    }
}
