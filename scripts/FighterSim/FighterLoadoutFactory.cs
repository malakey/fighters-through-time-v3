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
                RoundDamage(specialOne?.BaseDamage ?? 0f),
                RoundDamage(specialTwo?.BaseDamage ?? 0f),
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
                new FighterAbilityLoadout(
                    (int)(specialOne?.ExecutionType ?? AbilityExecutionType.Melee),
                    (int)(specialTwo?.ExecutionType ?? AbilityExecutionType.Melee),
                    DurationFrames(specialOne?.ProjectileLifetime ?? 0f),
                    DurationFrames(specialTwo?.ProjectileLifetime ?? 0f),
                    PersistentObjectTypeID(specialOne?.PersistentObjectID),
                    specialOne?.MaxActiveObjects ?? 0,
                    DurationFrames(specialOne?.Lifetime ?? 0f),
                    PersistentObjectTypeID(specialTwo?.PersistentObjectID),
                    specialTwo?.MaxActiveObjects ?? 0,
                    DurationFrames(specialTwo?.Lifetime ?? 0f),
                    (int)(movement?.MovementType ?? MovementType.Dash),
                    CooldownFrames(movement),
                    DurationFrames(movement?.MovementDuration ?? 0f),
                    movement?.ResetsDoubleJump == true ? 1 : 0,
                    movement?.GrantsHyperArmor == true ? 1 : 0,
                    PersistentObjectTypeID(movement?.PersistentObjectID),
                    movement?.MaxActiveObjects ?? 0,
                    DurationFrames(movement?.Lifetime ?? 0f),
                    WorldSpeed(specialOne?.ProjectileSpeed ?? 0f),
                    WorldSpeed(specialTwo?.ProjectileSpeed ?? 0f),
                    WorldDistance(movement?.DistanceMoved ?? 0f),
                    WorldSpeed(movement?.MovementSpeed ?? 0f)));
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
