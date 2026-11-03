using FTT.Combat;
using FTT.Core;
using Godot;

namespace FTT.Enemies {

    /// <summary>
    /// Gameplay review 2026-11-03 (rulings 2b, 2b+; plan section 1.12): a Standard
    /// mob's soft-counter trait, read from <see cref="EnemyData.Trait"/>.
    ///
    /// <para><b>Armoured</b> takes half from Basic and Special hits and wears the
    /// gold glow until a throw launches it. <b>Catcher</b> takes string hits 1 and 2
    /// without a stun or a knockback, and the second earns a counter; a Special's
    /// stun or a grab answers it, because only a mob that is not stunned catches.
    /// <b>Anti-air</b> meets a hero descending into its reach with a rising strike
    /// and sits in a long recovery when it misses. Both trait attacks are never gated by
    /// the engagement director: like the V7.4 answers they are retaliation.</para>
    ///
    /// <para>A summoned body carries no trait: Level 0's hold spawns its era locals
    /// as summons, and its lesson teaches the plain string. Story only, never
    /// persisted or snapshotted; nothing under <c>scripts/FighterSim/</c> reads it.
    /// Nobody has played any of this.</para>
    /// </summary>
    public partial class EnemyController {

        // Own ticks before a trait attack may fire again.
        private int _traitCooldownFrames;
        // A throw has taken the Armoured mob's armour off.
        private bool _traitArmourStripped;
        // Built per body from its Data, on first use (ResetTraitState drops them).
        private EnemyAbilityData _catcherCounter;
        private EnemyAbilityData _antiAirStrike;

        /// <summary>The trait this body acts on: Standard tier only, never on a summon.</summary>
        public EnemyTrait ActiveTrait =>
            Data?.Tier == EnemyTier.Standard && !IsSummoned ? Data.Trait : EnemyTrait.None;

        /// <summary>The armour is up: halved Basic and Special hits and the gold glow.</summary>
        public bool IsTraitArmourIntact =>
            ActiveTrait == EnemyTrait.Armoured && !_traitArmourStripped && CurrentState != EnemyState.Dead;

        /// <summary>The shared gold armour glow is up. Test seam.</summary>
        public bool IsArmourGlowShowing => _glow?.IsLayerActive(GlowLayer.HyperArmor) == true;

        /// <summary>Own ticks before a trait attack may fire again. Test seam.</summary>
        public int TraitCooldownFramesRemaining => _traitCooldownFrames;

        /// <summary>The executor's telegraph glyph, or null before the first telegraph. Test seam.</summary>
        public TelegraphGlyph TelegraphGlyphNode => Executor.Glyph;

        /// <summary>The Catcher's counter, built on first use. Test seam.</summary>
        public EnemyAbilityData CatcherCounterAbility => _catcherCounter ??= BuildTraitStrike(
            "catcher_counter",
            StoryEnemyCombatRules.CatcherCounterTelegraphFrames,
            StoryEnemyCombatRules.CatcherCounterActiveFrames,
            StoryEnemyCombatRules.CatcherCounterRecoveryFrames,
            StoryEnemyCombatRules.CatcherCounterHitstunSeconds,
            new Vector2(Data?.AttackKnockback ?? 2f, -1.5f),
            new Vector2(StoryEnemyCombatRules.CatcherCounterBoxWidthPixels, StoryEnemyCombatRules.CatcherCounterBoxHeightPixels),
            new Vector2(StoryEnemyCombatRules.CatcherCounterBoxOffsetXPixels, StoryEnemyCombatRules.CatcherCounterBoxOffsetYPixels));

        /// <summary>The Anti-air rising strike, built on first use. Test seam.</summary>
        public EnemyAbilityData AntiAirStrikeAbility => _antiAirStrike ??= BuildTraitStrike(
            "anti_air_strike",
            EnemyTraitRules.AntiAirTelegraphFrames,
            EnemyTraitRules.AntiAirActiveFrames,
            EnemyTraitRules.AntiAirMissRecoveryFrames,
            EnemyTraitRules.AntiAirHitstunSeconds,
            new Vector2(Data?.AttackKnockback ?? 2f, EnemyTraitRules.AntiAirKnockbackUp),
            new Vector2(EnemyTraitRules.AntiAirBoxWidthPixels, EnemyTraitRules.AntiAirBoxHeightPixels),
            new Vector2(EnemyTraitRules.AntiAirBoxOffsetXPixels, EnemyTraitRules.AntiAirBoxOffsetYPixels));

