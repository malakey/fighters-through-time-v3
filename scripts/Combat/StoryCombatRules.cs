using FTT.Characters;

namespace FTT.Combat {

    public static class StoryCombatRules {
        public const int ComboBufferFrames = 24;
        public const int DropThroughFrames = 15;
        public const int DownDoubleTapFrames = 18;
        public const float CrouchHurtboxScale = 0.7f;

        public static bool IsDropThroughAllowed(CharacterState state) => state is
            CharacterState.Idle or
            CharacterState.Running or
            CharacterState.Crouching or
            CharacterState.Blocking or
            CharacterState.Attacking;

        public static bool HyperArmorPreventsInterruption(
            bool grantsHyperArmor,
            AbilityPhase phase,
            AttackClass incomingAttackClass) =>
            grantsHyperArmor &&
            phase is AbilityPhase.Startup or AbilityPhase.Active &&
            incomingAttackClass != AttackClass.Ultimate;
    }
}
