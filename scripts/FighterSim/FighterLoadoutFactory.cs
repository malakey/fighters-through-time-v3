using System;
using FTT.Characters;
using FTT.Combat;
using xpTURN.Klotho.Deterministic.Math;

namespace FTT.FighterSim {

    /// <summary>
    /// Converts normalized base resources into immutable deterministic match tuning.
    /// Story progression modifiers must never be applied at this boundary.
    /// </summary>
    public static class FighterLoadoutFactory {
        public static FighterLoadout FromCharacterData(CharacterData data) {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (!TryGetCharacterID(data.CharacterID, out FighterCharacterID characterID)) {
                throw new ArgumentException($"Unknown Fighter character ID '{data.CharacterID}'.", nameof(data));
            }

            AbilityData specialOne = data.SpecialAttackOne;
            AbilityData specialTwo = data.SpecialAttackTwo;
            MovementAbilityData movement = data.MovementAbility;
            AbilityData ultimate = data.UltimateAttack;
            return new FighterLoadout(
                (int)characterID,
                Math.Max(1, data.MaxHP),
                Math.Max(0, data.MaxBlockCharges),
                Math.Max(1, data.MaxJumpCount),
                RoundDamage(data.BasicAttackDamage),
                RoundDamage(SpecialTotalDamage(specialOne)),
                RoundDamage(SpecialTotalDamage(specialTwo)),
                RoundDamage(ultimate?.BaseDamage ?? 0f),
                CooldownFrames(specialOne),
                CooldownFrames(specialTwo),
                (int)(specialOne?.AppliedStatus ?? FTT.Core.StatusType.None),
                DurationFrames(specialOne?.StatusDuration ?? 0f),
                (int)(specialTwo?.AppliedStatus ?? FTT.Core.StatusType.None),
                DurationFrames(specialTwo?.StatusDuration ?? 0f),
                (int)(ultimate?.AppliedStatus ?? FTT.Core.StatusType.None),
                DurationFrames(ultimate?.StatusDuration ?? 0f),
                FP64.FromFloat(Math.Max(0.01f, data.Weight)),
                FP64.FromFloat(Math.Max(0f, data.MaxMoveSpeed)),
                FP64.FromFloat(Math.Max(0f, data.MaxJumpForce)),
                FP64.FromFloat(Math.Max(0f, data.BasicAttackKnockback)),
                KnockbackMagnitude(specialOne),
                KnockbackMagnitude(specialTwo),
                KnockbackMagnitude(ultimate),
                StatusIntensity(specialOne),
                StatusIntensity(specialTwo),
                StatusIntensity(ultimate),
                new FighterAbilityLoadout {
                    SpecialOneExecutionType = (int)(specialOne?.ExecutionType ?? AbilityExecutionType.Melee),
                    SpecialTwoExecutionType = (int)(specialTwo?.ExecutionType ?? AbilityExecutionType.Melee),
                    SpecialOneProjectileLifetimeFrames = DurationFrames(specialOne?.ProjectileLifetime ?? 0f),
                    SpecialTwoProjectileLifetimeFrames = DurationFrames(specialTwo?.ProjectileLifetime ?? 0f),
                    SpecialOnePersistentTypeID = PersistentObjectTypeID(specialOne?.PersistentObjectID),
                    SpecialOneMaxActiveObjects = specialOne?.MaxActiveObjects ?? 0,
                    SpecialOnePersistentLifetimeFrames = DurationFrames(specialOne?.Lifetime ?? 0f),
                    SpecialTwoPersistentTypeID = PersistentObjectTypeID(specialTwo?.PersistentObjectID),
                    SpecialTwoMaxActiveObjects = specialTwo?.MaxActiveObjects ?? 0,
                    SpecialTwoPersistentLifetimeFrames = DurationFrames(specialTwo?.Lifetime ?? 0f),
                    SpecialOneTickIntervalFrames = Math.Max(0, specialOne?.DamageTickIntervalFrames ?? 0),
                    SpecialTwoTickIntervalFrames = Math.Max(0, specialTwo?.DamageTickIntervalFrames ?? 0),
                    MovementType = (int)(movement?.MovementType ?? MovementType.Dash),
                    MovementCooldownFrames = CooldownFrames(movement),
                    MovementDurationFrames = DurationFrames(movement?.MovementDuration ?? 0f),
                    MovementResetsJump = movement?.ResetsDoubleJump == true ? 1 : 0,
                    MovementGrantsHyperArmor = movement?.GrantsHyperArmor == true ? 1 : 0,
                    MovementPersistentTypeID = PersistentObjectTypeID(movement?.PersistentObjectID),
                    MovementMaxActiveObjects = movement?.MaxActiveObjects ?? 0,
                    MovementPersistentLifetimeFrames = DurationFrames(movement?.Lifetime ?? 0f),
                    SpecialOneProjectileSpeed = WorldSpeed(specialOne?.ProjectileSpeed ?? 0f),
                    SpecialTwoProjectileSpeed = WorldSpeed(specialTwo?.ProjectileSpeed ?? 0f),
                    MovementDistance = WorldDistance(movement?.DistanceMoved ?? 0f),
                    MovementSpeed = WorldSpeed(movement?.MovementSpeed ?? 0f)
                });
        }