        /// <summary>
        /// A trait strike is synthesized from its mob's scalars, like the legacy
        /// primary. It is Guard-Crush class because the telegraph draws a diamond
        /// and the tint and glyph may never disagree (<see cref="TelegraphGlyph"/>):
        /// blockable, for two charges.
        /// </summary>
        private EnemyAbilityData BuildTraitStrike(string suffix, int telegraph, int active, int recovery,
            float hitstunSeconds, Vector2 knockback, Vector2 boxSize, Vector2 boxOffset) => new() {
                AbilityID = $"{Data?.EnemyID ?? "enemy"}.{suffix}",
                Archetype = EnemyAbilityArchetype.MeleeStrike,
                RangeClass = EnemyAbilityRangeClass.Melee,
                TelegraphFrames = telegraph,
                ActiveFrames = active,
                RecoveryFrames = recovery,
                Damage = Data?.AttackDamage ?? 5f,
                KnockbackForce = knockback,
                HitstunDuration = hitstunSeconds,
                IsGuardCrushing = true,
                HitboxSize = boxSize,
                HitboxOffset = boxOffset,
                TelegraphTint = TelegraphGlyph.GuardCrushColor,
                PresentationEventID = (Data?.PrimaryAttack ?? GetLegacyPrimaryAttack()).PresentationEventID
            };

        // === Hooks (one-line calls from EnemyController.cs) ===

        /// <summary>A fresh body: spawn, new data, or a restoring rewind.</summary>
        private void ResetTraitState() {
            _traitCooldownFrames = 0;
            _traitArmourStripped = false;
            _catcherCounter = null;
            _antiAirStrike = null;
            RefreshArmorPresentation();
        }

        /// <summary>Armoured: the throw that launches the mob takes the armour off for good.</summary>
        private void StripTraitArmour() {
            if (!IsTraitArmourIntact) return;
            _traitArmourStripped = true;
            EnemyTraitTally.ArmourStrips++;
            RefreshArmorPresentation();
        }

        /// <summary>The Armoured factor of this hit, 1 for every other mob.</summary>
        private float TraitIntakeScale(in HitPayload hit) =>
            EnemyTraitRules.ArmouredIntakeScale(IsTraitArmourIntact, hit.AttackClass, hit.Origin, hit.Delivery);

        /// <summary>Folds the armour into the intake scale, inside its one rounding.</summary>
        private float ApplyTraitIntake(in HitPayload hit, int baseDamage, float scale) {
            float armour = TraitIntakeScale(in hit);
            if (armour >= 1f) return scale;
            EnemyTraitTally.ArmourDamagePrevented += StoryEnemyCombatRules.ScaleIntakeDamage(baseDamage, scale)
                - StoryEnemyCombatRules.ScaleIntakeDamage(baseDamage, scale * armour);
            return scale * armour;
        }

        /// <summary>The hit's intake with the armour left out: a wall impact is reckoned from it.</summary>
        private int TraitUnreducedIntake(in HitPayload hit, int baseDamage, float intakeScale) =>
            StoryEnemyCombatRules.ScaleIntakeDamage(baseDamage, intakeScale / TraitIntakeScale(in hit));

        /// <summary>
        /// Catcher: a free mob takes string hit 1 or 2 with no stun and no
        /// knockback. Rewrites the payload the intake then reads. A mob already
        /// stunned catches nothing, which is how a Special answers it.
        /// </summary>
        private bool CatchStringHit(ref HitPayload hit) {
            if (ActiveTrait != EnemyTrait.Catcher) return false;
            if (CurrentState is EnemyState.Dead or EnemyState.Stunned || _isHeldByPlayer || _thrownFlight) return false;
            if (!StoryEnemyCombatRules.CatcherCatchesHit(hit.AttackClass, hit.HitboxID)) return false;
            hit.Knockback = Vector2.Zero;
            hit.HitstunDuration = 0f;
            hit.Launches = false;
            // Static Charge's action lock is a stun in PvE.
            if (hit.AppliedStatus == FTT.Core.StatusType.StaticCharge) {
                hit.AppliedStatus = FTT.Core.StatusType.None;
                hit.StatusDuration = 0f;
            }
            EnemyTraitTally.CatcherCatches++;
            return true;
        }

        /// <summary>Catcher: the second string hit, off cooldown and with no attack in flight, earns the counter.</summary>
        private void AnswerCaughtHit(in HitPayload hit) {
            if (CurrentState == EnemyState.Dead) return;
            if (!StoryEnemyCombatRules.CatcherCounterFires(
                    StoryEnemyCombatRules.StringStepOf(hit.HitboxID), _traitCooldownFrames, Executor.IsBusy)) return;
            _target ??= FindNearestPlayer();
            if (_target == null) return;
            EnemyTraitTally.CatcherCounters++;
            CommitTraitStrike(CatcherCounterAbility, StoryEnemyCombatRules.CatcherCounterCooldownFrames);
        }

        /// <summary>The trait clock and the Anti-air check, once per unfrozen, unheld tick.</summary>
        private void TickTraits() {
            if (_traitCooldownFrames > 0) _traitCooldownFrames--;
            if (ActiveTrait == EnemyTrait.AntiAir && AntiAirTriggered()) {
                EnemyTraitTally.AntiAirStrikes++;
                CommitTraitStrike(AntiAirStrikeAbility, EnemyTraitRules.AntiAirCooldownFrames);
            }
        }

        /// <summary>The hero is descending into this free mob's reach, with no wall or ceiling between them.</summary>
        private bool AntiAirTriggered() {
            if (_target == null || !IsInstanceValid(_target)) return false;
            if (_finisherFlight || IsEncounterDormant) return false;
            bool free = CurrentState == EnemyState.Chase || (CurrentState == EnemyState.Attacking && !_attackCommitted);
            if (!free) return false;
            bool jumpingIn = EnemyTraitRules.HeroIsJumpingIn(
                _target.CurrentState is FTT.Characters.CharacterState.Airborne or FTT.Characters.CharacterState.Attacking,
                _target.IsOnFloor(), _target.Velocity.Y);
            Vector2 toHero = _target.GlobalPosition - GlobalPosition;
            if (!EnemyTraitRules.AntiAirFires(jumpingIn, Mathf.Abs(toHero.X), -toHero.Y, _traitCooldownFrames, Executor.IsBusy)) return false;
            Vector2 head = GlobalPosition + new Vector2(0f, _bodyCenterY);
            Vector2 heroChest = _target.GlobalPosition + new Vector2(0f, _bodyCenterY);
            return !ProbeHits(head, heroChest, CollisionLayers.Environment, hitFromInside: false, fallback: false);
        }

        /// <summary>Commits a trait strike at once: no reaction delay, never gated, facing the hero.</summary>
        private void CommitTraitStrike(EnemyAbilityData strike, int cooldownFrames) {
            _traitCooldownFrames = cooldownFrames;
            _armoredAnswerPending = false;
            _secondaryBlinkInFlight = false;
            _lastAttackWasElite = false;
            _standOffEngaged = false;
            CurrentState = EnemyState.Attacking;
            _attackCommitted = true;
            _reactionFramesRemaining = 0;
            Velocity = new Vector2(0f, Velocity.Y);
            SetFacing(_target.GlobalPosition.X >= GlobalPosition.X);
            BeginAttack(strike);
        }

        /// <summary>
        /// A trait strike's active window ended: the rising strike's recovery is set
        /// here because the executor reads it next (a miss keeps the ruled 40 frames,
        /// a contact takes the short one). Tallies the contacts for the bot reports.
        /// </summary>
        private void NoteTraitWindowEnded(EnemyAbilityData ability, bool connected) {
            if (ability == null) return;
            if (ReferenceEquals(ability, _antiAirStrike)) {
                ability.RecoveryFrames = connected
                    ? EnemyTraitRules.AntiAirHitRecoveryFrames
                    : EnemyTraitRules.AntiAirMissRecoveryFrames;
                if (connected) EnemyTraitTally.AntiAirStrikesConnected++;
            } else if (connected && ReferenceEquals(ability, _catcherCounter)) {
                EnemyTraitTally.CatcherCountersConnected++;
            }
        }
    }
}