        public static bool TryGetCharacterID(string characterID, out FighterCharacterID value) {
            switch (characterID?.Trim().ToLowerInvariant()) {
                case "einstein": value = FighterCharacterID.Einstein; return true;
                case "joan": value = FighterCharacterID.Joan; return true;
                case "leonardo": value = FighterCharacterID.Leonardo; return true;
                case "lincoln": value = FighterCharacterID.Lincoln; return true;
                case "cleopatra": value = FighterCharacterID.Cleopatra; return true;
                case "tesla": value = FighterCharacterID.Tesla; return true;
                case "shakespeare": value = FighterCharacterID.Shakespeare; return true;
                case "mozart": value = FighterCharacterID.Mozart; return true;
                case "pocahontas": value = FighterCharacterID.Pocahontas; return true;
                default: value = default; return false;
            }
        }

        private static int RoundDamage(float damage) => Math.Max(0, (int)MathF.Round(damage, MidpointRounding.AwayFromZero));

        /// <summary>
        /// Melee-execution multi-hit specials (Joan's Divine Piercing) resolve as a
        /// single deterministic application of their full multi-hit total. Area and
        /// persistent executions keep per-hit damage because their systems apply it
        /// repeatedly per tick/attack.
        /// </summary>
        private static float SpecialTotalDamage(AbilityData ability) {
            if (ability == null) return 0f;
            float damage = ability.BaseDamage;
            if (ability.IsMultiHit && ability.ExecutionType == AbilityExecutionType.Melee) {
                damage *= Math.Max(1, ability.HitCount);
            }
            return damage;
        }
        private static int DurationFrames(float seconds) => Math.Max(0, (int)MathF.Round(seconds * FighterSimulation.TickRate, MidpointRounding.AwayFromZero));
        private static int CooldownFrames(AbilityData ability) => Math.Max(1, DurationFrames(ability?.CooldownDuration ?? 0f));

        private static FP64 KnockbackMagnitude(AbilityData ability) {
            if (ability == null) return FP64.Zero;
            float magnitude = MathF.Max(MathF.Abs(ability.KnockbackForce.X), MathF.Abs(ability.KnockbackForce.Y));
            return FP64.FromFloat(magnitude);
        }

        private static FP64 StatusIntensity(AbilityData ability) =>
            FP64.FromFloat(Math.Max(0f, ability?.StatusIntensity ?? 1f));

        private static FP64 WorldSpeed(float pixelsPerSecond) =>
            FP64.FromFloat(Math.Max(0f, pixelsPerSecond) / 60f);

        private static FP64 WorldDistance(float pixels) =>
            FP64.FromFloat(Math.Max(0f, pixels) / 60f);

        public static int PersistentObjectTypeID(string objectTypeID) => objectTypeID switch {
            "tesla_coil" => 1,
            "clockwork_turret" => 2,
            "serpent_nest" => 3,
            "vine_snare" => 4,
            "sonata_platform" => 5,
            _ => 0
        };
    }
}
