using Godot;
using System;
using System.Collections.Generic;
using FTT.Core;

namespace FTT.Characters {

	public enum CharacterState {
		Idle,
		Running,
		/// <summary>
		/// Reserved. The universal dash mechanic was removed (2026-08-09 user
		/// directive); the member stays so serialized/int-cast state and the
		/// enum's ordinal layout are undisturbed. No code enters this state.
		/// </summary>
		Dashing,
		Rolling,
		Skidding,
		Crouching,
		Airborne,
		Attacking,
		UsingSpecial,
		UsingUltimate,
		Blocking,
		Stunned,
		Dazed,
		LedgeHanging,
		Dead,
		Respawning,
		UsingMovementAbility,
		/// <summary>V7.2 grab: startup, active window, hold, throw, and whiff
		/// recovery all live here (a phase counter drives the sub-states).</summary>
		Grabbing,
		/// <summary>
		/// F23 (Package 11 A1c): the grab VICTIM's launched state — from the
		/// throw's release until its hitstun expires. Distinct from
		/// <see cref="Stunned"/> because a throw is not a strike: it is
		/// unblockable by construction, it cannot be landing-teched, and it carries
		/// no directional influence (the throw IS the decision).
		///
		/// <para>Appended <b>after</b> <see cref="Grabbing"/>, per plan §2.8:
		/// <see cref="Dashing"/> keeps its reserved ordinal and no existing member
		/// moves, because the ordinals serialize.</para>
		/// </summary>
		Thrown
	}

	public partial class PlayerController : CharacterBody2D, FTT.Combat.IStatusEffectTarget, FTT.Combat.IDamageable {
		public const int StoryRewindInvulnerabilityFrames = 120;

		/// <summary>
		/// Story-only Resonance perk key (Joan). V7.6 rework: the connecting
		/// combo finisher reclaims an extra share of the CURRENT Rally echo
		/// pool on top of the damage-scaled reclaim. The V6 "heal 5% of missing
		/// HP" effect is retired.
		/// </summary>
		public const string ZealousVigorPerkKey = "zealous_vigor";
		/// <summary>
		/// Story-only Resonance perk key (Joan). V7.6 rework (F06 Option A):
		/// a flat meter grant per DISTINCT successfully blocked attack, plus a
		/// one-charge refund on the first block of a Guard-Crush execution.
		/// The V6 "blocked damage builds meter 25% faster" scaling is retired.
		/// </summary>
		public const string ShieldOfOrleansPerkKey = "shield_of_orleans";
		/// <summary>V7.6 Zealous Vigor: extra fraction of the live echo pool reclaimed on the finisher.</summary>
		public const float ZealousVigorEchoPoolFraction = 0.25f;
		/// <summary>V7.6 Shield of Orleans: flat Influence per distinct blocked attack.</summary>
		public const float ShieldOfOrleansMeterPerBlock = 5f;
		/// <summary>V7.6 Shield of Orleans: block-charge cap the Guard-Crush refund respects.</summary>
		public const int ShieldOfOrleansRefundCap = 3;
		/// <summary>
		/// V7.6 Shield of Orleans dedup window. The engine has no attack-
		/// execution ID, so repeats of the same (attacker, AttackID, HitboxID)
		/// inside this many physics frames count as ONE execution — long enough
		/// to fold a volley, a multi-hit ability and a zone pulse into one
		/// grant, short enough that re-using the attack later counts again.
		/// </summary>
		public const int ShieldOfOrleansExecutionWindowFrames = 60;

		/// <summary>Monotonic Story physics-frame counter; drives per-execution dedup windows.</summary>
		private int _storyFrame;
		private readonly Dictionary<string, int> _shieldOfOrleansExecutions = new(StringComparer.Ordinal);

		[Export] public CharacterData Data;
		[Export] public int PlayerIndex = 0;
		public FTT.Core.PlayerInputFrame CurrentInputFrame { get; private set; }

		// Runtime state
		public CharacterState CurrentState { get; private set; } = CharacterState.Idle;
		public int CurrentHP;
		public int CurrentBlockCharges;
		public float CurrentUltimateMeter;
		public bool IsFacingRight = true;
		public int RemainingJumps;
		public float SpecialOneCooldownTimer;
		public float SpecialTwoCooldownTimer;
		public float MovementAbilityCooldownTimer;
		public List<Node2D> ActivePersistentObjects = new();
		public int ComboCounter;
		public int GroundComboCounter { get; private set; }
		public int AerialComboCounter { get; private set; }
		public float StatusMovementMultiplier { get; set; } = 1.0f;
		public float StatusJumpMultiplier { get; set; } = 1.0f;
		public float StatusAnimationMultiplier { get; set; } = 1.0f;
		public float StatusDamageTakenMultiplier { get; set; } = 1.0f;
		public bool IsMovementRooted { get; set; }
		/// <summary>
		/// Story-only environment gravity scale (low-gravity fields, Chronal Void
		/// shifts). 1.0 is normal gravity; multiplied into the Story gravity
		/// integration only. Owned by <c>FTT.Environment.EnvironmentPlayerModifiers</c>
		/// so overlapping zones stack instead of clobbering each other's restore.
		/// Fighter Mode never reads this — <c>scripts/FighterSim/</c> is authoritative
		/// there and has no equivalent field.
		/// </summary>
		public float EnvironmentGravityScale { get; set; } = 1.0f;
		/// <summary>
		/// Story-only environment movement multiplier (deep sand, submerged water).
		/// Stacks multiplicatively with — and never replaces — the status-effect
		/// <see cref="StatusMovementMultiplier"/>. Same ownership and isolation rules
		/// as <see cref="EnvironmentGravityScale"/>.
		/// </summary>
		public float EnvironmentMoveMultiplier { get; set; } = 1.0f;
		public int StoryMaxHPBonus { get; set; }
		public int StoryBlockChargeBonus { get; set; }
		public float StoryMoveSpeedMultiplier { get; set; } = 1f;
		public float StoryJumpForceMultiplier { get; set; } = 1f;
		public float StoryBasicDamageMultiplier { get; set; } = 1f;
		public float StorySpecialDamageMultiplier { get; set; } = 1f;
		// Story-only Resonance minor stat multipliers (all neutral at 1f; populated
		// by CharacterFactory from the resolved StoryStatProfile of the active
		// save's grid). Fighter Mode never sets these — the deterministic
		// simulation is authoritative there and its loadouts never read them.
		/// <summary>Cooldown time multiplier: 0.9 means cooldowns run 10% shorter.</summary>
		public float StoryCooldownMultiplier { get; set; } = 1f;
		public float StoryAttackRangeMultiplier { get; set; } = 1f;
		public float StoryComboSpeedMultiplier { get; set; } = 1f;
		public float StoryBlockRecoveryMultiplier { get; set; } = 1f;
		public float StoryKnockbackMultiplier { get; set; } = 1f;
		public float StoryProjectileSpeedMultiplier { get; set; } = 1f;
		public float StoryProjectileDamageMultiplier { get; set; } = 1f;
		public float StoryGlideSpeedMultiplier { get; set; } = 1f;
		/// <summary>Story-only "GlideDuration" minors (e.g. Pocahontas wr2) lengthen glide windows.</summary>
		public float StoryGlideDurationMultiplier { get; set; } = 1f;
		/// <summary>Story-only "ZoneRadius" minors (Einstein u2, Leonardo a2, Cleopatra dm1) widen authored ability zones.</summary>
		public float StoryZoneRadiusMultiplier { get; set; } = 1f;
		/// <summary>Story-only "ZoneDuration" minors (Cleopatra dm2) lengthen authored ability zones.</summary>
		public float StoryZoneDurationMultiplier { get; set; } = 1f;
		public float StoryPersistentDurationMultiplier { get; set; } = 1f;
		public float StoryPersistentRangeMultiplier { get; set; } = 1f;
		public float StoryPersistentHealthMultiplier { get; set; } = 1f;
		public float StoryStatusDurationMultiplier { get; set; } = 1f;
		/// <summary>Damaging-status (Venom/RadiantBurn) potency multiplier.</summary>
		public float StoryStatusIntensityMultiplier { get; set; } = 1f;
		public float StoryTemporaryDamageMultiplier { get; private set; } = 1f;
		public float StoryTemporarySpeedMultiplier { get; private set; } = 1f;
		// === Package 11 A4 (Resonance V7.6) — new resolver lanes ===
		/// <summary>Scales the V7.1 Rally echo fraction on damage taken (Story-only).</summary>
		public float StoryRallyEchoFractionMultiplier { get; set; } = 1f;
		/// <summary>Scales Influence-meter accrual (Story-only, Mozart's Minor Resonance).</summary>
		public float StoryUltimateBuildRateMultiplier { get; set; } = 1f;
		/// <summary>Scales damage dealt to Chronal Extractors (Story-only).</summary>
		public float StoryExtractorDamageMultiplier { get; set; } = 1f;
		/// <summary>
		/// V7.6 ability-scoped Resonance lanes, keyed (statKey, abilityScope).
		/// One dictionary rather than six-times-N fields; read it through
		/// <see cref="StoryScoped"/>, never directly. Story-only — Fighter
		/// loadouts leave it empty.
		/// </summary>
		public FTT.Environment.ScopedStoryStats StoryScopedStats { get; set; }
			= FTT.Environment.ScopedStoryStats.Empty;

		/// <summary>
		/// Reads an ability-scoped Resonance lane, e.g.
		/// <c>StoryScoped("AbilityRange", "relativity_rift")</c>. Returns the
		/// neutral 1.0 when the node is unauthored or not unlocked.
		/// </summary>
		public float StoryScoped(string statKey, string abilityScope, float fallback = 1f) =>
			StoryScopedStats.Get(statKey, abilityScope, fallback);

		/// <summary>
		/// Unlocked Resonance major-perk keys (Story-only). Populated by
		/// CharacterFactory from the active save; always empty in Fighter Mode.
		/// </summary>
		public HashSet<string> StoryAbilityPerks { get; } = new(StringComparer.Ordinal);
		public bool HasStoryPerk(string abilityModifierKey) =>
			!string.IsNullOrWhiteSpace(abilityModifierKey) && StoryAbilityPerks.Contains(abilityModifierKey);

		// === Package 11 A5 region: V7.5 Legacy Unlock ability gate ==========
		// Story-only. The locks are INSTALLED by CharacterFactory on the
		// applyStoryProgression: true path alone, so Fighter Mode, the hub
		// Holodeck, the Calibration Drills and the Mirror Paradox clone — all of
		// which travel applyStoryProgression: false — keep the full normalized
		// kit and never see this state. The default is "no locks": a controller
		// nobody gated behaves exactly as it did before this package.

		private HashSet<FTT.Core.AbilitySlot> _legacyUnlockedSlots;

		/// <summary>True while the Legacy Unlock Schedule is gating this controller.</summary>
		public bool LegacyAbilityLocksActive => _legacyUnlockedSlots != null;

		/// <summary>
		/// Installs the Story ability gate. Called once by
		/// <c>CharacterFactory</c> with the slots the active save has earned;
		/// publishes the Dormant/Clear slot-lock states A8 renders. Passing null
		/// clears the gate (full kit).
		/// </summary>
		public void ApplyLegacyUnlockLocks(IEnumerable<FTT.Core.AbilitySlot> unlockedSlots) {
			if (unlockedSlots == null) {
				_legacyUnlockedSlots = null;
				return;
			}
			_legacyUnlockedSlots = new HashSet<FTT.Core.AbilitySlot>(unlockedSlots);
			PublishAbilitySlotLockStates();
		}

		/// <summary>
		/// Republishes every gated slot's lock state. Public so a HUD rebuilt
		/// mid-level (room transition, resume) can be refreshed without a respawn.
		/// </summary>
		public void PublishAbilitySlotLockStates() {
			if (PlayerIndex != 0) return;
			foreach (FTT.Core.AbilitySlot slot in FTT.Core.LegacyUnlockSchedule.GatedSlots) {
				FTT.Core.EventBus.Instance?.RaiseAbilitySlotLockChanged(new FTT.Core.AbilitySlotLockPayload {
					Slot = slot,
					State = IsAbilityUnlocked(slot)
						? FTT.Core.AbilitySlotLockState.Clear
						: FTT.Core.AbilitySlotLockState.Dormant
				});
			}
		}

		/// <summary>
		/// V7.5: is this slot's ability available yet? Always true when no gate
		/// is installed, and always true for anything the schedule does not gate
		/// (the basics, Block, Rally, the rewind, the meter and Defy History are
		/// never locked).
		/// </summary>
		public bool IsAbilityUnlocked(FTT.Core.AbilitySlot slot) {
			if (_legacyUnlockedSlots == null) return true;
			if (FTT.Core.LegacyUnlockSchedule.MilestoneLevelFor(slot) <= 0) return true;
			return _legacyUnlockedSlots.Contains(slot);
		}

		// === end Package 11 A5 region (gate declaration) ===

		// === Package 11 A1b region (V7.6 defensive contract D01-D04) ==========

		// Story-only perk shield (Wardenclyffe Shield, Henry's Bastion, Royal
		// Aegis, Leaf Barrier): a finite HP barrier absorbed BEFORE ordinary
		// block (D01 / D02b), never after it. Fighter Mode never reads these
		// fields because the deterministic simulation is authoritative there.
		private FTT.Combat.StoryShieldInstance _storyShield;

		public float StoryShieldPoints => _storyShield.Points;
		public float StoryShieldCapacity => _storyShield.Capacity;
		public FTT.Combat.StoryShieldEffect StoryShieldEffectId => _storyShield.EffectId;
		/// <summary>Remaining D02c lifetime in active ticks; -1 for Wardenclyffe.</summary>
		public int StoryShieldRemainingFrames => _storyShield.RemainingFrames;

		/// <summary>
		/// V7.6 D02a/D02c: grant or REFRESH one finite HP barrier.
		///
		/// <para>A repeat grant of the same effect sets remaining absorption to
		/// that effect's normal cap and restarts its authored duration. It never
		/// adds capacity and never adds duration — a 10-HP shield with 4 left
		/// returns to 10, not 14 and not 20. A full shield may refresh its
		/// timer.</para>
		///
		/// <para><paramref name="grantEventId"/> is D02a's unique grant-event
		/// identity: a refused input, polling the active condition, a duplicate
		/// animation or collision callback, a recreated visual or restored state
		/// reuses the same ID and is rejected outright. Pass 0 only when the
		/// caller genuinely cannot deduplicate.</para>
		///
		/// <para>The grant itself confers no HP, Rally or damage-based meter.</para>
		/// </summary>
		public bool GrantStoryShield(
			FTT.Combat.StoryShieldEffect effect,
			float capacity,
			int lifetimeFrames = FTT.Combat.StoryDefenseRules.GrantedShieldLifetimeFrames,
			int grantEventId = 0) {
			if (effect == FTT.Combat.StoryShieldEffect.None || capacity <= 0f) return false;
			if (grantEventId != 0
				&& _storyShield.EffectId == effect
				&& _storyShield.GrantEventId == grantEventId) {
				return false;
			}
			_storyShield = new FTT.Combat.StoryShieldInstance {
				EffectId = effect,
				Capacity = MathF.Max(0f, capacity),
				Points = MathF.Max(0f, capacity),
				RemainingFrames = lifetimeFrames,
				GrantEventId = grantEventId
			};
			return true;
		}

		/// <summary>
		/// Wardenclyffe only (D02d). The shield is a CONTINUOUSLY RECHARGING
		/// pool with no eight-second lifetime, so it is installed at zero charge
		/// and filled by <see cref="RechargeStoryShield"/> at the authored rate.
		/// Re-entering a coil radius is a condition, not a repeat D02a grant,
		/// so this never refills.
		/// </summary>
		public void ConfigureStoryShield(float capacity) {
			float resolved = MathF.Max(0f, capacity);
			if (_storyShield.EffectId != FTT.Combat.StoryShieldEffect.Wardenclyffe) {
				// First acquisition initialises to ZERO charge with no delay; it
				// builds at the normal rate and never starts full.
				_storyShield = new FTT.Combat.StoryShieldInstance {
					EffectId = FTT.Combat.StoryShieldEffect.Wardenclyffe,
					Capacity = resolved,
					Points = 0f,
					RemainingFrames = FTT.Combat.StoryShieldInstance.NoExpiry,
					GrantEventId = 0
				};
				return;
			}
			_storyShield.Capacity = resolved;
			_storyShield.Points = MathF.Min(_storyShield.Points, resolved);
		}

		public void RechargeStoryShield(float amount) {
			if (_storyShield.Capacity <= 0f || amount <= 0f) return;
			_storyShield.Points = MathF.Min(_storyShield.Capacity, _storyShield.Points + amount);
		}

		/// <summary>
		/// D02d: while Tesla stands outside every owned coil radius the stored
		/// charge is PRESERVED but absorption and recharge are disabled. Set by
		/// <c>TeslaWardenclyffe</c> every frame; irrelevant to every other
		/// barrier, which is always enabled while it lives.
		/// </summary>
		public bool WardenclyffeInCoilRange { get; set; }

		/// <summary>
		/// D02d: remaining seconds of the three-second recharge delay, armed
		/// whenever damage actually reduces Tesla's HP or Wardenclyffe
		/// absorption. Preserved across same-attempt recovery/load so a reload
		/// cannot accelerate recovery.
		/// </summary>
		public float WardenclyffeDamageDelaySeconds { get; set; }

		/// <summary>True when the live barrier may absorb right now (D02d range gate).</summary>
		private bool StoryShieldAbsorbing =>
			_storyShield.IsActive
			&& (_storyShield.EffectId != FTT.Combat.StoryShieldEffect.Wardenclyffe || WardenclyffeInCoilRange);

		/// <summary>
		/// V7.6 D02e: Temporal Aegis — a ONE-HIT bubble with no time expiry and
		/// at most one active per recipient. An active flag, deliberately not a
		/// growing charge count. A pickup while already protected is consumed
		/// with no second charge, reserve, duration, HP, meter or replacement
		/// reward, which is exactly what an idempotent set expresses.
		/// </summary>
		public bool HasTemporalAegis => _temporalAegisActive;

		public void GrantTemporalAegis() => _temporalAegisActive = true;

		public void ClearTemporalAegis() => _temporalAegisActive = false;

		private bool _temporalAegisActive;

		/// <summary>
		/// V7.6 D04: true while a Defy survivor is protected — through the
		/// presentation (awaiting control) and for the 60 resumed control ticks.
		/// The gate rejects damaging hits, damaging status ticks and damaging
		/// stage hazards BEFORE Aegis, HP barriers and block; a rejected contact
		/// spends no protection, applies no effects and grants no reward.
		/// It is not immunity to non-hit deaths (pits, Integrity zero).
		/// </summary>
		public bool IsDefyProtected => _defyProtectionAwaitControl || _defyProtectionFrames > 0;

		/// <summary>Remaining protected ticks once control has resumed. Test seam.</summary>
		public int DefyProtectionFramesRemaining => _defyProtectionFrames;

		/// <summary>True while the survivor is still inside the Defy presentation.</summary>
		public bool DefyProtectionAwaitingControl => _defyProtectionAwaitControl;

		private bool _defyProtectionAwaitControl;
		private int _defyProtectionFrames;
		// Set for the duration of one hit's resolution when that hit fired Defy:
		// the defied blow may not launch, stun or re-lock the survivor it spared.
		private bool _defySuppressHitEffects;

		/// <summary>
		/// F10 persistence: "once per level" means once per ATTEMPT. Restored by
		/// <c>StoryManager.ApplyResumedAttemptState</c> so a mid-level quit and
		/// resume, or a death rewind that rebuilds the player, cannot hand the
		/// run a second saved life. Only a fresh entry or a full Restart Level
		/// resets it.
		/// </summary>
		public void SetStoryDefyHistoryUsed(bool used) => _storyDefyHistoryUsed = used;

		/// <summary>
		/// F10 checkpoint reconstruction clears the transient protection and the
		/// pending presentation while retaining committed HP, meter and the
		/// spent use. Loading never replays the proc or grants a fresh second.
		/// </summary>
		public void ClearDefyProtection() {
			_defyProtectionAwaitControl = false;
			_defyProtectionFrames = 0;
		}

		/// <summary>
		/// The F13 seal read-model, derived from authoritative meter / Defy-used
		/// / life state at the end of the gameplay update. Never persisted as a
		/// separate cosmetic flag; recomputed on gain, spend, drain, proc,
		/// death/respawn, load and rollback.
		/// </summary>
		public FTT.Core.DefySealState DefySeal =>
			CurrentState == CharacterState.Dead || CurrentState == CharacterState.Respawning
				? FTT.Core.DefySealState.Barred
				: _storyDefyHistoryUsed
					? FTT.Core.DefySealState.Spent
					: CurrentUltimateMeter >= FTT.Combat.UltimateMeter.MaxValue
						? FTT.Core.DefySealState.Ready
						: FTT.Core.DefySealState.Building;

		private FTT.Core.DefySealState _publishedDefySeal = (FTT.Core.DefySealState)(-1);

		/// <summary>
		/// Publishes the seal only when it actually changes, so the HUD never
		/// sees a per-frame storm. Called at the end of the gameplay update.
		/// </summary>
		private void PublishDefySealIfChanged() {
			FTT.Core.DefySealState state = DefySeal;
			if (state == _publishedDefySeal) return;
			_publishedDefySeal = state;
			FTT.Core.EventBus.Instance?.RaiseDefySealChanged(new FTT.Core.DefySealPayload {
				PlayerIndex = PlayerIndex,
				State = state
			});
		}

		/// <summary>
		/// The ONE ordered resolution of the D01/D02b protection stack for one
		/// distinct eligible hit contact, shared by the hurtbox path, DoT ticks
		/// and environmental hazards.
		///
		/// <para>Order: Temporal Aegis, then the finite HP barrier. Invulnerability
		/// and projectile immunity are checked by the callers before this runs,
		/// because they are per-source. Resolution stops at the first layer that
		/// fully prevents the hit; only the remainder continues.</para>
		///
		/// <para>D03c boundary: a zero-damage warning, a non-damaging status and
		/// a harmless overlap consume no Aegis and no barrier HP.</para>
		///
		/// <para>Returns the damage still to resolve. A return of 0 with a
		/// positive <paramref name="damage"/> means the contact was fully
		/// prevented and D03a/D03e suppress its attached effects entirely.</para>
		/// </summary>
		private int ResolveProtectionLayers(int damage, bool bypassesFiniteShields = false) {
			if (damage <= 0 || bypassesFiniteShields) return damage;
			if (_temporalAegisActive) {
				_temporalAegisActive = false;
				return 0;
			}
			if (!StoryShieldAbsorbing) return damage;
			int absorbed = Math.Min(damage, (int)MathF.Floor(_storyShield.Points));
			if (absorbed <= 0) return damage;
			_storyShield.Points -= absorbed;
			// D02d: losing capacity restarts Tesla's recharge delay even when no
			// HP was lost and no attached effect landed.
			if (_storyShield.EffectId == FTT.Combat.StoryShieldEffect.Wardenclyffe) {
				WardenclyffeDamageDelaySeconds = FTT.Combat.StoryDefenseRules.WardenclyffeDamageDelaySeconds;
			}
			// A broken shield disappears immediately and needs a new valid grant.
			if (_storyShield.Points <= 0f) ClearStoryShield();
			return damage - absorbed;
		}

		private void ClearStoryShield() => _storyShield = default;

		/// <summary>
		/// D02c/D04 clocks. Both use the suspended-effect clock — Time Freeze,
		/// hitstop and world-frozen presentations consume neither lifetime nor a
		/// protected tick, and neither awards catch-up ticks.
		/// </summary>
		private void TickStoryDefense() {
			if (IsInHitstop || TimeFrozen) return;
			if (_storyShield.RemainingFrames > 0) {
				_storyShield.RemainingFrames--;
				// Expiry discards the remaining absorption with no heal, meter,
				// block event or expiry proc.
				if (_storyShield.RemainingFrames <= 0) ClearStoryShield();
			}
			if (WardenclyffeDamageDelaySeconds > 0f) {
				WardenclyffeDamageDelaySeconds = MathF.Max(
					0f, WardenclyffeDamageDelaySeconds - 1f / 60f);
			}
			if (_defyProtectionAwaitControl) {
				// The window opens at the first RESUMED NORMAL-CONTROL tick, not
				// on the player's first input and not while stunned or dead.
				bool actionable = CurrentState != CharacterState.Dead
					&& CurrentState != CharacterState.Respawning
					&& CurrentState != CharacterState.Stunned
					&& CurrentState != CharacterState.Dazed;
				if (!actionable) return;
				_defyProtectionAwaitControl = false;
			}
			if (_defyProtectionFrames > 0) _defyProtectionFrames--;
		}

		// === end Package 11 A1b region (defensive state) =====================

		// Story-only projectile immunity (Virtuoso Dash, Rest Shield, ...): while
		// active, hits whose HitboxID is "projectile" (the placeholder-projectile
		// hit path) are absorbed outright. Granting perks refresh the window each
		// frame; Fighter Mode never reads this because the deterministic
		// simulation is authoritative there.
		public bool HasStoryProjectileImmunity => _storyProjectileImmunityFrames > 0;

		public void GrantStoryProjectileImmunity(float durationSeconds) {
			_storyProjectileImmunityFrames = Math.Max(
				_storyProjectileImmunityFrames,
				Mathf.RoundToInt(durationSeconds * 60f));
		}

		private int _storyProjectileImmunityFrames;
		/// <summary>
		/// Story-only timed hyper-armor granted by perks (Homestead Bulwark, Joan's
		/// Unstoppable Crusade window). While active the player takes damage but
		/// ignores hitstun/knockback from non-ultimate hits, matching the
		/// phase-based ability hyper-armor rules. Fighter Mode never reads this.
		/// </summary>
		public bool StoryHyperArmorActive => _storyHyperArmorFrames > 0;

		public void ApplyStoryHyperArmor(float durationSeconds) {
			_storyHyperArmorFrames = Math.Max(
				_storyHyperArmorFrames,
				Mathf.RoundToInt(durationSeconds * 60f));
		}
		/// <summary>
		/// Remaining float-glide time granted by movement abilities (Einstein's
		/// Relativity Warp cancel). While positive, gravity is heavily reduced.
		/// </summary>
		public float StoryFloatTimer { get; set; }
		public bool IsPostRewindInvulnerable => _postRewindInvulnerabilityFrames > 0;
		/// <summary>
		/// Encounter-scoped HP pool that replaces the character baseline entirely.
		/// Set only by boss encounters that reuse a character body — the Level 13
		/// Mirror Paradox and its 1000-HP <c>BossData</c> pool. Zero (the default)
		/// keeps the normal <c>CharacterData</c> + Resonance baseline. This is not a
		/// Resonance bonus and never participates in Story stat resolution.
		/// </summary>
		public int EncounterMaxHPOverride { get; set; }
		/// <summary>
		/// Story-side opponent marker, set only by encounters that reuse a character
		/// body as a hostile (the Level 13 Mirror Paradox clone). Projectiles this
		/// controller fires join the <c>enemy_projectile</c> group so a Chronal
		/// Rewind's world clear removes them like any other hostile shot (audit
		/// H-8). Never set on the campaign avatar; Fighter Mode never reads it.
		/// </summary>
		public bool IsStoryHostile { get; set; }
		public int MaximumHP => EncounterMaxHPOverride > 0
			? EncounterMaxHPOverride
			: (Data?.MaxHP ?? 100) + StoryMaxHPBonus;
		public int MaximumBlockCharges => (Data?.MaxBlockCharges ?? 3) + StoryBlockChargeBonus;
		private float EffectiveMoveSpeed => (Data?.MaxMoveSpeed ?? 8f) * StoryMoveSpeedMultiplier
			* StoryTemporarySpeedMultiplier * EnvironmentMoveMultiplier;
		private float EffectiveJumpForce => (Data?.MaxJumpForce ?? 14f) * StoryJumpForceMultiplier;

		// Physics constants
		private const float BaseGravity = 18.0f;
		private const float FallGravityMultiplier = 1.8f;
		private const float ShortHopGravityMultiplier = 2.5f;
		private const float DirectionReversalPenalty = 0.7f;
		private const float TerminalVelocity = 600.0f;
		private const float GroundRampFrames = FTT.Core.UniversalMovementRules.RunAccelerationFrames;
		/// <summary>
		/// Grounded stop ramp (2026-08-10 feel batch §2.1). Every site that bleeds
		/// horizontal speed toward zero on the ground — idle/skid/crouch settling,
		/// the block stance, roll startup/recovery, and a released or reversed
		/// stick while running — uses this instead of the accel ramp.
		/// </summary>
		private const float GroundDecelRampFrames = FTT.Core.UniversalMovementRules.RunDecelerationFrames;
		private const float AirAccelRampFrames = 4.0f;
		private const float AirDecelRampFrames = 8.0f;
		/// <summary>Story pixel-space fast-fall speed: <c>FastFallSpeed</c> units/s × 60 (M01: 1200 px/s).</summary>
		private const float FastFallSpeedPixels = FTT.Core.UniversalMovementRules.FastFallSpeed * 60f;

		// Timers
		private float _coyoteTimer;
		private const float CoyoteTime = 0.1f;
		private float _jumpBufferTimer;
		private const float JumpBufferTime = 0.1f;
		private float _skidTimer;
		private const float SkidDuration = 0.05f;
		private float _ledgeHangTimer;
		private const float LedgeHangMaxTime = 5.0f;
		private float _stunTimer;
		private float _dazeTimer;
		private const float DazeDuration = 1.0f;
		private float _respawnTimer;
		private float _dropThroughTimer;
		private const float DropThroughDuration = 0.25f;
		private int _dropThroughFramesRemaining;
		private int _downTapFramesRemaining;
		private int _temporaryDamageBuffFrames;
		private int _temporarySpeedBuffFrames;
		private int _postRewindInvulnerabilityFrames;
		private int _storyHyperArmorFrames;

		// === V7.1 verb layer (hitstop / DI / landing tech) ===
		// Numbers live in FTT.Combat.BasicComboRules; the deterministic sim's
		// FighterVerbComponent carries the same state on the fighter side.
		// The freeze counts physics frames (not seconds) so it can never drift
		// against the 60 Hz clock the shared frame tables are authored in.
		private int _hitstopFramesRemaining;
		private Vector2 _pendingLaunch;
		private bool _hasPendingLaunch;
		private bool _stunTumble;
		// V7.3 hit-2 cancel gate: true while the current hitstun came from
		// string hit 1 ("combo_1") and cannot be block-cancelled.
		private bool _hitstunBlockCancelBlocked;
		private float _techInvulnerabilitySeconds;
		// V7.3: the 12-frame tech recovery is a LOCKED window — in place, no
		// actions, no movement — mirroring the sim's TechLockoutFrames.
		private float _techLockoutSeconds;
		// Per-stun latch: the launch actually left the ground, so the next
		// grounded frame is a genuine landing (the tech's trigger).
		private bool _stunLeftTheGround;
		// M05 knockdown and get-up (Package 12 W3b), the Story mirror of the
		// sim's FighterKnockdownComponent (Klotho 320). Both run as sub-phases
		// of CharacterState.Stunned and count physics frames. Meaningful only
		// while CurrentState is Stunned; ApplyStun clears them.
		private int _knockdownFrames;
		private int _getUpKind;
		private int _getUpFrames;
		private int _getUpDirection;

		// === V7.1 Rally / Desperation Resonance / Defy History (Story side) ===
		// The Fighter sim carries the identical state on FighterVerbComponent;
		// fractions and the drain window live in BasicComboRules. Enemies do
		// not rally — this state exists only on the player.
		private float _echoPool;
		private float _echoDrainPerFrame;
		private bool _storyDefyHistoryUsed;
		// Set by ApplyDamage when Defy History fires, read (and cleared) by
		// OnHurtboxHit: a defied hit generates neither echo nor victim meter —
		// the shattered meter consumed the entire blow.
		private bool _defyFiredThisHit;

		// === V7.6 Echo Step (Story side) ===
		// The 31-sample per-tick position ring, mirroring the sim's component bank
		// (IDs 313-317). One sample per physics tick, recorded BEFORE action input
		// and movement, and the destination is the EXACT t-30 sample — never a
		// nearest-sample approximation. The retired V7.1 version kept 5 samples
		// every 6 frames, so its destination drifted between 24 and 30 frames back
		// depending on phase. See docs/design-contracts/TEMPORAL_STATE_CONTRACT.md.
		private readonly Vector2[] _echoStepRing =
			new Vector2[FTT.Combat.BasicComboRules.EchoStepRingSamples];
		/// <summary>Next slot to overwrite. With 31 slots it is also the t-30 slot.</summary>
		private int _echoStepRingHead;
		/// <summary>Real samples since the last generation reset; spawn fill is not history.</summary>
		private int _echoStepRingValidCount;
		/// <summary>Bumped on every forced relocation (spawn, respawn, death rewind, checkpoint).</summary>
		private int _echoStepHistoryGeneration;
		private int _echoStepWindupFrames;
		private int _echoStepCooldownFrames;
		private bool _echoStepArmed;
		private Vector2 _echoStepDestination;

		/// <summary>The current Echo Step history generation. Test seam.</summary>
		public int EchoStepHistoryGeneration => _echoStepHistoryGeneration;

		/// <summary>Real samples in the current generation. Test seam.</summary>
		public int EchoStepRingValidCount => _echoStepRingValidCount;

		/// <summary>The armed destination while a wind-up runs. Test seam.</summary>
		public Vector2 EchoStepDestination => _echoStepDestination;

		/// <summary>
		/// Opens a new Echo Step history generation at the current position: storage
		/// is filled with that coordinate, but only real subsequent samples count, so
		/// the player must live 30 further ticks before an exact t-30 position
		/// exists. Called at spawn and at every forced relocation — a death rewind
		/// landing or a checkpoint reconstruction must never leave a destination
		/// pointing into the timeline the player was just pulled out of.
		/// </summary>
		public void ResetEchoStepHistory() {
			for (int index = 0; index < _echoStepRing.Length; index++) {
				_echoStepRing[index] = GlobalPosition;
			}
			_echoStepRingHead = 0;
			_echoStepRingValidCount = 0;
			_echoStepHistoryGeneration++;
			_echoStepArmed = false;
			_echoStepWindupFrames = 0;
		}

		/// <summary>Frames left on the Echo Step internal cooldown. Test seam.</summary>
		public int EchoStepCooldownFramesRemaining => _echoStepCooldownFrames;

		/// <summary>True while the Echo Step wind-up is running. Test seam.</summary>
		public bool EchoStepWindingUp => _echoStepWindupFrames > 0;

		// === V7.1 Resonance Momentum (Story side) ===
		private int _momentumRefundsSlotOne;
		private int _momentumRefundsSlotTwo;

		// === V7.2 Grabs & Throws (Story side) ===
		// Phase mirrors the sim's FighterGrabRules: 1 startup · 2 active ·
		// 3 whiff recovery · 4 holding (decision window) · 5 throw animation.
		private int _grabPhase;
		private int _grabPhaseFrames;
		private int _grabThrowDirection;
		private FTT.Enemies.EnemyController _grabbedEnemy;

		/// <summary>Grab reach in Story pixels (0.8 units at 62.5 px/unit).</summary>
		private const float GrabReachPixels = FTT.Combat.BasicComboRules.GrabReachUnits * 62.5f;

		/// <summary>Current grab phase (0 when not grabbing). Test seam.</summary>
		public int GrabPhase => _grabPhase;

		/// <summary>The mob currently held, or null. Test seam.</summary>
		public FTT.Enemies.EnemyController GrabbedEnemy => _grabbedEnemy;

		/// <summary>Reclaimable Rally echo remaining, in HP. Test seam / HUD.</summary>
		public float EchoPool => _echoPool;

		/// <summary>Whether Defy History already fired this level. Test seam.</summary>
		public bool StoryDefyHistoryUsed => _storyDefyHistoryUsed;

		// Jump tracking
		private bool _jumpHeld;
		private bool _wasGrounded;

		// Crouching
		private float _normalHurtboxHeight;
		private Vector2 _normalBodyShapePosition;
		private Vector2 _normalHurtboxSize;
		private Vector2 _normalHurtboxPosition;

		// Drop-through
		private PhysicsBody2D _dropThroughPlatform;

		// Node references
		private AnimatedSprite2D _animatedSprite;
		private CollisionShape2D _collisionShape;
		private Area2D _ledgeDetector;
		private AnimationPlayer _combatAnimationPlayer;
		private Marker2D _aerialHitboxMarker;
		private FTT.Combat.GlowPresentationController _glow;
		private FTT.Environment.LedgeGrabPoint _activeLedge;
		// V7.3 ledge rules, mirroring the sim: a per-airtime grab budget
		// (LedgeRegrabsPerAirtime, reset on floor contact) and a short regrab
		// lockout armed when a trump forces this player off a ledge.
		private int _ledgeGrabsThisAirtime;
		private float _ledgeRegrabLockoutSeconds;

		// Combat wiring
		private FTT.Combat.BaseSpecial _special1;
		private FTT.Combat.BaseSpecial _special2;
		private FTT.Combat.BaseSpecial _movementAbility;
		private FTT.Combat.BaseSpecial _ultimate;
		private FTT.Combat.UltimateMeter _ultimateMeter;
		private FTT.Combat.BlockSystem _blockSystem;
		private FTT.Combat.StatusController _statusController;
		private FTT.Combat.Hurtbox _hurtbox;
		private CollisionShape2D _hurtboxShape;
		private FTT.Combat.CombatantPushbox _pushbox;
		private FTT.Combat.Hitbox _meleeHitbox;
		private uint _rewindCollisionLayer;
		private uint _rewindCollisionMask;
		private bool _rewindSuspended;
		private int _rollFrame;
		private int _rollDirection;
		private bool _rollInvulnerable;
		private bool _hyperArmorPresentationActive;
		private bool _spawnInvulnerabilityPresentationActive;

		// === Package 8 B5: footsteps ===
		// Presentation only; nothing here is read by movement, combat, or rewind.

		/// <summary>
		/// Surface the character is standing on, used to pick a footstep sound.
		/// Levels set this from their geometry as production audio lands; until then
		/// every surface resolves to the single placeholder step through
		/// <c>AudioManager.PlayFootstep</c>'s fallback.
		/// </summary>
		public string FootstepSurfaceID = "";

		private readonly FTT.Core.FootstepCadence _footsteps = new();

		// Basic attack timing
		private int _attackFramesRemaining;
		private float _attackFrameProgress;
		private const int ComboBufferFrames = FTT.Combat.StoryCombatRules.ComboBufferFrames;
		private int _comboBufferFramesRemaining;
		private bool _comboBufferActive;
		private bool _nextAttackBuffered;
		private bool _attackHitActive;
		private bool _attackInRecovery;
		private bool _inRecoveryHold;
		private int _pendingSpecialSlot;
		private bool _attackStartedAerial;
		private bool _attackStartedCrouched;
		private bool _attackAnimationDriven;
		/// <summary>
		/// Which basic the running swing is (gameplay feel §2.8):
		/// <c>BasicComboRules.VariantChain</c>, <c>VariantUpAttack</c>, or
		/// <c>VariantDownAir</c>. The two directional strikes are single swings
		/// outside the chain — they never buffer, hold, or advance a counter.
		/// </summary>
		private int _attackVariant = FTT.Combat.BasicComboRules.VariantChain;
		private bool _specialStartedAerial;
		private bool _ultimateStartedAerial;

		// Built from FTT.Combat.BasicComboRules — the one shared rulebook with
		// the Fighter simulation. Do not author numbers here.
		private static readonly FTT.Combat.CombatFrameTimeline[] ComboTimelines = BuildComboTimelines(
			FTT.Combat.BasicComboRules.GroundStartupFrames,
			FTT.Combat.BasicComboRules.GroundActiveFrames,
			FTT.Combat.BasicComboRules.GroundRecoveryFrames);
		private static readonly FTT.Combat.CombatFrameTimeline[] AerialComboTimelines = BuildComboTimelines(
			FTT.Combat.BasicComboRules.AerialStartupFrames,
			FTT.Combat.BasicComboRules.AerialActiveFrames,
			FTT.Combat.BasicComboRules.AerialRecoveryFrames);

		private static FTT.Combat.CombatFrameTimeline[] BuildComboTimelines(
			int[] startup, int[] active, int[] recovery) {
			var timelines = new FTT.Combat.CombatFrameTimeline[startup.Length];
			for (int index = 0; index < startup.Length; index++) {
				timelines[index] = new(startup[index], active[index], recovery[index]);
			}
			return timelines;
		}
		// V7.1 per-character string profile (BasicComboRules.StringProfileFor):
		// authored opener/finisher startups, damage shape (tenths, sum 33), and
		// reach scale. Resolved from CharacterData in _Ready; the instance
		// timelines swap the template startups for the authored ones while
		// active/recovery frames stay universal. Damage multipliers come from
		// the profile — do not author a second copy here.
		private FTT.Combat.BasicStringProfile _stringProfile =
			FTT.Combat.BasicComboRules.TemplateStringProfile;
		private FTT.Combat.CombatFrameTimeline[] _groundComboTimelines = ComboTimelines;
		private FTT.Combat.CombatFrameTimeline[] _aerialComboTimelines = AerialComboTimelines;
		private static readonly Vector2[] ComboHitboxSizes = {
			new(72f, 60f),
			new(84f, 72f),
			new(108f, 84f)
		};
		private static readonly Vector2[] ComboHitboxOffsets = {
			new(60f, -32f),
			new(60f, -20f),
			new(72f, -32f)
		};
		private static readonly Vector2[] AerialComboHitboxSizes = {
			new(72f, 64f),
			new(82f, 82f),
			new(88f, 104f)
		};
		private static readonly Vector2[] AerialComboHitboxOffsets = {
			new(58f, -46f),
			new(42f, -50f),
			new(20f, -12f)
		};

		// Directional attacks (§2.8). Frames come from the shared rulebook; the
		// pixel boxes mirror the simulation's world-unit reaches at the
		// repository's 62.5 px/unit convention (up-attack |dx| <= 1.2 and 2.4
		// above the origin; down-air |dx| <= 1.0 and 2.0 below). Offsets are
		// facing-independent — these strikes are centred on the fighter.
		private static readonly FTT.Combat.CombatFrameTimeline UpAttackTimeline = new(
			FTT.Combat.BasicComboRules.UpAttackStartupFrames,
			FTT.Combat.BasicComboRules.UpAttackActiveFrames,
			FTT.Combat.BasicComboRules.UpAttackRecoveryFrames);
		private static readonly FTT.Combat.CombatFrameTimeline DownAirTimeline = new(
			FTT.Combat.BasicComboRules.DownAirStartupFrames,
			FTT.Combat.BasicComboRules.DownAirActiveFrames,
			FTT.Combat.BasicComboRules.DownAirRecoveryFrames);
		private static readonly Vector2 UpAttackHitboxSize = new(150f, 150f);
		private static readonly Vector2 UpAttackHitboxOffset = new(0f, -75f);
		private static readonly Vector2 DownAirHitboxSize = new(125f, 125f);
		private static readonly Vector2 DownAirHitboxOffset = new(0f, 62.5f);

		public override void _Ready() {
			AddToGroup("StoryPlayer");
			if (Data != null) {
				CurrentHP = MaximumHP;
				CurrentBlockCharges = MaximumBlockCharges;
				RemainingJumps = Data.MaxJumpCount;
			}
			_stringProfile = FTT.Combat.BasicComboRules.StringProfileFor(Data?.CharacterID);
			_groundComboTimelines = BuildComboTimelines(
				_stringProfile.GroundStartupFrames,
				FTT.Combat.BasicComboRules.GroundActiveFrames,
				FTT.Combat.BasicComboRules.GroundRecoveryFrames);
			_aerialComboTimelines = BuildComboTimelines(
				_stringProfile.AerialStartupFrames,
				FTT.Combat.BasicComboRules.AerialActiveFrames,
				FTT.Combat.BasicComboRules.AerialRecoveryFrames);

			_animatedSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
			_collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
			_ledgeDetector = GetNodeOrNull<Area2D>("LedgeDetector");
			_combatAnimationPlayer = GetNodeOrNull<AnimationPlayer>("CombatAnimationPlayer");
			_aerialHitboxMarker = GetNodeOrNull<Marker2D>("AerialHitboxMarker");
			_glow = GetNodeOrNull<FTT.Combat.GlowPresentationController>(
				FTT.Combat.GlowPresentationController.NodeName);

			if (_collisionShape?.Shape is RectangleShape2D rect) {
				_normalHurtboxHeight = rect.Size.Y;
				_normalBodyShapePosition = _collisionShape.Position;
			}

			if (_ledgeDetector != null) {
				_ledgeDetector.AreaEntered += OnLedgeAreaEntered;
			}
			if (_combatAnimationPlayer != null) {
				_combatAnimationPlayer.AnimationFinished += OnCombatAnimationFinished;
			}

			MotionMode = MotionModeEnum.Grounded;
			UpDirection = Vector2.Up;
			FloorStopOnSlope = true;

			InitializeCombatNodes();
		}

		public override void _ExitTree() {
			if (_ledgeDetector != null) _ledgeDetector.AreaEntered -= OnLedgeAreaEntered;
			if (_combatAnimationPlayer != null) _combatAnimationPlayer.AnimationFinished -= OnCombatAnimationFinished;
			if (_hurtbox != null) _hurtbox.OnHit -= OnHurtboxHit;
			if (_meleeHitbox != null) _meleeHitbox.HitConfirmed -= OnMeleeHitConfirmed;
			ReleaseActiveLedge();
			if (_dropThroughPlatform != null && IsInstanceValid(_dropThroughPlatform)) {
				RemoveCollisionExceptionWith(_dropThroughPlatform);
			}
		}

		public void InitializeCombatNodes() {
			_special1 = GetNodeOrNull<FTT.Combat.BaseSpecial>("Special1");
			_special2 = GetNodeOrNull<FTT.Combat.BaseSpecial>("Special2");
			_movementAbility = GetNodeOrNull<FTT.Combat.BaseSpecial>("MovementAbility");
			_ultimate = GetNodeOrNull<FTT.Combat.BaseSpecial>("Ultimate");
			_ultimateMeter = GetNodeOrNull<FTT.Combat.UltimateMeter>("UltimateMeter");
			_blockSystem = GetNodeOrNull<FTT.Combat.BlockSystem>("BlockSystem");
			_statusController = GetNodeOrNull<FTT.Combat.StatusController>("StatusController");
			_hurtbox = GetNodeOrNull<FTT.Combat.Hurtbox>("Hurtbox");
			_pushbox = GetNodeOrNull<FTT.Combat.CombatantPushbox>("Pushbox");
			_meleeHitbox = GetNodeOrNull<FTT.Combat.Hitbox>("MeleeHitbox");

			if (_meleeHitbox != null) {
				// Guard against double subscription: InitializeCombatNodes can run
				// again after CharacterFactory adds combat children.
				_meleeHitbox.HitConfirmed -= OnMeleeHitConfirmed;
				_meleeHitbox.HitConfirmed += OnMeleeHitConfirmed;
			}

			if (_hurtbox != null) {
				_hurtbox.OnHit += OnHurtboxHit;
				_hurtboxShape = _hurtbox.GetNodeOrNull<CollisionShape2D>("CollisionShape2D")
					?? (_hurtbox.GetChildCount() > 0 ? _hurtbox.GetChild(0) as CollisionShape2D : null);
				if (_hurtboxShape?.Shape is RectangleShape2D hurtboxRect) {
					_normalHurtboxSize = hurtboxRect.Size;
					_normalHurtboxPosition = _hurtboxShape.Position;
				}
			}
		}

		private float OnHurtboxHit(FTT.Combat.HitPayload hit) => ResolveIncomingHit(hit);

		/// <summary>
		/// M08 (Package 12 W3) <see cref="FTT.Combat.IDamageable"/>: the same
		/// chokepoint the hurtbox feeds, reachable without a hurtbox in hand.
		/// The target index is stamped exactly as <c>Hurtbox.TakeHit</c> does.
		/// </summary>
		public float TakeDamage(in FTT.Combat.HitPayload hit) {
			FTT.Combat.HitPayload routed = hit;
			routed.TargetIndex = PlayerIndex;
			return ResolveIncomingHit(routed);
		}

		/// <inheritdoc/>
		public bool IsAlive => CurrentState != CharacterState.Dead;

		/// <summary>
		/// The ONE Story damage-resolution chokepoint (Package 11 A1b, V7.6
		/// D01/D02b). Per distinct eligible hit contact:
		///
		/// <list type="number">
		/// <item>hit eligibility / invulnerability (dead, roll i-frames, the D04
		/// Defy protected window),</item>
		/// <item>authored projectile immunity,</item>
		/// <item>Temporal Aegis — one hit, consumed whole,</item>
		/// <item>the finite HP barrier,</item>
		/// <item>ordinary charge-based block,</item>
		/// <item>HP and the victim-side pipeline.</item>
		/// </list>
		///
		/// <para>Resolution STOPS at the first layer that fully prevents the
		/// hit. A full absorption spends no block charge, causes no block
		/// response (no shieldstun, shatter, daze or lockout), awards no
		/// successful-block perk and — under D03a/D03e — applies no hitstun,
		/// knockback, status or mark: "holding Block does not turn a shield
		/// absorption into a successful block". A PARTIAL absorption passes only
		/// the remainder to a legal block, charged at the ORIGINAL attack
		/// classification.</para>
		///
		/// <para>Before this the barrier drained inside <see cref="ApplyDamage"/>,
		/// i.e. AFTER block — exactly backwards. A blocking Shakespeare with a
		/// live Bastion spent a charge, and could shatter, on a hit the guard
		/// should have absorbed for free.</para>
		/// </summary>
		private float ResolveIncomingHit(FTT.Combat.HitPayload hit) {
			if (CurrentState == CharacterState.Dead || CurrentState == CharacterState.Respawning) return 0f;
			if (_rollInvulnerable) return 0f;
			// D04: the protected window sits above every other layer. A rejected
			// contact spends no shield capacity or block charge, applies no
			// hitstun, launch or status, and grants no damage, Rally or block
			// reward. Existing statuses are NOT cleansed.
			if (IsDefyProtected) return 0f;
			if (HasStoryProjectileImmunity && hit.HitboxID == "projectile") return 0f;
			bool wasLedgeHanging = CurrentState == CharacterState.LedgeHanging;

			// D02b layers two and three, ahead of block. The class identity and
			// the authored BlockChargeCost are carried through untouched: an
			// absorbed Unblockable is still Unblockable, never relabelled.
			int incoming = Math.Max(0, (int)MathF.Round(hit.Damage));
			int remaining = ResolveProtectionLayers(incoming, hit.BypassesFiniteShields);
			if (incoming > 0 && remaining <= 0) {
				// Fully absorbed: shield-impact feedback, and nothing else. No
				// ResolveHit call, no Henry's Bastion grant, no shieldstun.
				FTT.Core.CameraShake.Instance?.Shake(2f, 0.06f);
				return 0f;
			}
			if (remaining != incoming) {
				// Partial absorption: only the remainder continues, at the
				// original attack classification and charge cost.
				hit.Damage = remaining;
			}

			if (CurrentState == CharacterState.Blocking && _blockSystem != null) {
				FTT.Combat.BlockResult blockResult = _blockSystem.ResolveHit(hit);
				if (blockResult != FTT.Combat.BlockResult.NotBlocked) {
					// Shield of Orleans (Story-only, Joan). V7.6 F06 Option A:
					// a FLAT +5 Influence per DISTINCT successfully blocked
					// attack, not a damage-scaled multiplier. A shattering
					// block still qualifies (a charge was consumed either way);
					// a hold with no absorb, a whiff, a hit from behind, an
					// unblockable and a zero-charge refusal all return
					// NotBlocked above and never reach here. A multi-hit
					// ability, volley, zone or piercing projectile grants at
					// most one, via the execution dedup below.
					if (HasStoryPerk(ShieldOfOrleansPerkKey)) {
						ApplyShieldOfOrleans(hit, blockResult);
					}
					FTT.Core.CameraShake.Instance?.Shake(3f, 0.08f);
					if (blockResult == FTT.Combat.BlockResult.Blocked) {
						// Guard Impact (design haptic table): a successful absorb.
						// A guard break buzzes its own heavier pattern through the
						// OnBlockBroken event instead.
						FTT.Core.HapticFeedbackManager.Instance?.OnGuardImpact(PlayerIndex);
					}
					// V7.1/V7.3 hitstop: a blocked hit freezes for the flat 2
					// frames; a shatter replaces that with the longer shared
					// shatter freeze (the sim mirrors both). Construct/DoT
					// ticks are exempt — blocked or not, they never freeze.
					if (!hit.ExemptFromHitstop) {
						ApplyHitstop(blockResult == FTT.Combat.BlockResult.GuardBroken
							? FTT.Combat.BasicComboRules.ShatterFreezeFrames
							: FTT.Combat.BasicComboRules.BlockedHitstopFrames);
					}
					return 0f;
				}
			}

			int damageApplied = ApplyDamage(Math.Max(0, (int)MathF.Round(hit.Damage)));
			if (damageApplied <= 0) return 0f;

			if (wasLedgeHanging && CurrentState != CharacterState.Dead) {
				ReleaseActiveLedge();
				TransitionTo(CharacterState.Airborne);
			}

			// V7.6 D04: the defied hit imposes no forced motion, no stun and no
			// new attached control/status effect on the survivor it spared. The
			// flag is consumed here, once, by the hit that set it.
			bool defySuppressed = _defySuppressHitEffects;
			_defySuppressHitEffects = false;

			bool hasHyperArmor = HasActiveHyperArmorAgainst(hit.AttackClass);
			if (!hasHyperArmor && !defySuppressed) {
				// An impulse-free hit (construct arcs/bites carry zero
				// knockback) must not replace the velocity — a zero vector
				// would freeze the victim mid-motion.
				// M05 (Package 12 W3b): only an authored launcher launches.
				bool launches = ResolveHitLaunches(in hit);
				if (hit.Knockback != Vector2.Zero) {
					// Low-health knockback scaling (gameplay-feel plan §2.5): the
					// impulse scales with the victim's missing HP *after* this
					// hit's damage, exactly as the Fighter sim scales it.
					Vector2 knockback = FTT.Combat.DamageCalculator.CalculateKnockback(
						hit.Knockback,
						Data?.Weight ?? 1f,
						hit.AttackerFacingRight,
						CurrentHP,
						MaximumHP);
					// Knockback replaces velocity, as the Fighter sim resolves it —
					// a hit imparts the same impulse regardless of prior motion.
					Velocity = knockback * 60f;
					if (launches) {
						// V7.1 DI: a launching hit (impulse + hitstun) stashes its
						// impulse; the held direction at hitstop end bends the angle
						// up to ±15° (the pre-written velocity is inert while frozen).
						if (hit.HitstunDuration > 0f && CurrentState != CharacterState.Dead) {
							_pendingLaunch = Velocity;
							_hasPendingLaunch = true;
						}
					} else if (IsOnFloor()) {
						// M05: a non-launcher on a grounded victim is grounded
						// knockback — a horizontal slide on the floor, no tumble,
						// no DI (mirrors FighterDamageRules.ApplyFighterHit).
						Velocity = new Vector2(Velocity.X, 0f);
					}
					// An airborne victim of a non-launcher keeps the authored
					// vector but gets no tumble, DI or tech.
				}

				if (hit.HitstunDuration > 0f && CurrentState != CharacterState.Dead) {
					// F23: a throw's hitstun rides out in CharacterState.Thrown.
					bool thrown = hit.HitboxID == FTT.Combat.BasicComboRules.ThrowHitboxID;
					ApplyStun(hit.HitstunDuration, thrown);
					// A launched stun is a tumble: the victim may tech the landing.
					// A throw is never techable — its trajectory IS the decision.
					// M05 (Package 12 W3b): only an authored launcher tumbles.
					_stunTumble = !thrown && launches && hit.Knockback != Vector2.Zero;
					// V7.3 hit-2 cancel gate: string hit 1 is never
					// block-cancelable; from hit two on the escape opens.
					_hitstunBlockCancelBlocked = hit.HitboxID == "combo_1";
				}
			}

			// V7.1 hitstop (victim side): scaled by the damage that actually
			// applied; a lethal hit skips — the death presentation owns it.
			// V7.3: construct/DoT ticks are exempt from hitstop entirely.
			if (CurrentState != CharacterState.Dead && !hit.ExemptFromHitstop) {
				ApplyHitstop(FTT.Combat.BasicComboRules.HitstopFrames(damageApplied));
			}
			// A launching hit whose victim ended up with no freeze resolves its
			// DI immediately — a stashed launch must never sit armed waiting to
			// replay under a later hit's hitstop (V7.3, mirrors the sim).
			if (_hasPendingLaunch && _hitstopFramesRemaining <= 0) {
				FTT.Combat.BasicComboRules.ResolveDirectionalInfluence(
					_pendingLaunch.X, _pendingLaunch.Y,
					CurrentInputFrame.Horizontal, CurrentInputFrame.Vertical,
					out float launchX, out float launchY);
				Velocity = new Vector2(launchX, launchY);
				_hasPendingLaunch = false;
			}

			if (hit.AppliedStatus != FTT.Core.StatusType.None && !defySuppressed
				&& CurrentState != CharacterState.Dead) {
				_statusController?.ApplyStatus(hit.AppliedStatus, hit.StatusDuration, hit.StatusIntensity);
			}

			// V7.6 F07: the caster-owned combo mark rides the same hit but is not
			// a status — no slot, no action lock, no stagger budget.
			if (!defySuppressed
				&& hit.ComboMark == FTT.Combat.ComboMarkType.Conductive && hit.ComboMarkFrames > 0) {
				ApplyConductiveMark(hit.AttackerIndex, hit.ComboMarkFrames);
			}

			// V7.1 Rally: a fraction of the hit becomes a briefly reclaimable
			// echo — 20% at full health sliding to 50% near death (evaluated
			// after the damage), difficulty-scaled, never on a lethal hit. Each
			// accrual restarts the 150-frame drain. Meter-from-damage-taken
			// accrues only on the permanent (non-echo) portion here; the echo
			// portion's meter accrues if and when it drains (no double-earning).
			float echoAmount = 0f;
			bool defied = _defyFiredThisHit;
			_defyFiredThisHit = false;
			if (!defied && CurrentState != CharacterState.Dead && CurrentHP > 0 && MaximumHP > 0) {
				float missing = (MaximumHP - CurrentHP) / (float)MaximumHP;
				// Package 11 A4: the V7.6 RallyEchoFraction lane (Einstein,
				// Joan, Shakespeare, Lincoln) scales the echo share alongside
				// the difficulty multiplier. Story-only - the sim is untouched.
				float fraction = (FTT.Combat.BasicComboRules.EchoFractionBase
						+ FTT.Combat.BasicComboRules.EchoFractionSlope * missing)
					* FTT.Core.StoryDifficultyTuning.GetRallyEchoMultiplier(
						FTT.Core.StoryDifficultyTuning.CurrentStoryDifficulty)
					* StoryRallyEchoFractionMultiplier;
				echoAmount = damageApplied * fraction;
				_echoPool += echoAmount;
				_echoDrainPerFrame = _echoPool / FTT.Combat.BasicComboRules.EchoDrainFrames;
			}

			if (!defied) {
				_ultimateMeter?.AddFromDamageTaken(damageApplied - echoAmount);
				CurrentUltimateMeter = _ultimateMeter?.CurrentValue ?? CurrentUltimateMeter;
			}
			float shakeIntensity = hit.ScreenShakeIntensity > 0f
				? hit.ScreenShakeIntensity * 12f
				: damageApplied * 0.25f;
			FTT.Core.CameraShake.Instance?.Shake(shakeIntensity, hit.ScreenShakeDuration);
			_glow?.FlashHit();
			SpawnDamageNumber(damageApplied, hit.HitOrigin);
			return damageApplied;
		}

		private void SpawnDamageNumber(int damage, Vector2 position) {
			FTT.UI.FloatingDamageNumber.Show(damage, position + new Vector2(-10, -30), GetParent());
		}

		/// <summary>
		/// Zealous Vigor (Story-only, Joan): landing the final cleave of the basic
		/// three-hit string heals 5% of missing HP.
		/// </summary>
		private void OnMeleeHitConfirmed(FTT.Combat.HitPayload payload, float damageApplied) {
			if (damageApplied > 0f) {
				// V7.1 hitstop (attacker side): the same shared freeze the victim
				// takes in their own hit handler — both parties suspend together.
				ApplyHitstop(FTT.Combat.BasicComboRules.HitstopFrames(
					Math.Max(0, (int)MathF.Round(damageApplied))));
				// V7.1 Resonance Momentum: only the CONNECTING string finisher
				// refunds — never hits 1-2 or the directional strikes.
				if (payload.HitboxID == "combo_3") ApplyMomentumRefund();
				// Package 8 A3: the attacker-side feedback hook. Haptics and impact
				// VFX consume this; it carries no gameplay authority.
				FTT.Core.EventBus.Instance?.RaiseHitConfirm(new FTT.Core.HitConfirmPayload {
					PlayerIndex = PlayerIndex,
					AttackID = payload.AttackID ?? "",
					DamageApplied = damageApplied,
					IsHeavy = payload.AttackClass != FTT.Combat.AttackClass.Basic,
					Position = payload.HitOrigin
				});
			}
			if (damageApplied <= 0f || !HasStoryPerk(ZealousVigorPerkKey)) return;
			if (payload.HitboxID != "combo_3") return;
			// Zealous Vigor (Story-only, Joan). V7.6 rework: the connecting
			// finisher reclaims an EXTRA 25% of the Rally echo pool ON TOP of
			// the damage-scaled reclaim that AddInfluenceFromDamageDealt has
			// already taken for this hit — so this reads the pool as it stands
			// after that reclaim. The V6 "heal 5% of missing HP" is retired.
			ReclaimEchoPoolFraction(ZealousVigorEchoPoolFraction);
		}

		/// <summary>
		/// Package 11 A4 (Resonance V7.6). The shared "a hit I authored landed
		/// on something" hook, raised by <c>Hitbox</c> for every Story hit.
		/// Traversal flags that trigger on a connecting hit read it; nothing
		/// here reaches the deterministic simulation.
		/// </summary>
		public void NotifyStoryHitLanded(in FTT.Combat.HitPayload payload) {
			TryWingsRefresh(payload);
		}

		/// <summary>
		/// Wings Refresh (Story-only traversal node, Joan). A DIRECT connecting
		/// combo finisher or Righteous Smite hit resets Ascendant Wings'
		/// cooldown to zero — once per attack EXECUTION even on multi-target.
		/// It resets the cooldown and nothing else: no restored air jumps, no
		/// extra glide time, no action cancel, and no bypass of Suppression or
		/// a Time Freeze, all of which gate the cast itself rather than the
		/// cooldown. Never on a whiff, a blocked hit, an invulnerable target, a
		/// DoT tick or a prop, because <c>Hitbox</c> only raises the hook on
		/// applied damage against a hurtbox.
		/// </summary>
		private void TryWingsRefresh(in FTT.Combat.HitPayload payload) {
			if (!HasStoryPerk(Abilities.JoanAscendantWings.WingsRefreshPerkKey)) return;
			string hitboxID = payload.HitboxID ?? "";
			bool finisher = hitboxID == "combo_3";
			bool smite = (payload.AttackID ?? "") == Abilities.JoanAscendantWings.RighteousSmiteAttackID;
			if (!finisher && !smite) return;
			string execution = $"wings|{payload.AttackID}|{hitboxID}";
			if (_shieldOfOrleansExecutions.TryGetValue(execution, out int stamp)
				&& _storyFrame - stamp < ShieldOfOrleansExecutionWindowFrames) {
				return;
			}
			_shieldOfOrleansExecutions[execution] = _storyFrame;
			MovementAbilityCooldownTimer = 0f;
		}

		/// <summary>
		/// Reclaims a fraction of the LIVE Rally echo pool as real HP and
		/// removes it from the pool. Zealous Vigor's chokepoint; the ordinary
		/// damage-scaled reclaim lives in <see cref="AddInfluenceFromDamageDealt"/>.
		/// Reclaimed HP grants no meter to anyone.
		/// </summary>
		private void ReclaimEchoPoolFraction(float fraction) {
			if (fraction <= 0f || _echoPool <= 0f) return;
			float amount = MathF.Min(_echoPool, _echoPool * fraction);
			_echoPool -= amount;
			if (_echoPool <= 0.0001f) {
				_echoPool = 0f;
				_echoDrainPerFrame = 0f;
			}
			int reclaim = (int)MathF.Round(amount);
			if (reclaim > 0) HealStory(reclaim);
		}

		/// <summary>
		/// Shield of Orleans (Story-only, Joan; V7.6 F06 Option A). Grants a
		/// flat <see cref="ShieldOfOrleansMeterPerBlock"/> Influence once per
		/// DISTINCT blocked attack execution, and refunds one shield charge on
		/// the first successful block of a Guard-Crush execution
		/// (<c>BlockChargeCost == 2</c>).
		///
		/// <para>The engine carries no attack-execution ID, so "distinct" is
		/// approximated by (attacker, AttackID, HitboxID) inside a short
		/// window: each basic-string strike has its own HitboxID and counts
		/// once, while a multi-hit ability, volley, zone pulse or piercing
		/// projectile reuses one pair and grants at most one. See the plan's
		/// §9 A4 deviation note.</para>
		/// </summary>
		private void ApplyShieldOfOrleans(in FTT.Combat.HitPayload hit, FTT.Combat.BlockResult blockResult) {
			string execution = $"{hit.AttackerIndex}|{hit.AttackID ?? ""}|{hit.HitboxID ?? ""}";
			if (_shieldOfOrleansExecutions.TryGetValue(execution, out int stamp)
				&& _storyFrame - stamp < ShieldOfOrleansExecutionWindowFrames) {
				return;
			}
			PruneShieldOfOrleansExecutions();
			_shieldOfOrleansExecutions[execution] = _storyFrame;
			_ultimateMeter?.AddFlat(ShieldOfOrleansMeterPerBlock);
			CurrentUltimateMeter = _ultimateMeter?.CurrentValue ?? CurrentUltimateMeter;
			// The Guard-Crush refund runs AFTER normal consumption and after
			// any shatter, and deliberately does not cancel the daze, the
			// shieldstun or the shatter lockout — BlockSystem.CanRaiseStance
			// keeps the refunded charge unusable until that lockout ends.
			if (hit.BlockChargeCost >= 2) {
				_blockSystem?.RefundCharges(1, ShieldOfOrleansRefundCap);
				CurrentBlockCharges = _blockSystem?.CurrentCharges ?? CurrentBlockCharges;
			}
			_ = blockResult;
		}

		private void PruneShieldOfOrleansExecutions() {
			if (_shieldOfOrleansExecutions.Count < 8) return;
			var stale = new List<string>();
			foreach (KeyValuePair<string, int> entry in _shieldOfOrleansExecutions) {
				if (_storyFrame - entry.Value >= ShieldOfOrleansExecutionWindowFrames) stale.Add(entry.Key);
			}
			foreach (string key in stale) _shieldOfOrleansExecutions.Remove(key);
		}

		public override void _PhysicsProcess(double delta) {
			float dt = (float)delta;
			_storyFrame++;
			CurrentInputFrame = FTT.Core.InputManager.Instance?.GetFrame(PlayerIndex) ?? default;

			// V7.1 hitstop: while frozen, every state timer, velocity, position,
			// and the animation clock are suspended — only the input frame is
			// read (it feeds DI below). At expiry a stashed launching hit
			// resolves directional influence from the held direction.
			if (_hitstopFramesRemaining > 0) {
				_hitstopFramesRemaining--;
				if (_animatedSprite != null) _animatedSprite.SpeedScale = 0f;
				if (_hitstopFramesRemaining <= 0) {
					if (_animatedSprite != null) _animatedSprite.SpeedScale = 1f;
					if (_hasPendingLaunch) {
						FTT.Combat.BasicComboRules.ResolveDirectionalInfluence(
							_pendingLaunch.X, _pendingLaunch.Y,
							CurrentInputFrame.Horizontal, CurrentInputFrame.Vertical,
							out float launchX, out float launchY);
						Velocity = new Vector2(launchX, launchY);
						_hasPendingLaunch = false;
					}
				}
				return;
			}

			if (_techInvulnerabilitySeconds > 0f) _techInvulnerabilitySeconds -= dt;
			if (_ledgeRegrabLockoutSeconds > 0f) _ledgeRegrabLockoutSeconds -= dt;
			// V7.3 regrab cap: floor contact resets the per-airtime ledge budget.
			if (IsOnFloor()) _ledgeGrabsThisAirtime = 0;

			// V7.1 Echo Step bookkeeping: sample the position ring, tick the
			// cooldown, and advance an armed wind-up (the snap fires at 0).
			AdvanceEchoStep();

			// V7.1 Rally drain: the Echo Pool empties linearly over 150 frames.
			// Echo that finishes draining is permanently lost HP, so its
			// deferred meter-from-damage-taken accrues now (the guardrail's
			// other half — nothing was earned at hit time for this portion).
			if (_echoPool > 0f && CurrentState != CharacterState.Dead) {
				float drained = Mathf.Min(_echoPool, _echoDrainPerFrame);
				_echoPool -= drained;
				_ultimateMeter?.AddFromDamageTaken(drained);
				CurrentUltimateMeter = _ultimateMeter?.CurrentValue ?? CurrentUltimateMeter;
				if (_echoPool <= 0.0001f) {
					_echoPool = 0f;
					_echoDrainPerFrame = 0f;
				}
			}

			UpdateStoryTemporaryEffects();
			// V7.6 D02c/D02d/D04 (Package 11 A1b): the granted-shield lifetime,
			// Wardenclyffe's recharge delay and the Defy protected second all
			// run on the suspended-effect clock.
			TickStoryDefense();
			// F13: the Defy seal is DERIVED from authoritative meter / Defy-used
			// / life state after the gameplay update, never persisted as its own
			// cosmetic flag.
			PublishDefySealIfChanged();

			// V7.6: the freeze suspends ability cooldowns (block-charge regen and
			// status durations suspend in BlockSystem/StatusController for the
			// same reason). Movement and the effect clock keep running.
			if (!TimeFrozen) UpdateCooldowns(dt);
			UpdateDropThrough(dt);
			UpdateHyperArmorPresentation();
			// F07: the Conductive mark is a caster-owned combo mark, not a
			// status — it ticks on its own frame counter and locks nothing.
			TickConductiveMark();
			if (_downTapFramesRemaining > 0) _downTapFramesRemaining--;

			// V7.3 landing-tech lock: the 12-frame recovery holds the player in
			// place — invulnerable (the ApplyDamage gate), no actions, no
			// movement — then releases to Idle. Placed after the counters above
			// so cooldowns and the Rally drain keep ticking, exactly as the
			// sim's TechLockoutFrames branch keeps TickCounters running.
			if (_techLockoutSeconds > 0f) {
				_techLockoutSeconds -= dt;
				Velocity = Vector2.Zero;
				if (_techLockoutSeconds <= 0f) TransitionTo(CharacterState.Idle);
				return;
			}

			switch (CurrentState) {
				case CharacterState.Idle:
					ProcessIdle(dt);
					break;
				case CharacterState.Running:
					ProcessRunning(dt);
					break;
				case CharacterState.Rolling:
					ProcessRolling(dt);
					break;
				case CharacterState.Skidding:
					ProcessSkidding(dt);
					break;
				case CharacterState.Crouching:
					ProcessCrouching(dt);
					break;
				case CharacterState.Airborne:
					ProcessAirborne(dt);
					break;
				case CharacterState.Attacking:
					ProcessAttacking(dt);
					break;
				case CharacterState.UsingSpecial:
					ProcessUsingSpecial(dt);
					break;
				case CharacterState.UsingUltimate:
					ProcessUsingUltimate(dt);
					break;
				case CharacterState.Blocking:
					ProcessBlocking(dt);
					break;
				case CharacterState.Stunned:
					ProcessStunned(dt);
					break;
				case CharacterState.Dazed:
					ProcessDazed(dt);
					break;
				case CharacterState.LedgeHanging:
					ProcessLedgeHanging(dt);
					break;
				case CharacterState.Dead:
					break;
				case CharacterState.Respawning:
					ProcessRespawning(dt);
					break;
				case CharacterState.UsingMovementAbility:
					ProcessUsingMovementAbility(dt);
					break;
				case CharacterState.Grabbing:
					ProcessGrabbing(dt);
					break;
				case CharacterState.Thrown:
					ProcessThrown(dt);
					break;
			}

			if (CurrentState != CharacterState.LedgeHanging &&
				CurrentState != CharacterState.Dead) {
				MoveAndSlide();
			}

			if (!_rewindSuspended && _pushbox != null) {
				if (CurrentState == CharacterState.Rolling && IsRollTravelFrame) {
					if (_pushbox.ResolveBlockingRollOverlaps(_rollDirection)) {
						EnterRollRecovery();
					}
				} else {
					_pushbox.ResolveStoryOverlaps(
						CurrentState == CharacterState.Rolling ? _rollDirection : 0);
				}
			}

			UpdateFootsteps(dt);
			UpdateLandingFeedback();

			_wasGrounded = IsOnFloor();
			// Package 11 A4: per-airtime Resonance traversal latches reset the
			// moment the feet are down.
			if (_wasGrounded) ResetAirtimeTraversalLatches();
		}

		/// <summary>
		/// Fall distance, in pixels, above which a landing plays the Heavy Landing
		/// haptic — the design's "fall &gt; 3 units" at the 62.5 px/world-unit scale
		/// the Fighter presentation mapping uses.
		/// </summary>
		private const float HeavyLandingFallPixels =
			FTT.Core.HapticFeedbackManager.HeavyLandingFallUnits * 62.5f;

		private bool _fallTracking;
		private float _fallPeakY;

		/// <summary>
		/// Heavy Landing haptic (design haptic table; audit M-31). Tracks the
		/// airborne apex and buzzes on the grounded edge when the drop was tall
		/// enough. Purely additive presentation, runs after <c>MoveAndSlide</c> so
		/// <c>IsOnFloor</c> describes the frame that actually happened.
		/// </summary>
		private void UpdateLandingFeedback() {
			if (!IsOnFloor()) {
				// Y grows downward, so the apex is the smallest Y seen airborne.
				if (!_fallTracking) {
					_fallTracking = true;
					_fallPeakY = GlobalPosition.Y;
				} else if (GlobalPosition.Y < _fallPeakY) {
					_fallPeakY = GlobalPosition.Y;
				}
				return;
			}
			if (!_fallTracking) return;
			_fallTracking = false;
			if (_rewindSuspended || CurrentState == CharacterState.Dead) return;
			if (GlobalPosition.Y - _fallPeakY >= HeavyLandingFallPixels) {
				FTT.Core.HapticFeedbackManager.Instance?.OnHeavyLanding(PlayerIndex);
			}
		}

		/// <summary>
		/// Package 8 B5. Emits a footstep every stride of grounded travel. Runs after
		/// <c>MoveAndSlide</c> so <c>IsOnFloor</c> and <c>Velocity</c> describe the
		/// frame that actually happened; the cadence itself lives in
		/// <see cref="FTT.Core.FootstepCadence"/> so the rhythm is testable without a
		/// physics frame. Purely additive presentation — no gameplay state is read or
		/// written here.
		///
		/// <para>Rolling is deliberately silent: the character is off their feet, and
		/// the roll has its own feedback. Fighter Mode gets no footsteps from this
		/// path at all, because the driver disables native processing on its
		/// presentation bodies.</para>
		/// </summary>
		private void UpdateFootsteps(float dt) {
			bool eligible = IsOnFloor() && !_rewindSuspended && CurrentState switch {
				CharacterState.Rolling => false,
				CharacterState.Dead => false,
				CharacterState.Respawning => false,
				CharacterState.Stunned => false,
				CharacterState.Dazed => false,
				CharacterState.Thrown => false,
				CharacterState.LedgeHanging => false,
				_ => true
			};
			if (_footsteps.Advance(eligible, Velocity.X, dt)) {
				FTT.Core.AudioManager.Instance?.PlayFootstep(FootstepSurfaceID);
			}
		}

		// === State Processors ===

		private void ProcessIdle(float dt) {
			ApplyGravity(dt);
			float maxSpeed = EffectiveMoveSpeed * StatusMovementMultiplier * 60f;
			float step = maxSpeed / GroundDecelRampFrames * dt * 60f;
			var idleVel = Velocity;
			idleVel.X = Mathf.MoveToward(idleVel.X, 0, step);
			Velocity = idleVel;

			if (!IsOnFloor()) {
				_coyoteTimer -= dt;
				if (_coyoteTimer <= 0) {
					TransitionTo(CharacterState.Airborne);
					return;
				}
			} else {
				_coyoteTimer = CoyoteTime;
				RemainingJumps = Data?.MaxJumpCount ?? 1;
			}

			if (CheckRollInput()) return;
			if (CheckJumpInput()) return;
			if (CheckAttackInput()) return;
			if (CheckSpecialInput()) return;
			if (CheckUltimateInput()) return;
			if (CheckBlockInput()) return;
			if (CheckMovementAbilityInput()) return;
			if (CheckInteractInput()) return;

			float hAxis = GetHorizontalInput();
			if (Mathf.Abs(hAxis) > 0.1f) {
				TransitionTo(CharacterState.Running);
				return;
			}

			if (CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.Down)) {
				TransitionTo(CharacterState.Crouching);
				return;
			}

			CheckDropThrough();
			PlayAnimation("idle");
		}

		private void ProcessRunning(float dt) {
			ApplyGravity(dt);

			if (!IsOnFloor()) {
				_coyoteTimer -= dt;
				if (_coyoteTimer <= 0) {
					TransitionTo(CharacterState.Airborne);
					return;
				}
			} else {
				_coyoteTimer = CoyoteTime;
				RemainingJumps = Data?.MaxJumpCount ?? 1;
			}

			if (CheckRollInput()) return;
			if (CheckJumpInput()) return;
			if (CheckAttackInput()) return;
			// M04 (Package 12 W3): Running → UsingSpecial / UsingUltimate with
			// the same input and eligibility as from Idle (the sim never gated
			// specials on locomotion).
			if (CheckSpecialInput()) return;
			if (CheckUltimateInput()) return;
			if (CheckBlockInput()) return;
			if (CheckMovementAbilityInput()) return;

			float hAxis = GetHorizontalInput();
			if (Mathf.Abs(hAxis) < 0.1f) {
				TransitionTo(CharacterState.Idle);
				return;
			}

			if (CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.Down)) {
				TransitionTo(CharacterState.Crouching);
				return;
			}

			bool wantsRight = hAxis > 0;
			if (wantsRight != IsFacingRight && IsOnFloor()) {
				TransitionTo(CharacterState.Skidding);
				return;
			}

			ApplyHorizontalMovement(hAxis, dt);
			UpdateFacing(hAxis);
			CheckDropThrough();
			PlayAnimation("run");
		}

		private void ProcessRolling(float dt) {
			ApplyGravity(dt);
			_rollInvulnerable = false;

			if (_rollFrame < FTT.Core.UniversalMovementRules.RollStartupFrames) {
				_pushbox?.SetPushEnabled(true);
				DecelerateHorizontal(dt);
				PlayAnimation("roll_startup");
			} else if (IsRollTravelFrame) {
				int travelFrame = _rollFrame - FTT.Core.UniversalMovementRules.RollStartupFrames;
				_rollInvulnerable = travelFrame < FTT.Core.UniversalMovementRules.RollInvulnerabilityFrames;
				_pushbox?.SetPushEnabled(false);
				float rollSpeed = EffectiveMoveSpeed
					* StatusMovementMultiplier
					* 60f
					* FTT.Core.UniversalMovementRules.RollSpeedMultiplier;
				Velocity = new Vector2(_rollDirection * rollSpeed, Velocity.Y);
				PlayAnimation("roll");
			} else {
				_pushbox?.SetPushEnabled(true);
				DecelerateHorizontal(dt);
				PlayAnimation("roll_recovery");
			}

			_rollFrame++;
			if (_rollFrame >= FTT.Core.UniversalMovementRules.RollTotalFrames) {
				TransitionTo(IsOnFloor() ? CharacterState.Idle : CharacterState.Airborne);
			}
		}

		private bool IsRollTravelFrame =>
			_rollFrame >= FTT.Core.UniversalMovementRules.RollStartupFrames
			&& _rollFrame < FTT.Core.UniversalMovementRules.RollStartupFrames
				+ FTT.Core.UniversalMovementRules.RollTravelFrames;

		private void EnterRollRecovery() {
			_rollFrame = FTT.Core.UniversalMovementRules.RollStartupFrames
				+ FTT.Core.UniversalMovementRules.RollTravelFrames;
			_rollInvulnerable = false;
			_pushbox?.SetPushEnabled(true);
			Velocity = new Vector2(0f, Velocity.Y);
		}

		private void DecelerateHorizontal(float dt) {
			float maxSpeed = EffectiveMoveSpeed * StatusMovementMultiplier * 60f;
			float step = maxSpeed / GroundDecelRampFrames * dt * 60f;
			Velocity = new Vector2(Mathf.MoveToward(Velocity.X, 0f, step), Velocity.Y);
		}

		private void ProcessSkidding(float dt) {
			ApplyGravity(dt);
			float maxSpeed = EffectiveMoveSpeed * StatusMovementMultiplier * 60f;
			float step = maxSpeed / GroundDecelRampFrames * dt * 60f;
			var skidVel = Velocity;
			skidVel.X = Mathf.MoveToward(skidVel.X, 0, step);
			Velocity = skidVel;
			_skidTimer -= dt;

			if (_skidTimer <= 0) {
				IsFacingRight = !IsFacingRight;
				UpdateSpriteFlip();
				TransitionTo(CharacterState.Running);
			}

			PlayAnimation("skid");
		}

		private void ProcessCrouching(float dt) {
			ApplyGravity(dt);
			float maxSpeed = EffectiveMoveSpeed * StatusMovementMultiplier * 60f;
			float step = maxSpeed / GroundDecelRampFrames * dt * 60f;
			var crouchVel = Velocity;
			crouchVel.X = Mathf.MoveToward(crouchVel.X, 0, step);
			Velocity = crouchVel;

			if (!CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.Down)) {
				RestoreHurtboxHeight();
				TransitionTo(CharacterState.Idle);
				return;
			}

			// M04 (Package 12 W3): Crouching → Rolling / Airborne / Blocking /
			// UsingSpecial / UsingUltimate / UsingMovementAbility with the same
			// input and eligibility as from Idle (Idle's check order). Drop-
			// through stays the double-tap Down, so Jump from a crouch is free.
			// TransitionTo restores the crouched hurtbox on the way out.
			if (CheckRollInput()) return;
			if (CheckJumpInput()) return;
			if (CheckAttackInput()) return;
			if (CheckSpecialInput()) return;
			if (CheckUltimateInput()) return;
			if (CheckBlockInput()) return;
			if (CheckMovementAbilityInput()) return;
			CheckDropThrough();
			PlayAnimation("crouch");
		}

		private void ProcessAirborne(float dt) {
			ApplyGravity(dt);

			float hAxis = GetHorizontalInput();
			float maxSpeed = EffectiveMoveSpeed * StatusMovementMultiplier * 60f;
			float targetSpeed = hAxis * maxSpeed;
			bool isAccelerating = Mathf.Abs(targetSpeed) >= Mathf.Abs(Velocity.X)
				|| (targetSpeed > 0 && Velocity.X < 0) || (targetSpeed < 0 && Velocity.X > 0);
			// The 4-frame air ramp is the designed constant the Fighter sim uses;
			// the 8-frame ground ramp was accidental here (audit M-17).
			float rampFrames = isAccelerating ? AirAccelRampFrames : AirDecelRampFrames;
			// V7.2 wiring: AirControlMultiplier was authored on all nine characters
			// (0.4 Lincoln .. 0.75 Pocahontas) and read by nothing — it scales how
			// quickly held input changes airborne velocity (aerial identity).
			float step = maxSpeed / rampFrames * dt * 60f * (Data?.AirControlMultiplier ?? 1f);
			var vel = Velocity;
			vel.X = Mathf.MoveToward(vel.X, targetSpeed, step);

			// Fast-fall (§2.9, 2026-08-10): stateless — held Down while airborne
			// snaps the descent to FastFallSpeedPixels and cancels the
			// Warp float window. Applied after ApplyGravity so it deliberately
			// overrides that method's 600 px/s terminal clamp for this case.
			// ProcessStunned/ProcessDazed own hitstun and never route here.
			// M01 (D1(a), Package 12 W3): fast-fall SNAPS to 20 u/s (1200 px/s),
			// the terminal it is defined as — assigned, never exceeded. The
			// ordinary Story terminal (600 px/s) is unchanged; H-7 stays deferred.
			if (CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.Down)) {
				StoryFloatTimer = 0f;
				vel.Y = FastFallSpeedPixels;
			}
			Velocity = vel;

			UpdateFacing(hAxis);

			if (IsOnFloor()) {
				if (_jumpBufferTimer > 0) {
					PerformJump();
				} else {
					AerialComboCounter = 0;
					ComboCounter = 0;
					TransitionTo(CharacterState.Idle);
				}
				return;
			}

			if (CheckJumpInput()) return;
			if (CheckAttackInput()) return;
			if (CheckSpecialInput()) return;
			if (CheckUltimateInput()) return;
			if (CheckMovementAbilityInput()) return;

			PlayAnimation(Velocity.Y < 0 ? "jump" : "fall");
		}

		private void ProcessAttacking(float dt) {
			ApplyGravity(dt);
			if (_attackStartedAerial && IsOnFloor()) {
				CancelActiveAttack();
				AerialComboCounter = 0;
				ComboCounter = 0;
				TransitionTo(CharacterState.Idle);
				return;
			}
			if (IsOnFloor()) {
				// Swings never stop the attacker: full horizontal steering at
				// the normal run ramp through startup/active/recovery/hold
				// (mirrors the Fighter sim's unlocked swing movement).
				ApplyHorizontalMovement(GetHorizontalInput(), dt);
			} else {
				ApplyAirControl(dt);
			}
			// Gameplay-feel plan §2.12 — facing follows movement during a swing,
			// superseding the 2026-08-09 "committed for the whole string" rule.
			// Hitbox placement still reads facing when the active window opens
			// (OnAttackActiveStarted), so nothing migrates mid-active.
			UpdateFacing(GetHorizontalInput());

			// Design 1051 — a special (or the ultimate) cancels a basic at any
			// point in the swing and resets the chain; shared rule with the
			// Fighter sim.
			if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Special1)
				|| CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Special2)
				|| CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Ultimate)) {
				if (CheckSpecialInput() || CheckUltimateInput()) {
					CancelActiveAttack();
					ResetComboChain();
					return;
				}
			}

			if (_inRecoveryHold) {
				ProcessRecoveryHold(dt);
				return;
			}

			CheckDropThrough();

			// Directional attacks are single strikes: they never buffer into the
			// three-hit string (§2.8).
			if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.BasicAttack)
				&& !_attackHitActive
				&& _attackVariant == FTT.Combat.BasicComboRules.VariantChain) {
				_nextAttackBuffered = true;
			}

			// Design 752/3080 — during recovery frames, jumping, rolling, or
			// blocking cancels the swing and resets the chain. Held movement
			// steers the attacker but never cancels: the authored string pace
			// is the only pace.
			if (_attackInRecovery) {
				// V7.1 Echo Step: the Block+Roll chord in recovery frames arms
				// the step first — the same chord then cancels the swing below
				// (the wind-up owns the Roll press, so no roll starts).
				TryStartEchoStep();
				if (TryRecoveryCancel()) return;
			}

			if (_attackAnimationDriven) return;

			FTT.Combat.CombatFrameTimeline timeline = GetActiveComboTimeline();
			int elapsedFrame = timeline.TotalFrames - _attackFramesRemaining;
			bool shouldBeActive = timeline.IsActive(elapsedFrame);

			if (shouldBeActive && !_attackHitActive) {
				OnAttackActiveStarted();
			}

			if (_attackHitActive && !shouldBeActive) {
				OnAttackActiveEnded();
			}

			// Story-only ComboSpeed minors advance the authored attack clock
			// faster than real time (a 1f multiplier steps exactly one frame).
			_attackFrameProgress += StoryComboSpeedMultiplier;
			while (_attackFrameProgress >= 1f && _attackFramesRemaining > 0) {
				_attackFrameProgress -= 1f;
				_attackFramesRemaining--;
			}
			if (_attackFramesRemaining <= 0) {
				CompleteCurrentComboHit();
			}
		}

		/// <summary>
		/// The post-recovery chain window. Reached only from
		/// <see cref="ProcessAttacking"/>, which has already applied this tick's
		/// movement and §2.12 facing update before dispatching here.
		/// </summary>
		private void ProcessRecoveryHold(float dt) {
			_comboBufferFramesRemaining--;

			if (_attackStartedAerial && IsOnFloor()) {
				CancelActiveAttack();
				AerialComboCounter = 0;
				ComboCounter = 0;
				TransitionTo(CharacterState.Idle);
				return;
			}

			if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.BasicAttack)) {
				_inRecoveryHold = false;
				AdvanceActiveCombo();
				StartComboHit();
				return;
			}

			// Same cancel set as the recovery frames: jump, roll, or block
			// ends the chain window (specials are handled upstream in
			// ProcessAttacking before the hold dispatch). Held movement walks
			// the hold window without ending it.
			if (TryRecoveryCancel()) {
				_inRecoveryHold = false;
				return;
			}

			if (_comboBufferFramesRemaining <= 0) {
				_inRecoveryHold = false;
				ResetActiveCombo();
				TransitionTo(IsOnFloor() ? CharacterState.Idle : CharacterState.Airborne);
			}
		}

		/// <summary>
		/// Design 752/3080: during a basic's recovery frames or the chain-hold
		/// window, jumping, rolling, or blocking cancels the swing and
		/// resets the chain. Held horizontal movement deliberately does not —
		/// it steers the attacker while the string keeps its authored pace
		/// (movement-cancel let a moving attacker restart hit one early). The
		/// Fighter sim applies the identical rule in its phase machine.
		/// </summary>
		private bool TryRecoveryCancel() {
			// An Echo Step wind-up owns the Roll press that armed it — the
			// chord must not also start a roll.
			if (CheckJumpInput()
				|| (_echoStepWindupFrames <= 0 && CheckRollInput())
				|| CheckBlockInput()) {
				CancelActiveAttack();
				ResetComboChain();
				return true;
			}
			return false;
		}

		/// <summary>
		/// V7.6 Echo Step bookkeeping (Package 11 A1c). Records <b>one sample per
		/// physics tick</b> into the 31-entry ring, ticks the internal cooldown, and
		/// advances an armed wind-up.
		///
		/// <para>At wind-up completion the <b>same</b> destination is rechecked
		/// against the world as it is <em>then</em>: a gate that closed or a
		/// platform that moved during the eight frames cancels the teleport and the
		/// committed meter and cooldown are <b>retained</b> — no refund and no
		/// nearby substitute. On a successful snap, position only: velocity is
		/// zeroed, facing preserved, actionable immediately. Being struck during the
		/// wind-up cancels it, also with no refund.</para>
		/// </summary>
		private void AdvanceEchoStep() {
			// A fresh controller has no generation yet: open one at the spawn.
			if (_echoStepHistoryGeneration == 0) ResetEchoStepHistory();
			if (_echoStepCooldownFrames > 0) _echoStepCooldownFrames--;
			// Sampled BEFORE this tick's input and movement, so "t-30" is thirty
			// ticks. With 31 slots the head is also the t-30 slot.
			_echoStepRing[_echoStepRingHead] = GlobalPosition;
			_echoStepRingHead = (_echoStepRingHead + 1) % _echoStepRing.Length;
			if (_echoStepRingValidCount < _echoStepRing.Length) _echoStepRingValidCount++;

			if (_echoStepWindupFrames <= 0) return;
			if (CurrentState is CharacterState.Stunned or CharacterState.Dazed
				or CharacterState.Dead or CharacterState.Respawning
				or CharacterState.Thrown) {
				_echoStepWindupFrames = 0;
				_echoStepArmed = false;
				return;
			}
			_echoStepWindupFrames--;
			if (_echoStepWindupFrames != 0) return;
			_echoStepArmed = false;
			// The second validation. A destination that became blocked cancels the
			// teleport; the cost stands.
			if (!IsEchoStepDestinationClear(_echoStepDestination)) return;
			GlobalPosition = _echoStepDestination;
			Velocity = Vector2.Zero;
		}

		/// <summary>
		/// The exact <c>t - 30</c> sample, or false when this history generation has
		/// not produced one yet — a spawn, a death-rewind landing or a checkpoint
		/// reconstruction genuinely costs the player 30 ticks before the verb is
		/// available again. Storage filled at a reset is never treated as history.
		/// </summary>
		private bool TryGetEchoStepLookback(out Vector2 destination) {
			destination = GlobalPosition;
			if (_echoStepRingValidCount < _echoStepRing.Length) return false;
			destination = _echoStepRing[_echoStepRingHead];
			return true;
		}

		/// <summary>
		/// Story's destination validation, the mirror of the sim's geometry
		/// predicate: the player's <b>full collision shape</b> is swept against the
		/// Environment layer at the candidate position. A solid overlap refuses it.
		///
		/// <para>The intervening path need not be clear — this is a teleport, not a
		/// move — and airborne destinations stay legal, so support is never
		/// required. Headless callers with no physics space get a permissive answer
		/// rather than a refusal, because a test harness without a world is not
		/// evidence of an obstruction.</para>
		/// </summary>
		private bool IsEchoStepDestinationClear(Vector2 destination) {
			if (_collisionShape?.Shape == null) return true;
			PhysicsDirectSpaceState2D space = GetWorld2D()?.DirectSpaceState;
			if (space == null) return true;
			var query = new PhysicsShapeQueryParameters2D {
				Shape = _collisionShape.Shape,
				Transform = new Transform2D(0f, destination + _collisionShape.Position),
				CollisionMask = FTT.Core.CollisionLayers.Environment,
				CollideWithBodies = true,
				CollideWithAreas = false
			};
			Godot.Collections.Array<Godot.Collections.Dictionary> hits = space.IntersectShape(query, 1);
			using var lifetime = hits.AsDisposable();
			return hits.Count == 0;
		}

		/// <summary>
		/// V7.6 Echo Step initiation. During the recovery frames of the player's own
		/// swing (basic, directional, or special) the Block+Roll chord — or the
		/// direct <c>gameplay_echo_step</c> bind — snaps to the position held
		/// <b>exactly</b> 30 ticks earlier.
		///
		/// <para><b>Exact or nothing.</b> The destination is resolved and validated
		/// before anything is spent: insufficient history, or a target inside
		/// terrain, refuses the activation at <b>no meter and no cooldown cost</b>
		/// and does not start the wind-up. Acceptance spends the 30 meter, arms the
		/// 8-frame wind-up and starts the 120-frame cooldown atomically, once.</para>
		///
		/// <para>Callers gate the "recovery frames" half; hitstop cannot reach here
		/// (the frozen frame returns before any state processing).</para>
		/// </summary>
		private bool TryStartEchoStep() {
			if (TimeFrozen) return false;
			if (_echoStepWindupFrames > 0 || _echoStepCooldownFrames > 0) return false;

			bool echoLegal =
				TryGetEchoStepLookback(out Vector2 destination)
				&& IsEchoStepDestinationClear(destination)
				&& CurrentUltimateMeter >= FTT.Combat.BasicComboRules.EchoStepMeterCost;

			// V7.6 same-frame chord priority, from the one shared table both modes
			// read. A direct bind requests the same verb once and grants no extra
			// priority, cancel, leniency or resource bypass.
			bool directRequest = CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.EchoStep);
			bool chordAllowed = !CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.DirectOrigin);
			FTT.Combat.BasicComboRules.RecoveryVerb chosen =
				FTT.Combat.BasicComboRules.SelectRecoveryVerb(
					blockHeld: chordAllowed && CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.Block),
					blockPressed: CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Block),
					rollHeld: CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.Roll),
					rollPressed: CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Roll),
					basicPressed: CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.BasicAttack),
					grabLegal: false,
					echoLegal: echoLegal);
			if (!directRequest && chosen != FTT.Combat.BasicComboRules.RecoveryVerb.EchoStep) return false;
			// A refused direct action does nothing and spends nothing.
			if (!echoLegal) return false;

			DrainUltimateMeter(FTT.Combat.BasicComboRules.EchoStepMeterCost);
			_echoStepCooldownFrames = FTT.Combat.BasicComboRules.EchoStepCooldownFrames;
			_echoStepWindupFrames = FTT.Combat.BasicComboRules.EchoStepWindupFrames;
			_echoStepDestination = destination;
			_echoStepArmed = true;
			return true;
		}

		/// <summary>
		/// V7.1 Resonance Momentum: a CONNECTING basic-string finisher refunds
		/// 60 frames from each running special cooldown, at most twice per
		/// cooldown cycle per slot (BaseSpecial re-arms the counters when a
		/// slot's cooldown starts).
		/// </summary>
		private void ApplyMomentumRefund() {
			const float refundSeconds = FTT.Combat.BasicComboRules.MomentumRefundFrames / 60f;
			if (SpecialOneCooldownTimer > 0f
				&& _momentumRefundsSlotOne < FTT.Combat.BasicComboRules.MomentumRefundCapPerCycle) {
				SpecialOneCooldownTimer = Mathf.Max(0f, SpecialOneCooldownTimer - refundSeconds);
				_momentumRefundsSlotOne++;
			}
			if (SpecialTwoCooldownTimer > 0f
				&& _momentumRefundsSlotTwo < FTT.Combat.BasicComboRules.MomentumRefundCapPerCycle) {
				SpecialTwoCooldownTimer = Mathf.Max(0f, SpecialTwoCooldownTimer - refundSeconds);
				_momentumRefundsSlotTwo++;
			}
		}

		/// <summary>Called by BaseSpecial when a slot's cooldown is armed: a fresh
		/// cycle re-arms its Resonance Momentum refunds.</summary>
		public void OnSpecialCooldownArmed(FTT.Core.AbilitySlot slot) {
			if (slot == FTT.Core.AbilitySlot.Special1) _momentumRefundsSlotOne = 0;
			else if (slot == FTT.Core.AbilitySlot.Special2) _momentumRefundsSlotTwo = 0;
		}

		/// <summary>Resets both surface counters — the whole chain, not one string.</summary>
		private void ResetComboChain() {
			GroundComboCounter = 0;
			AerialComboCounter = 0;
			ComboCounter = 0;
			_attackVariant = FTT.Combat.BasicComboRules.VariantChain;
		}

		private void ProcessUsingSpecial(float dt) {
			var ability = _pendingSpecialSlot == 2 ? _special2 : _special1;
			float gravityMultiplier = _specialStartedAerial && ability?.CurrentPhase is
				FTT.Combat.AbilityPhase.Startup or FTT.Combat.AbilityPhase.Active
				? 0.5f
				: 1f;
			ApplyGravity(dt * gravityMultiplier);
			// V7.1 Echo Step: a special's recovery frames also qualify.
			if (ability?.CurrentPhase == FTT.Combat.AbilityPhase.Recovery) {
				TryStartEchoStep();
				// Package 12 W3: Block may cancel a Special's RECOVERY frames,
				// exactly as it cancels a basic's (design 752/3080: recovery
				// frames define block-cancel eligibility, M08). Grounded only —
				// the stance is grounded — and never while an Echo Step wind-up
				// owns the chord. The cast is interrupted (what it already put
				// into the world survives); the cooldown it armed stands.
				if (IsOnFloor() && _echoStepWindupFrames <= 0 && CheckBlockInput()) {
					ability.Interrupt();
					_specialStartedAerial = false;
					return;
				}
			}
			if (ability == null || !ability.IsExecuting) {
				_specialStartedAerial = false;
				TransitionTo(IsOnFloor() ? CharacterState.Idle : CharacterState.Airborne);
			}
		}

		private void ProcessUsingUltimate(float dt) {
			if (_ultimateStartedAerial && _ultimate?.IsExecuting == true) {
				Velocity = new Vector2(Velocity.X, 0f);
			}
			if (_ultimate == null || !_ultimate.IsExecuting) {
				_ultimateStartedAerial = false;
				TransitionTo(IsOnFloor() ? CharacterState.Idle : CharacterState.Airborne);
			}
		}

		/// <summary>
		/// V7.2 grab initiation (Story): grounded, from neutral or the block
		/// stance. 10f startup / 4f active / 24f whiff recovery; a connect
		/// holds a standard mob for the 30-frame decision window, then the held
		/// direction throws. Elites and bosses are grab-immune (the attempt
		/// whiffs into normal recovery).
		/// </summary>
		private bool TryStartGrab() {
			if (TimeFrozen) return false;
			if (!IsOnFloor()) return false;
			if (CurrentState is CharacterState.Stunned or CharacterState.Dazed
				or CharacterState.Dead or CharacterState.Respawning
				or CharacterState.Grabbing or CharacterState.Rolling
				or CharacterState.LedgeHanging) return false;
			if (CurrentState == CharacterState.Attacking) CancelActiveAttack();
			ResetComboChain();
			_grabPhase = 1;
			_grabPhaseFrames = FTT.Combat.BasicComboRules.GrabStartupFrames;
			_grabThrowDirection = 0;
			_grabbedEnemy = null;
			PlayAnimation("grab");
			TransitionTo(CharacterState.Grabbing);
			return true;
		}

		private void ProcessGrabbing(float dt) {
			ApplyGravity(dt);
			var vel = Velocity;
			vel.X = Mathf.MoveToward(vel.X, 0f, EffectiveMoveSpeed * 60f / GroundDecelRampFrames * dt * 60f);
			Velocity = vel;

			if (_grabPhase is 4 or 5) PinGrabbedEnemy();

			_grabPhaseFrames--;
			if (_grabPhaseFrames > 0) {
				// The active window scans every frame it is open.
				if (_grabPhase == 2 && TryConnectGrab()) return;
				return;
			}

			switch (_grabPhase) {
				case 1:
					_grabPhase = 2;
					_grabPhaseFrames = FTT.Combat.BasicComboRules.GrabActiveFrames;
					if (TryConnectGrab()) return;
					break;
				case 2:
					// Whiff — the most punishable committal in the kit.
					_grabPhase = 3;
					_grabPhaseFrames = FTT.Combat.BasicComboRules.GrabWhiffRecoveryFrames;
					break;
				case 3:
					EndGrab(CharacterState.Idle);
					break;
				case 4:
					// The decision window closed: the held direction picks the
					// throw; both parties are invulnerable through the animation.
					_grabThrowDirection = ResolveStoryThrowDirection();
					_grabPhase = 5;
					_grabPhaseFrames = FTT.Combat.BasicComboRules.ThrowAnimationFrames;
					_techInvulnerabilitySeconds = Mathf.Max(
						_techInvulnerabilitySeconds,
						FTT.Combat.BasicComboRules.ThrowAnimationFrames / 60f);
					break;
				case 5:
					ResolveStoryThrow();
					break;
				default:
					EndGrab(CharacterState.Idle);
					break;
			}
		}

		private bool TryConnectGrab() {
			FTT.Enemies.EnemyController target = FindGrabbableEnemy();
			if (target == null) return false;
			_grabPhase = 4;
			_grabPhaseFrames = FTT.Combat.BasicComboRules.ThrowDecisionFrames;
			_grabbedEnemy = target;
			target.BeginHeld();
			PinGrabbedEnemy();
			return true;
		}

		private FTT.Enemies.EnemyController FindGrabbableEnemy() {
			float facing = IsFacingRight ? 1f : -1f;
			FTT.Enemies.EnemyController best = null;
			float bestDistance = float.MaxValue;
			foreach (Godot.Node node in GetTree().GetNodesInGroup("Enemies")) {
				if (node is not FTT.Enemies.EnemyController enemy || !enemy.IsGrabbable) continue;
				// Grabs are a neutral tool: they whiff against a victim already
				// in hitstun or daze, exactly as the sim's rule reads.
				if (enemy.CurrentState is FTT.Enemies.EnemyState.Stunned) continue;
				Vector2 offset = enemy.GlobalPosition - GlobalPosition;
				float front = offset.X * facing;
				if (front < 0f || front > GrabReachPixels) continue;
				if (Mathf.Abs(offset.Y) > 62.5f) continue;
				if (front < bestDistance) {
					bestDistance = front;
					best = enemy;
				}
			}
			return best;
		}

		private void PinGrabbedEnemy() {
			if (_grabbedEnemy == null || !IsInstanceValid(_grabbedEnemy)
				|| _grabbedEnemy.CurrentState == FTT.Enemies.EnemyState.Dead) {
				// The held mob died or vanished mid-hold: let the grab go.
				_grabbedEnemy = null;
				EndGrab(CharacterState.Idle);
				return;
			}
			float facing = IsFacingRight ? 1f : -1f;
			_grabbedEnemy.PinHeldAt(GlobalPosition + new Vector2(facing * GrabReachPixels, 0f));
		}

		private int ResolveStoryThrowDirection() {
			if (CurrentInputFrame.Vertical < -0.3f) return 1;
			float horizontal = CurrentInputFrame.Horizontal;
			float facing = IsFacingRight ? 1f : -1f;
			if (horizontal * facing < -0.3f) return 2;
			return 0;
		}

		private void ResolveStoryThrow() {
			FTT.Enemies.EnemyController victim = _grabbedEnemy;
			_grabbedEnemy = null;
			if (victim == null || !IsInstanceValid(victim)) {
				EndGrab(CharacterState.Idle);
				return;
			}

			// The back throw is a positional reversal: the mob swings to the
			// other side and the player turns around.
			if (_grabThrowDirection == 2) {
				IsFacingRight = !IsFacingRight;
				UpdateSpriteFlip();
				float newFacing = IsFacingRight ? 1f : -1f;
				victim.PinHeldAt(GlobalPosition + new Vector2(newFacing * GrabReachPixels, 0f));
			}

			// 1.0x BasicAttackDamage — priced as position, meter accrues
			// normally through the ordinary chokepoint (a direct hit).
			//
			// V7.6 D03d (Package 11 A1b): this is the PRIMARY hit of a validated
			// paired grab/throw event, so it bypasses the victim's finite
			// shields without consuming them. Story mobs carry neither an Aegis
			// nor a barrier today, so the rule is authored for symmetry with the
			// simulation (where an active Aegis used to absorb the throw
			// outright) rather than to fix a live Story defect. The SECONDARY
			// thrown-mob collision below is a separate projectile hit with
			// normal defenses and must never inherit the bypass.
			int damage = Math.Max(0, (int)MathF.Round(
				(Data?.BasicAttackDamage ?? 10f) * FTT.Combat.BasicComboRules.ThrowDamageMultiplier
				* StoryTemporaryDamageMultiplier));
			int dealt = victim.TakeDamage(damage);
			if (dealt > 0) AddInfluenceFromDamageDealt(dealt);

			float baseKnockback = Data?.BasicAttackKnockback ?? 3f;
			Vector2 impulse = _grabThrowDirection == 1
				? new Vector2(0.8f, -FTT.Combat.BasicComboRules.UpThrowKnockbackMultiplier) * baseKnockback
				: new Vector2(
					FTT.Combat.BasicComboRules.ForwardThrowKnockbackMultiplier,
					-0.6f) * baseKnockback;
			Vector2 launch = FTT.Combat.DamageCalculator.CalculateKnockback(
				impulse,
				victim.Data?.Weight ?? 1f,
				IsFacingRight,
				victim.CurrentHP,
				victim.ScaledMaxHP) * 60f;
			int bowling = Math.Max(1, (int)MathF.Round(
				(Data?.BasicAttackDamage ?? 10f)
				* FTT.Combat.BasicComboRules.ThrownMobCollisionDamageMultiplier));
			victim.LaunchThrown(launch, bowling);
			EndGrab(IsOnFloor() ? CharacterState.Idle : CharacterState.Airborne);
		}

		/// <summary>
		/// Clears grab state without a state transition — for interruptions
		/// (a landed stun, death, a rewind) where another state takes over.
		/// Attack beats grab: the interrupted attempt releases its victim.
		/// </summary>
		private void ReleaseGrabState() {
			if (_grabbedEnemy != null && IsInstanceValid(_grabbedEnemy)) {
				_grabbedEnemy.ReleaseHeld();
			}
			_grabbedEnemy = null;
			_grabPhase = 0;
			_grabPhaseFrames = 0;
		}

		/// <summary>Ends the grab (any phase), releasing a still-held mob.</summary>
		private void EndGrab(CharacterState nextState) {
			if (_grabbedEnemy != null && IsInstanceValid(_grabbedEnemy)) {
				_grabbedEnemy.ReleaseHeld();
			}
			_grabbedEnemy = null;
			_grabPhase = 0;
			_grabPhaseFrames = 0;
			if (CurrentState == CharacterState.Grabbing) TransitionTo(nextState);
		}

		private void ProcessBlocking(float dt) {
			ApplyGravity(dt);
			float maxSpeed = EffectiveMoveSpeed * StatusMovementMultiplier * 60f;
			float step = maxSpeed / GroundDecelRampFrames * dt * 60f;
			var blockVel = Velocity;
			blockVel.X = Mathf.MoveToward(blockVel.X, 0, step);
			Velocity = blockVel;

			_blockSystem?.StartBlock();
			// V7.3 shieldstun: a blocked hit locks the blocker into the stance —
			// no grab, roll, drop-through, or release until it expires.
			bool shieldStunned = _blockSystem?.IsInShieldStun == true;
			// V7.2: from the stance, BasicAttack converts the stance into a
			// grab attempt — the chord repurposes dead input space (attack
			// inputs were ignored while blocking).
			if (!shieldStunned
				&& CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.BasicAttack) && TryStartGrab()) {
				_blockSystem?.EndBlock();
				return;
			}
			if (!shieldStunned && CheckRollInput()) {
				_blockSystem?.EndBlock();
				return;
			}
			// M04 (Package 12 W3): Blocking → Airborne on Jump, outside
			// shieldstun (the sim mirrors it: allowJump no longer excludes the
			// stance, only shieldstun).
			if (!shieldStunned && CheckJumpInput()) {
				_blockSystem?.EndBlock();
				return;
			}

			if (!shieldStunned) CheckDropThrough();
			if (CurrentState != CharacterState.Blocking) {
				_blockSystem?.EndBlock();
				return;
			}

			if (!shieldStunned && !CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.Block)) {
				_blockSystem?.EndBlock();
				TransitionTo(CharacterState.Idle);
				return;
			}

			PlayAnimation("block");
		}

		/// <summary>
		/// F23: the thrown victim's launched ride-out. No input, no actions, no
		/// landing tech — the throw's trajectory is fixed, which is exactly what
		/// distinguishes it from ordinary hitstun. Control returns when the hitstun
		/// the throw applied expires.
		/// </summary>
		private void ProcessThrown(float dt) {
			ApplyGravity(dt);
			_stunTimer -= dt;
			if (_stunTimer > 0f) return;
			_stunTumble = false;
			_stunLeftTheGround = false;
			_hitstunBlockCancelBlocked = false;
			_hasPendingLaunch = false;
			TransitionTo(IsOnFloor() ? CharacterState.Idle : CharacterState.Airborne);
		}

		private void ProcessStunned(float dt) {
			// M05 (Package 12 W3b): the knockdown and get-up are sub-phases of
			// Stunned, owned entirely by ProcessKnockdown.
			if (IsInKnockdownOrGetUp) {
				ProcessKnockdown(dt);
				return;
			}
			ApplyGravity(dt);
			// Track the launch leaving the ground with a per-stun latch.
			// (IsOnFloor() here reflects the PREVIOUS frame's MoveAndSlide, and
			// _wasGrounded is written from the same cached value at frame end —
			// the two could never differ at this point, so the old
			// `IsOnFloor() && !_wasGrounded` landing test was unsatisfiable and
			// the tech never fired. The latch detects the real airborne →
			// grounded transition instead; fixed in the V7.3 pass.)
			if (!IsOnFloor()) _stunLeftTheGround = true;
			// V7.1 landing tech (ukemi): a launched victim (tumble) holding Block
			// on the ground-contact frame techs — hitstun ends in place with a
			// 12-frame invulnerable recovery. Checked before the grounded
			// block-cancel below so the tech's invulnerability grant wins on the
			// landing frame. The Fighter sim's TryLandingTech mirrors this.
			if (_stunTumble && _stunLeftTheGround && IsOnFloor()
				&& CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.Block)) {
				// V7.3 ruling: the tech reads the raw input, not the stance —
				// charges, the shatter lockout, and the hit-2 gate are all
				// irrelevant here.
				_stunTimer = 0f;
				_stunTumble = false;
				_stunLeftTheGround = false;
				_hitstunBlockCancelBlocked = false;
				_hasPendingLaunch = false;
				Velocity = Vector2.Zero;
				_techInvulnerabilitySeconds =
					FTT.Combat.BasicComboRules.LandingTechRecoveryFrames / 60f;
				// V7.3: the recovery is a locked window, not a free Idle — the
				// _techLockoutSeconds gate in _PhysicsProcess holds the player
				// in place with no actions, then releases to Idle (mirrors the
				// sim's TechLockoutFrames branch).
				_techLockoutSeconds =
					FTT.Combat.BasicComboRules.LandingTechRecoveryFrames / 60f;
				return;
			}
			// M05 (Package 12 W3b): the same ground contact WITHOUT Block is a
			// missed tech — the victim is knocked down (the sim's
			// FighterKnockdownRules.TryBeginKnockdown).
			if (_stunTumble && _stunLeftTheGround && IsOnFloor()) {
				BeginKnockdown();
				return;
			}
			// Gameplay-feel plan §2.4 — Block cancels hitstun. A grounded victim
			// holding Block leaves hitstun straight into the block stance; an
			// airborne one cannot (the stance is grounded-only), and Dazed is a
			// separate state so the guard-break punish window is untouched. The
			// Fighter sim clears HitstunFrames on the same condition.
			// V7.3: string hit 1 arms the cancel gate — only from hit two on may
			// Block escape — and a stance the victim cannot raise (no charges,
			// shatter lockout) cannot be escaped into either.
			// M06 (Package 12 W3): tumble is excluded. A launched victim holding
			// Block on ground contact TECHS (above) and never escapes into the
			// stance — including the hit frame itself, where IsOnFloor() still
			// reports the pre-launch ground. The escape is for grounded,
			// non-tumble hitstun only (BasicComboRules.CanBlockEscapeHitstun).
			if (FTT.Combat.BasicComboRules.CanBlockEscapeHitstun(
					grounded: IsOnFloor(),
					blockHeld: CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.Block),
					tumbling: _stunTumble,
					stringHitOneGate: _hitstunBlockCancelBlocked,
					stanceCanRise: _blockSystem == null || _blockSystem.CanRaiseStance)) {
				_stunTimer = 0f;
				_stunTumble = false;
				TransitionTo(CharacterState.Blocking);
				return;
			}
			_stunTimer -= dt;
			if (_stunTimer <= 0) {
				_stunTumble = false;
				_stunLeftTheGround = false;
				_hitstunBlockCancelBlocked = false;
				// Hitstun over: any stashed launch is dead (V7.3, mirrors the
				// sim's clear-on-hitstun-end).
				_hasPendingLaunch = false;
				TransitionTo(IsOnFloor() ? CharacterState.Idle : CharacterState.Airborne);
			}
		}

		private void ProcessDazed(float dt) {
			ApplyGravity(dt);
			_dazeTimer -= dt;
			if (_dazeTimer <= 0) {
				TransitionTo(CharacterState.Idle);
			}
		}

		private void ProcessLedgeHanging(float dt) {
			_ledgeHangTimer -= dt;

			if (_ledgeHangTimer <= 0) {
				DropFromLedge();
				return;
			}

			if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Jump)) {
				ReleaseActiveLedge();
				TransitionTo(CharacterState.Airborne);
				var vel = Velocity;
				vel.Y = -EffectiveJumpForce * StatusJumpMultiplier * 45f;
				Velocity = vel;
				return;
			}

			if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Down)) {
				DropFromLedge();
				return;
			}

			float hAxis = GetHorizontalInput();
			bool towardStage = (IsFacingRight && hAxis > 0.1f) || (!IsFacingRight && hAxis < -0.1f);
			if (towardStage) {
				PlayAnimation("ledge_pull_up");
				if (_activeLedge != null) GlobalPosition = _activeLedge.StandPosition;
				ReleaseActiveLedge();
				TransitionTo(CharacterState.Idle);
				return;
			}

			PlayAnimation("ledge_hang");
		}

		private void DropFromLedge() {
			ReleaseActiveLedge();
			Velocity = new Vector2(Velocity.X, Mathf.Max(90f, Velocity.Y));
			TransitionTo(CharacterState.Airborne);
		}

		private void ReleaseActiveLedge() {
			_activeLedge?.Release(this);
			_activeLedge = null;
		}

		/// <summary>
		/// The two-second input lock. Story's death rewind stopped entering this
		/// state on 2026-09-14 (the landing is invulnerable but controllable);
		/// the state and its timer are retained because the ordinal is a
		/// serialization reservation and the Fighter driver names the pose.
		/// </summary>
		private void ProcessRespawning(float dt) {
			_respawnTimer -= dt;
			if (_respawnTimer <= 0) {
				TransitionTo(CharacterState.Idle);
			}
		}

		private void ProcessUsingMovementAbility(float dt) {
			if (_movementAbility == null || !_movementAbility.IsExecuting) {
				TransitionTo(IsOnFloor() ? CharacterState.Idle : CharacterState.Airborne);
			}
		}

		// === State Transitions ===

		public void TransitionTo(CharacterState newState) {
			// Dead has exactly two exits: Fighter Mode's Respawning, and the
			// Package 12 W1 (R02) Story revive edge Dead -> Idle/Airborne, which is
			// only open while CompleteStoryRewind is placing the hero.
			if (CurrentState == CharacterState.Dead && newState != CharacterState.Respawning
				&& !(_storyReviveEdgeOpen && newState is CharacterState.Idle or CharacterState.Airborne)) return;

			var oldState = CurrentState;
			if (oldState == CharacterState.Rolling && newState != CharacterState.Rolling) CleanupRoll();
			if (oldState == CharacterState.LedgeHanging && newState != CharacterState.LedgeHanging) ReleaseActiveLedge();
			if (oldState == CharacterState.Crouching && newState != CharacterState.Crouching &&
				!(newState == CharacterState.Attacking && _attackStartedCrouched)) {
				RestoreHurtboxHeight();
			}
			if (oldState == CharacterState.Attacking && newState != CharacterState.Attacking && _attackStartedCrouched) {
				RestoreHurtboxHeight();
				_attackStartedCrouched = false;
			}
			CurrentState = newState;
			// Package 12 W1 (R02): every enter/exit is observable, which is how the
			// "Story recovery never touches Respawning" pin watches both edges.
			StateTransitioned?.Invoke(oldState, newState);

			switch (newState) {
				case CharacterState.Rolling:
					_rollFrame = 0;
					_rollInvulnerable = false;
					_pushbox?.SetPushEnabled(true);
					break;
				case CharacterState.Skidding:
					_skidTimer = SkidDuration;
					break;
				case CharacterState.Crouching:
					SetCrouchHurtbox();
					break;
				case CharacterState.LedgeHanging:
					_ledgeHangTimer = LedgeHangMaxTime;
					Velocity = Vector2.Zero;
					break;
				case CharacterState.Dazed:
					_dazeTimer = DazeDuration;
					break;
				case CharacterState.Respawning:
					_respawnTimer = 2.0f;
					break;
				case CharacterState.Airborne:
					GroundComboCounter = 0;
					if (oldState != CharacterState.Attacking) ComboCounter = AerialComboCounter;
					break;
				case CharacterState.Idle:
					if (oldState == CharacterState.Airborne || _attackStartedAerial) AerialComboCounter = 0;
					if (oldState == CharacterState.Attacking) GroundComboCounter = 0;
					ComboCounter = GroundComboCounter;
					break;
				case CharacterState.Dead:
					// V7.6: death clears both status slots (so a Suppression lock
					// can never survive a respawn), and T01a clears every
					// Conductive mark this character sourced — Tesla's coils
					// survive his death, his marks do not.
					ClearAllStatusEffects();
					ClearConductiveMark();
					if (IsInsideTree()) ClearConductiveMarksFrom(GetTree(), PlayerIndex);
					break;
			}
		}

		public void ApplyStun(float duration) => ApplyStun(duration, thrown: false);

		/// <summary>
		/// F23 (Package 11 A1c): the same hitstun entry, told whether the source was
		/// a <b>throw</b>. A thrown victim rides out <see cref="CharacterState.Thrown"/>
		/// instead of <see cref="CharacterState.Stunned"/> — the trajectory is fixed,
		/// so there is no landing tech and no block escape out of it. The Fighter sim
		/// resolves a throw through the same damage chokepoint as any hit; this is
		/// Story's mirror of that victim transition.
		/// </summary>
		public void ApplyStun(float duration, bool thrown) {
			if (CurrentState == CharacterState.Dead || CurrentState == CharacterState.Respawning) return;
			// Being hit cancels the swing and resets the chain (design 752) —
			// and cleans up an active hitbox the state switch alone would leave
			// live. The Fighter sim applies the identical rule on hitstun.
			if (CurrentState == CharacterState.Attacking) CancelActiveAttack();
			ResetComboChain();
			// H-4: a landed stun also cancels an executing special/ultimate
			// outright — no further ticks, steering, or phase advancement.
			// Hyper-armor gating lives at the hit-resolution site
			// (OnHurtboxHit): while an armor window covers the incoming attack
			// class, this method is never reached and the cast completes.
			InterruptActiveAbilities();
			// V7.2: attack beats grab — a landed stun interrupts a grab in any
			// pre-throw phase and releases a held mob.
			ReleaseGrabState();
			_stunTimer = duration;
			// Callers that stun without a launch get no tumble; OnHurtboxHit
			// overrides this right after when the hit carried an impulse (and
			// arms the V7.3 hit-2 cancel gate for string hit 1).
			_stunTumble = false;
			_stunLeftTheGround = false;
			_hitstunBlockCancelBlocked = false;
			// M05: a hit during the (vulnerable) get-up ends it.
			ClearKnockdown();
			TransitionTo(thrown ? CharacterState.Thrown : CharacterState.Stunned);
		}

		/// <summary>
		/// V7.1 hitstop: freezes this character's gameplay clock (state timers,
		/// velocity, position, animation) for the given frames. Max-assign —
		/// an active freeze is never shortened. Dead characters skip: the KO
		/// presentation owns that moment. Numbers come from
		/// <see cref="FTT.Combat.BasicComboRules"/>.
		/// </summary>
		public void ApplyHitstop(int frames) {
			if (frames <= 0 || CurrentState == CharacterState.Dead) return;
			if (frames > _hitstopFramesRemaining) _hitstopFramesRemaining = frames;
		}

		/// <summary>True while the hitstop freeze suspends this character's
		/// gameplay clock. BlockSystem holds its timers on this.</summary>
		public bool IsInHitstop => _hitstopFramesRemaining > 0;

		/// <summary>V7.3: true through the 12-frame landing-tech lock — in
		/// place, invulnerable, no actions — before the release to Idle.</summary>
		public bool IsInTechLockout => _techLockoutSeconds > 0f;

		// === Package 12 W3b region: M05 launch flag, knockdown and get-up ===

		/// <summary>
		/// M05: whether a hit launches this victim. A player-sourced hit (a
		/// basic, a directional strike, a throw, an ability hitbox — anything
		/// with a real owner index) launches only when its payload says so. A
		/// non-player source — enemies, bosses and hazards, which author no
		/// launch flag yet — keeps the legacy "any knockback launches" rule;
		/// recorded under VERIFY-ABILITY-LAUNCHES for a later enemy pass.
		/// </summary>
		internal static bool ResolveHitLaunches(in FTT.Combat.HitPayload hit) =>
			hit.AttackerIndex >= 0 ? hit.Launches : hit.Knockback != Vector2.Zero;

		/// <summary>M05: down on the floor — invulnerable and action-locked.</summary>
		public bool IsKnockedDown => CurrentState == CharacterState.Stunned && _knockdownFrames > 0;

		/// <summary>M05: in the knockdown or its (vulnerable) get-up.</summary>
		public bool IsInKnockdownOrGetUp =>
			CurrentState == CharacterState.Stunned && (_knockdownFrames > 0 || _getUpFrames > 0);

		/// <summary>M05: the chosen get-up (<c>BasicComboRules.GetUp*</c>), <c>GetUpNone</c> while down.</summary>
		public int GetUpKind => _getUpKind;

		/// <summary>M05: knockdown frames left.</summary>
		public int KnockdownFramesRemaining => _knockdownFrames;

		/// <summary>M05: get-up frames left.</summary>
		public int GetUpFramesRemaining => _getUpFrames;

		private void ClearKnockdown() {
			_knockdownFrames = 0;
			_getUpKind = FTT.Combat.BasicComboRules.GetUpNone;
			_getUpFrames = 0;
			_getUpDirection = 0;
		}

		/// <summary>
		/// M05: a tumbling victim touched down without Block — the missed tech.
		/// The tumble's hitstun ends, the body stops, and the 30-frame
		/// invulnerable knockdown starts in the Stunned state.
		/// </summary>
		private void BeginKnockdown() {
			_stunTimer = 0f;
			_stunTumble = false;
			_stunLeftTheGround = false;
			_hitstunBlockCancelBlocked = false;
			_hasPendingLaunch = false;
			Velocity = Vector2.Zero;
			_knockdownFrames = FTT.Combat.BasicComboRules.KnockdownFrames;
			_getUpKind = FTT.Combat.BasicComboRules.GetUpNone;
			_getUpFrames = 0;
			_getUpDirection = 0;
			PlayAnimation("hitstun");
		}

		/// <summary>
		/// M05: one physics frame of the knockdown or the get-up. The get-up is
		/// chosen from the held horizontal direction on the frame the knockdown
		/// ends; neither get-up is invulnerable, and both lock every action.
		/// </summary>
		private void ProcessKnockdown(float dt) {
			ApplyGravity(dt);
			if (_knockdownFrames > 0) {
				Velocity = new Vector2(0f, Velocity.Y);
				PlayAnimation("hitstun");
				_knockdownFrames--;
				if (_knockdownFrames == 0) {
					float horizontal = CurrentInputFrame.Horizontal;
					_getUpDirection = horizontal > FTT.Combat.BasicComboRules.GetUpRollInputThreshold ? 1
						: horizontal < -FTT.Combat.BasicComboRules.GetUpRollInputThreshold ? -1
						: 0;
					_getUpKind = FTT.Combat.BasicComboRules.SelectGetUp(_getUpDirection);
					_getUpFrames = FTT.Combat.BasicComboRules.GetUpFramesFor(_getUpKind);
				}
				return;
			}
			if (_getUpFrames <= 0) return;
			_getUpFrames--;
			if (_getUpKind == FTT.Combat.BasicComboRules.GetUpRoll) {
				float rollSpeed = EffectiveMoveSpeed
					* 60f
					* FTT.Core.UniversalMovementRules.RollSpeedMultiplier;
				Velocity = new Vector2(_getUpDirection * rollSpeed, Velocity.Y);
				PlayAnimation("roll_recovery");
			} else {
				Velocity = new Vector2(0f, Velocity.Y);
				PlayAnimation("idle");
			}
			if (_getUpFrames == 0) {
				ClearKnockdown();
				TransitionTo(IsOnFloor() ? CharacterState.Idle : CharacterState.Airborne);
			}
		}

		// === end Package 12 W3b region ===

		// === Package 11 A5 region: Level 0 scripted-hit exemptions ==========
		// The V7.6 Hitstun Agency Calibration teaches DI and the landing tech
		// with a repeating scripted launch. The design is explicit that the
		// launch is FREE — "no HP, no Rally accounting, no rewind charge" — so
		// it deliberately does NOT travel OnHurtboxHit/ApplyDamage: there is no
		// damage to apply, nothing to echo, and no death to rewind. Everything
		// else about the launch is the ordinary verb layer, including the ±15°
		// DI resolution at hitstop expiry and the real landing-tech window.

		/// <summary>True while the victim is in a launched (techable) tumble.</summary>
		public bool IsInTumble => _stunTumble;

		/// <summary>Frames of hitstop freeze left. Tutorial/DI seam.</summary>
		public int HitstopFramesRemaining => _hitstopFramesRemaining;

		/// <summary>True while a launch impulse is stashed awaiting its DI read.</summary>
		public bool HasPendingLaunch => _hasPendingLaunch;

		/// <summary>
		/// Level 0 only: a scripted launching hit that costs the player nothing.
		/// <paramref name="knockback"/> is in the authored hit-payload scale
		/// (units/frame, as <c>HitPayload.Knockback</c> carries it);
		/// <paramref name="hitstopFrames"/> is the tutorial's deliberately
		/// extended freeze, which is what gives the player time to read the DI
		/// prompt and hold a direction.
		/// </summary>
		public void ApplyTutorialScriptedLaunch(
			Vector2 knockback, float hitstunSeconds, int hitstopFrames) {
			if (CurrentState is CharacterState.Dead or CharacterState.Respawning) return;
			if (knockback == Vector2.Zero || hitstunSeconds <= 0f) return;
			ApplyStun(hitstunSeconds);
			Velocity = knockback * 60f;
			_pendingLaunch = Velocity;
			_hasPendingLaunch = true;
			// A launched stun is a tumble, so the landing tech is live; the
			// V7.3 hit-2 gate never applies to a scripted lesson hit.
			_stunTumble = true;
			_stunLeftTheGround = false;
			_hitstunBlockCancelBlocked = false;
			ApplyHitstop(hitstopFrames);
		}

		// === end Package 11 A5 region (scripted-hit exemptions) ===

		/// <summary>
		/// H-4: interrupts whichever ability slot is mid-cast. Safe to call
		/// unconditionally — <see cref="FTT.Combat.BaseSpecial.Interrupt"/>
		/// no-ops on an inactive ability.
		/// </summary>
		private void InterruptActiveAbilities() {
			_special1?.Interrupt();
			_special2?.Interrupt();
			_movementAbility?.Interrupt();
			_ultimate?.Interrupt();
		}

		public int ApplyDamage(int damage) => ApplyDamage(damage, ignoreRollInvulnerability: false);

		/// <summary>
		/// One scheduled damaging tick of an already-applied status (Venom).
		///
		/// <para>V7.6 D03c (Option B, Package 11 A1b): actual HP-damaging ticks
		/// are eligible for Temporal Aegis and the active HP barrier, in the
		/// D02b order. A status tick is NOT a projectile, so projectile-only
		/// immunity never rejects it, and there is no block opportunity against
		/// an already-attached DoT. An absorbed tick still counts as that
		/// scheduled occurrence: no cleanse, no duration refresh, no delayed
		/// damage debt, no immediate retry. Ticks keep zero hitstop.</para>
		/// </summary>
		public int ApplyPersistentDamage(int damage) {
			// D04: a protected survivor discards the tick rather than banking it.
			if (IsDefyProtected) return 0;
			int remaining = ResolveProtectionLayers(damage);
			if (damage > 0 && remaining <= 0) return 0;
			return ApplyDamage(remaining, ignoreRollInvulnerability: true);
		}

		/// <summary>
		/// V7.3 environmental-damage chokepoint — the Story mirror of the sim's
		/// <c>FighterDamageRules.ApplyUnattributedDamage</c>. Every environmental
		/// source (extractor discharge, drown/searchlight/rift ticks, escape
		/// catches, hazards) routes here instead of calling
		/// <see cref="ApplyDamage(int)"/> raw, so the victim-side pipeline runs:
		/// Defy History consumes its flag (a Defy fired on an environmental path
		/// must never suppress the NEXT hurtbox hit's accounting), Rally echo
		/// accrues, and the victim meter earns the permanent portion. No
		/// attacker credit, and no hitstop unless the source opts in.
		/// </summary>
		public int ApplyEnvironmentalDamage(int damage, bool appliesHitstop = false) {
			// D04: the protected window covers damaging stage hazards too.
			if (IsDefyProtected) return 0;
			// V7.6 D03c: an ordinary external damaging hazard (spikes, lava,
			// stage-hazard ticks) can consume an eligible Aegis or barrier in
			// the D02b order; only the remainder reaches HP.
			int remaining = ResolveProtectionLayers(damage);
			if (damage > 0 && remaining <= 0) return 0;
			int damageApplied = ApplyDamage(remaining);
			// Consume the Defy flag whether or not damage landed — the flag
			// belongs to this hit's accounting alone.
			bool defied = _defyFiredThisHit;
			_defyFiredThisHit = false;
			if (damageApplied <= 0) return damageApplied;

			float echoAmount = 0f;
			if (!defied && CurrentState != CharacterState.Dead && CurrentHP > 0 && MaximumHP > 0) {
				float missing = (MaximumHP - CurrentHP) / (float)MaximumHP;
				// Package 11 A4: the V7.6 RallyEchoFraction lane (Einstein,
				// Joan, Shakespeare, Lincoln) scales the echo share alongside
				// the difficulty multiplier. Story-only - the sim is untouched.
				float fraction = (FTT.Combat.BasicComboRules.EchoFractionBase
						+ FTT.Combat.BasicComboRules.EchoFractionSlope * missing)
					* FTT.Core.StoryDifficultyTuning.GetRallyEchoMultiplier(
						FTT.Core.StoryDifficultyTuning.CurrentStoryDifficulty)
					* StoryRallyEchoFractionMultiplier;
				echoAmount = damageApplied * fraction;
				_echoPool += echoAmount;
				_echoDrainPerFrame = _echoPool / FTT.Combat.BasicComboRules.EchoDrainFrames;
			}
			if (!defied) {
				_ultimateMeter?.AddFromDamageTaken(damageApplied - echoAmount);
				CurrentUltimateMeter = _ultimateMeter?.CurrentValue ?? CurrentUltimateMeter;
			}
			if (appliesHitstop && CurrentState != CharacterState.Dead) {
				ApplyHitstop(FTT.Combat.BasicComboRules.HitstopFrames(damageApplied));
			}
			return damageApplied;
		}

		private int ApplyDamage(int damage, bool ignoreRollInvulnerability) {
			if (CurrentState == CharacterState.Dead || CurrentState == CharacterState.Respawning) return 0;
			if (_postRewindInvulnerabilityFrames > 0) return 0;
			if (_rollInvulnerable && !ignoreRollInvulnerability) return 0;
			// V7.1 landing tech: the 12-frame recovery is fully invulnerable,
			// like the sim's InvulnerabilityFrames grant.
			if (_techInvulnerabilitySeconds > 0f) return 0;
			// M05 (Package 12 W3b): a downed fighter is fully invulnerable, so
			// no hit lands and no meter or Rally is earned from it. The get-up
			// that follows is not protected.
			if (IsKnockedDown) return 0;
			damage = Math.Max(0, (int)MathF.Round(damage * StatusDamageTakenMultiplier));
			// V7.6 D01/D02b (Package 11 A1b): the finite HP barrier is NO LONGER
			// drained here. It used to absorb inside ApplyDamage — after block —
			// which is exactly backwards under D01. Absorption now happens in
			// ResolveIncomingHit (the hurtbox path) and at the DoT/environmental
			// entry points, all BEFORE the block layer.
			int previousHP = CurrentHP;
			CurrentHP = Math.Max(0, CurrentHP - damage);
			int damageApplied = previousHP - CurrentHP;
			// V7.6 D02d (Package 11 A1b): damage that actually reduces Tesla's HP
			// restarts the three-second Wardenclyffe recharge delay, in or out of
			// range. Hits fully rejected by invulnerability, projectile immunity
			// or Aegis never reach here, and a blocked hit that reduces neither
			// shield nor HP does not restart it - both exactly as D02d requires.
			// A throw's HP damage restarts it too, even though the bypass spent
			// no capacity.
			if (damageApplied > 0
				&& _storyShield.EffectId == FTT.Combat.StoryShieldEffect.Wardenclyffe) {
				WardenclyffeDamageDelaySeconds =
					FTT.Combat.StoryDefenseRules.WardenclyffeDamageDelaySeconds;
			}

			// V7.1 Defy History: a lethal hit against a full Ultimate Meter does
			// not kill — the meter shatters to 0 and the player survives at
			// 1 HP. Once per level; fires before the Chronal Rewind would, so
			// no rewind charge is spent. (Should a true blast-zone/pit death
			// path ever be added, it must bypass this — falling is not a hit.)
			if (CurrentHP <= 0 && !_storyDefyHistoryUsed
				&& CurrentUltimateMeter >= FTT.Combat.UltimateMeter.MaxValue) {
				_storyDefyHistoryUsed = true;
				// F10: commit the spent use to the per-attempt registry in the
				// same breath as HP and meter, so a quit-and-resume or a death
				// rewind that rebuilds this controller cannot restore it.
				// Only a live campaign attempt records the spent use; a proc outside
				// one (Test Arena, fixtures) must not leave stale singleton state.
				if (FTT.Core.StoryManager.Instance != null
					&& FTT.Core.StoryManager.Instance.HasLiveAttempt) {
					FTT.Core.StoryManager.Instance.StoryDefyHistoryUsed = true;
				}
				_defyFiredThisHit = true;
				CurrentHP = 1;
				damageApplied = previousHP - CurrentHP;
				_ultimateMeter?.Consume();
				CurrentUltimateMeter = _ultimateMeter?.CurrentValue ?? 0f;
				// The saved life reads as a hard moment: an extended freeze and
				// a heavy shake (the sim applies the same 12-frame hitstop).
				ApplyHitstop(12);
				FTT.Core.CameraShake.Instance?.Shake(10f, 0.35f);
				// V7.6 D04 (Package 11 A1b): protected recovery. The survivor is
				// RELEASED from the triggering hit's forced hitstun, stagger,
				// knockback/launch and capture — it may not be launched or
				// stunned by the very blow it defied — keeps its current legal
				// position (no teleport, no granted jumps, cooldowns or
				// resources), and is invulnerable through the presentation plus
				// 60 active ticks once normal control resumes.
				_defyProtectionAwaitControl = true;
				_defyProtectionFrames = FTT.Combat.StoryDefenseRules.DefyProtectionFrames;
				_defySuppressHitEffects = true;
				_hasPendingLaunch = false;
				_stunTumble = false;
				ReleaseGrabState();
				if (CurrentState == CharacterState.Stunned || CurrentState == CharacterState.Dazed) {
					TransitionTo(IsOnFloor() ? CharacterState.Idle : CharacterState.Airborne);
				}
			}

			FTT.Core.EventBus.Instance?.RaisePlayerHPChanged(new FTT.Core.PlayerHPPayload {
				PlayerIndex = PlayerIndex,
				CurrentHP = CurrentHP,
				MaxHP = MaximumHP,
				DamageAmount = damageApplied
			});

			if (CurrentHP <= 0) {
				// H-4: death cancels the basic swing and any executing
				// special/ultimate unconditionally — hyper-armor prevents
				// hitstun, never death — so no hitbox, steering, or
				// multi-hit sequence survives into the Dead state.
				if (CurrentState == CharacterState.Attacking) CancelActiveAttack();
				InterruptActiveAbilities();
				ReleaseGrabState();
				TransitionTo(CharacterState.Dead);
				FTT.Core.EventBus.Instance?.RaisePlayerDied(PlayerIndex);
			}
			return damageApplied;
		}

		// === Package 11 A3 (V7.6 F16): lethal Story pits ====================

		/// <summary>
		/// Resolves one <b>non-hit</b> death: HP to zero, unconditionally.
		///
		/// This is the chokepoint every lethal opening routes through, and it
		/// exists precisely so a pit author cannot accidentally go through
		/// <see cref="ApplyDamage"/>. Falling is not a hit, so F16 says the
		/// fall must ignore <i>everything</i> a hit would consult: current HP,
		/// the Story shield, hyper-armor, roll and landing-tech invulnerability,
		/// post-rewind invulnerability, and above all <b>Defy History</b> —
		/// whose used flag is left exactly as it was, because a pit must never
		/// spend the run's one saved life. No damage is "applied", so there is
		/// no Rally echo, no victim meter and no hitstop.
		///
		/// Everything downstream is the ordinary death flow: the Dead
		/// transition and <c>OnPlayerDied</c>, which the rewind manager turns
		/// into a spent charge plus a grounded-history rewind, or — with an
		/// empty pool — a Timeline Collapse.
		/// </summary>
		public void KillPlayerNonHit() {
			if (CurrentState == CharacterState.Dead || CurrentState == CharacterState.Respawning) return;
			int previousHP = CurrentHP;
			CurrentHP = 0;
			ClearStoryShield();
			if (CurrentState == CharacterState.Attacking) CancelActiveAttack();
			InterruptActiveAbilities();
			ReleaseGrabState();
			FTT.Core.EventBus.Instance?.RaisePlayerHPChanged(new FTT.Core.PlayerHPPayload {
				PlayerIndex = PlayerIndex,
				CurrentHP = 0,
				MaxHP = MaximumHP,
				DamageAmount = previousHP
			});
			TransitionTo(CharacterState.Dead);
			FTT.Core.EventBus.Instance?.RaisePlayerDied(PlayerIndex);
		}

		/// <summary>
		/// The Story chokepoint every damage-dealer already routes through for
		/// meter-from-damage-dealt. V7.1 Rally rides the same choke.
		///
		/// <para><b>V7.6 D03g (Package 11 A1b).</b> Only a landed <i>direct</i>
		/// hit reclaims: basic and directional strikes, direct Special or
		/// Ultimate impacts, player-fired projectiles (including delayed ones
		/// landing after the firing animation), primary throws, and the
		/// secondary Story thrown-mob collision. <b>Persistent zone ticks do
		/// not</b> — Relativity Rift's chip and every Sandstorm Vortex tick,
		/// explicitly including the Vortex's final launching tick — and neither
		/// do autonomous construct damage, coil-fence ticks, attached DoT ticks
		/// or stage-hazard ticks. Those callers pass
		/// <paramref name="collectsEcho"/> false, mirroring the sim's flag on
		/// ApplyFighterHit. The pre-V7.6 doc comment enshrined the wrong rule by
		/// listing "zone pulse" as a reclaiming source.</para>
		///
		/// <para><b>V7.6 D03f.</b> The reclaim is
		/// <c>min(pool, credited × 2.0, missingHP)</c>, and only the HP actually
		/// healed leaves the pool. The old shape debited the full capped amount
		/// even when the heal was clamped at MaximumHP, so a full-health
		/// attacker burned their whole pool for nothing.</para>
		///
		/// <para><b>V7.6 D03h.</b> <paramref name="ultimateOrigin"/> damage
		/// awards the caster ZERO damage-dealt meter regardless of HP removed,
		/// target count or when it lands. It is a SOURCE rule, not a global
		/// meter lock: an independent non-Ultimate attack, and pre-existing
		/// ordinary construct or status damage, keep their eligibility while an
		/// Ultimate effect survives. Direct-hit Rally reclaim from an Ultimate
		/// impact is retained (D03g).</para>
		/// </summary>
		public void AddInfluenceFromDamageDealt(
			float damageApplied, bool collectsEcho = true, bool ultimateOrigin = false) {
			if (!ultimateOrigin) {
				_ultimateMeter?.AddFromDamageDealt(damageApplied);
				CurrentUltimateMeter = _ultimateMeter?.CurrentValue ?? CurrentUltimateMeter;
			}
			if (!collectsEcho || damageApplied <= 0f || _echoPool <= 0f) return;
			int missing = MaximumHP - CurrentHP;
			if (missing <= 0) return;
			// Reclaimed HP grants no meter to anyone (HealStory is HP-only).
			float reclaimAmount = MathF.Min(
				MathF.Min(
					_echoPool,
					damageApplied * FTT.Combat.BasicComboRules.RallyReclaimDamageMultiplier),
				missing);
			int reclaim = Math.Min(missing, (int)MathF.Round(reclaimAmount));
			if (reclaim <= 0) return;
			int healed = HealStory(reclaim);
			if (healed <= 0) return;
			// Subtract ONLY the HP that actually became health.
			_echoPool = MathF.Max(0f, _echoPool - healed);
			if (_echoPool <= 0.0001f) {
				_echoPool = 0f;
				_echoDrainPerFrame = 0f;
			}
		}

		public void DrainUltimateMeter(float points) {
			if (_ultimateMeter != null) {
				_ultimateMeter.SetValue(_ultimateMeter.CurrentValue - Mathf.Max(0f, points));
				CurrentUltimateMeter = _ultimateMeter.CurrentValue;
			} else {
				CurrentUltimateMeter = Mathf.Max(0f, CurrentUltimateMeter - Mathf.Max(0f, points));
			}
		}

		public int HealStory(int amount) {
			if (CurrentState == CharacterState.Dead || amount <= 0) return 0;
			int previousHP = CurrentHP;
			CurrentHP = Math.Min(MaximumHP, CurrentHP + amount);
			int healed = CurrentHP - previousHP;
			if (healed > 0) {
				FTT.Core.EventBus.Instance?.RaisePlayerHPChanged(new FTT.Core.PlayerHPPayload {
					PlayerIndex = PlayerIndex,
					CurrentHP = CurrentHP,
					MaxHP = MaximumHP,
					DamageAmount = -healed
				});
			}
			return healed;
		}

		public void ApplyStoryDamageBuff(float multiplier, float durationSeconds) {
			StoryTemporaryDamageMultiplier = Math.Max(1f, multiplier);
			_temporaryDamageBuffFrames = Math.Max(0, Mathf.RoundToInt(durationSeconds * 60f));
			if (_temporaryDamageBuffFrames == 0) StoryTemporaryDamageMultiplier = 1f;
		}

		public void ApplyStorySpeedBuff(float multiplier, float durationSeconds) {
			StoryTemporarySpeedMultiplier = Math.Max(1f, multiplier);
			_temporarySpeedBuffFrames = Math.Max(0, Mathf.RoundToInt(durationSeconds * 60f));
			if (_temporarySpeedBuffFrames == 0) StoryTemporarySpeedMultiplier = 1f;
		}

		private void UpdateStoryTemporaryEffects() {
			if (_temporaryDamageBuffFrames > 0 && --_temporaryDamageBuffFrames == 0) {
				StoryTemporaryDamageMultiplier = 1f;
			}
			if (_temporarySpeedBuffFrames > 0 && --_temporarySpeedBuffFrames == 0) {
				StoryTemporarySpeedMultiplier = 1f;
			}
			if (_storyProjectileImmunityFrames > 0) _storyProjectileImmunityFrames--;
			if (_postRewindInvulnerabilityFrames > 0) _postRewindInvulnerabilityFrames--;
			if (_storyHyperArmorFrames > 0) _storyHyperArmorFrames--;
		}

		public void ApplyStockLossMeterRetention() {
			_ultimateMeter?.ApplyStockLossRetention();
			CurrentUltimateMeter = _ultimateMeter?.CurrentValue ?? CurrentUltimateMeter * 0.75f;
		}

		public bool HasActiveHyperArmor =>
			StoryHyperArmorActive ||
			AbilityHasActiveHyperArmor(_special1) ||
			AbilityHasActiveHyperArmor(_special2) ||
			AbilityHasActiveHyperArmor(_movementAbility) ||
			AbilityHasActiveHyperArmor(_ultimate);

		public bool HasActiveHyperArmorAgainst(FTT.Combat.AttackClass attackClass) =>
			(StoryHyperArmorActive && attackClass != FTT.Combat.AttackClass.Ultimate) ||
			AbilityHasActiveHyperArmor(_special1, attackClass) ||
			AbilityHasActiveHyperArmor(_special2, attackClass) ||
			AbilityHasActiveHyperArmor(_movementAbility, attackClass) ||
			AbilityHasActiveHyperArmor(_ultimate, attackClass);

		private static bool AbilityHasActiveHyperArmor(FTT.Combat.BaseSpecial ability) =>
			AbilityHasActiveHyperArmor(ability, FTT.Combat.AttackClass.Basic);

		private static bool AbilityHasActiveHyperArmor(
			FTT.Combat.BaseSpecial ability,
			FTT.Combat.AttackClass incomingAttackClass) =>
			ability != null && FTT.Combat.StoryCombatRules.HyperArmorPreventsInterruption(
				ability.Data?.GrantsHyperArmor == true,
				ability.CurrentPhase,
				incomingAttackClass);

		public string ActiveAnimationName => _animatedSprite?.Animation ?? "idle";

		public void PlayPresentationAnimation(string animationName) {
			if (_animatedSprite == null || string.IsNullOrWhiteSpace(animationName)) return;
			// Same-name guard so per-frame presentation drivers do not restart
			// the animation at frame zero every tick.
			if (_animatedSprite.Animation == animationName) return;
			if (_animatedSprite.SpriteFrames?.HasAnimation(animationName) == true) _animatedSprite.Play(animationName);
		}

		/// <summary>
		/// Applies <see cref="IsFacingRight"/> to the sprite. Fighter-mode
		/// presentation entry point: the driver writes the field from sim state
		/// and native processing (which normally flips the sprite) is disabled.
		/// </summary>
		public void SyncPresentationFacing() => UpdateSpriteFlip();

		// === V7.6 Time Freeze (F03) ===========================================

		/// <summary>
		/// True while the Story player's Time Freeze holds the world.
		///
		/// <para><b>This is the opposite of <see cref="SetRewindSuspended"/>.</b> A
		/// rewind suspends the PLAYER while the world is restored; a Time Freeze
		/// stops the WORLD while the player keeps playing. Running, jumping, the
		/// fast-fall and the universal evasive roll all stay available.</para>
		///
		/// <para>Refused for the whole freeze, and <b>discarded rather than
		/// buffered</b> so nothing bursts out on thaw: basic attacks, grabs, both
		/// specials, the ultimate, Echo Step, the character Movement Ability and
		/// world interaction. Block-charge regeneration, ability cooldowns and
		/// status durations are suspended for the same window; movement and the
		/// freeze's own five-second clock keep running.</para>
		/// </summary>
		public bool TimeFrozen { get; set; }

		public void SetRewindSuspended(bool suspended) {
			if (_rewindSuspended == suspended) return;
			_rewindSuspended = suspended;
			if (suspended) {
				_rewindCollisionLayer = CollisionLayer;
				_rewindCollisionMask = CollisionMask;
				CollisionLayer = 0;
				CollisionMask = 0;
				if (_hurtbox != null) {
					// Safe setters: a lethal-hit rewind suspends the player from
					// inside the hit signal's physics flush, where the direct
					// writes are engine-blocked.
					_hurtbox.SetMonitoringSafe(false);
					_hurtbox.SetMonitorableSafe(false);
				}
				_pushbox?.SetPushEnabled(false);
				Velocity = Vector2.Zero;
				this.SetProcessModeSafe(ProcessModeEnum.Disabled);
			} else {
				CollisionLayer = _rewindCollisionLayer;
				CollisionMask = _rewindCollisionMask;
				if (_hurtbox != null) {
					_hurtbox.SetMonitoringSafe(true);
					_hurtbox.SetMonitorableSafe(true);
				}
				_pushbox?.SetPushEnabled(true);
				this.SetProcessModeSafe(ProcessModeEnum.Inherit);
			}
		}

		private void CleanupRoll() {
			_rollInvulnerable = false;
			_pushbox?.SetPushEnabled(true);
			_pushbox?.ResolveStoryOverlaps(_rollDirection);
		}

		/// <summary>
		/// Places the hero at a Story recovery landing and hands control straight
		/// back. <paramref name="grounded"/> is the landing sample's own floor
		/// contact (the rewind buffer records it per frame); null falls back to
		/// <see cref="CharacterBody2D.IsOnFloor"/>.
		///
		/// <para><b>R02 (Package 12 W1):</b> the revive is the explicit Story edge
		/// <c>Dead -> Idle/Airborne</c>; <see cref="CharacterState.Respawning"/> is
		/// Fighter-only and is never entered or exited here. <b>R03:</b> the 2 s
		/// recovery protection is NOT armed here any more — it starts at the thaw
		/// that ends the Post-Landing Hold (<see cref="EndRecoveryHold"/>).</para>
		/// </summary>
		public void CompleteStoryRewind(Vector2 landingPosition, int restoredHP, bool? grounded = null) {
			GlobalPosition = landingPosition;
			Velocity = Vector2.Zero;
			CurrentHP = Math.Clamp(restoredHP, 1, MaximumHP);
			// A rewind wipes any in-flight verb state (hitstop freeze, stashed
			// launch, tumble, Rally echo) — the restored timeline never took
			// that hit. The Defy History flag deliberately survives: once per
			// level, not once per life.
			_hitstopFramesRemaining = 0;
			_hasPendingLaunch = false;
			_stunTumble = false;
			_stunLeftTheGround = false;
			_techInvulnerabilitySeconds = 0f;
			_techLockoutSeconds = 0f;
			ClearKnockdown();
			_echoPool = 0f;
			_echoDrainPerFrame = 0f;
			// V7.6 T01a/D02c/D02e/D04 (Package 11 A1b): a Death Rewind clears the
			// hero's TEMPORARY protection - the granted HP barrier, the Aegis
			// bubble and the transient Defy window - while retaining actual
			// block resources. The Defy History SPENT flag deliberately survives
			// (once per attempt, not once per life). Wardenclyffe's stored charge
			// is discarded with the barrier but its remaining damage delay is
			// preserved, so a reload cannot accelerate recovery (D02d).
			ClearStoryShield();
			ClearTemporalAegis();
			ClearDefyProtection();
			ReleaseGrabState();
			// V7.6 (Package 11 A1c): a death rewind is a forced relocation to a new
			// recovery anchor, so the Echo Step history starts a new generation at
			// the landing. Without this the next Echo Step would snap the player
			// back into the timeline they were just pulled out of — possibly into
			// the hazard that killed them.
			ResetEchoStepHistory();
			// Package 11 A4: a stock loss / respawn clears every per-airtime
			// Resonance traversal latch and the Shield of Orleans dedup ledger.
			ResetAirtimeTraversalLatches();
			_shieldOfOrleansExecutions.Clear();
			if (_animatedSprite != null) _animatedSprite.SpeedScale = 1f;
			SetRewindSuspended(false);
			// R03: any protection left over from an earlier recovery is not
			// carried through this one; the new entitlement is granted at the thaw.
			_postRewindInvulnerabilityFrames = 0;
			// 2026-09-14 (user direction): the landing hands control straight
			// back — there is no post-landing input lock. Package 12 W1 (R02):
			// the revive is the Story edge Dead -> Idle/Airborne chosen by floor
			// contact, so no Respawning enter or exit ever fires on a Story
			// recovery. A scripted (living) demonstration takes the same
			// ordinary transition.
			ReviveFromStoryRecovery(grounded ?? IsOnFloor());
			FTT.Core.EventBus.Instance?.RaisePlayerHPChanged(new FTT.Core.PlayerHPPayload {
				PlayerIndex = PlayerIndex,
				CurrentHP = CurrentHP,
				MaxHP = MaximumHP,
				DamageAmount = 0
			});
		}

		// === Package 12 W1 — Story revive edge (R02) and the Post-Landing Hold (R03)

		private bool _storyReviveEdgeOpen;

		/// <summary>
		/// Raised on every accepted <see cref="TransitionTo"/> with (old, new).
		/// Presentation and test observers only; nothing gameplay-authoritative
		/// may hang off it.
		/// </summary>
		public event Action<CharacterState, CharacterState> StateTransitioned;

		/// <summary>
		/// R02: the explicit Story revive edge. A dead hero leaves Dead straight to
		/// Idle (grounded) or Airborne; a living hero (the Level 0 scripted
		/// demonstration) takes the same ordinary transition.
		/// </summary>
		private void ReviveFromStoryRecovery(bool grounded) {
			_storyReviveEdgeOpen = true;
			try {
				TransitionTo(grounded ? CharacterState.Idle : CharacterState.Airborne);
			} finally {
				_storyReviveEdgeOpen = false;
			}
		}

		/// <summary>
		/// True for the whole R03 Post-Landing Hold and for a boss's T01b
		/// world suspension: the hero is live but the world is not. Interaction
		/// (pickups, the Restoration Font, puzzle controls, checkpoint prompts)
		/// waits for the thaw; <see cref="FTT.Environment.InteractionArea"/> reads
		/// this. Transient — never persisted.
		/// </summary>
		public bool IsRecoveryWorldHeld { get; private set; }

		private FTT.Core.StoryRecoveryHoldCause _recoveryHoldCause;
		private int _recoveryHoldFrames;

		/// <summary>
		/// R03: control has returned and the Post-Landing Hold begins. Raises
		/// <c>OnRecoveryLanded</c>. Recovery protection is deliberately not armed
		/// yet — frozen actors cannot hurt the hero, and the 2 s window must face
		/// live enemies.
		/// </summary>
		public void BeginRecoveryHold(FTT.Core.StoryRecoveryHoldCause cause, int holdFrames) {
			IsRecoveryWorldHeld = true;
			// Old protection never carries into a new recovery; the new
			// entitlement starts at this hold's thaw.
			_postRewindInvulnerabilityFrames = 0;
			_recoveryHoldCause = cause;
			_recoveryHoldFrames = holdFrames;
			FTT.Core.EventBus.Instance?.RaiseRecoveryLanded(CreateRecoveryHoldPayload());
		}

		/// <summary>
		/// R03: the world thawed. Arms the 2.0 s recovery protection as a new
		/// entitlement starting now, and raises <c>OnRecoveryWorldThawed</c>.
		/// Idempotent: a second call without a new hold does nothing.
		/// </summary>
		public void EndRecoveryHold() {
			if (!IsRecoveryWorldHeld) return;
			IsRecoveryWorldHeld = false;
			ArmRecoveryProtection();
			FTT.Core.EventBus.Instance?.RaiseRecoveryWorldThawed(CreateRecoveryHoldPayload());
		}

		/// <summary>
		/// Drops a hold without thawing into protection — the hero died inside
		/// it (a fall), so the next recovery owns the landing.
		/// </summary>
		public void CancelRecoveryHold() => IsRecoveryWorldHeld = false;

		/// <summary>Grants the 2.0 s (120-frame) recovery protection from this tick.</summary>
		public void ArmRecoveryProtection() =>
			_postRewindInvulnerabilityFrames = StoryRewindInvulnerabilityFrames;

		private FTT.Core.RecoveryHoldPayload CreateRecoveryHoldPayload() => new() {
			PlayerIndex = PlayerIndex,
			Cause = _recoveryHoldCause,
			HoldFrames = _recoveryHoldFrames,
			Position = GlobalPosition
		};

		private bool _worldSuspended;
		private ProcessModeEnum _worldSuspendedProcessMode;

		/// <summary>
		/// GAP-06 (T01b): the boss's Historical Recovery beat suspends the hero
		/// too — position, velocity, HP, resources and action progress are
		/// preserved exactly, and every hero clock holds. Unlike
		/// <see cref="SetRewindSuspended"/> nothing is zeroed; the node's whole
		/// subtree simply stops processing and resumes where it stopped.
		/// </summary>
		public void SetWorldSuspended(bool suspended) {
			if (_worldSuspended == suspended) return;
			_worldSuspended = suspended;
			if (suspended) {
				_worldSuspendedProcessMode = ProcessMode;
				IsRecoveryWorldHeld = true;
				this.SetProcessModeSafe(ProcessModeEnum.Disabled);
			} else {
				IsRecoveryWorldHeld = false;
				this.SetProcessModeSafe(_worldSuspendedProcessMode);
			}
		}

		/// <summary>True while a boss's T01b beat holds the hero. Test seam.</summary>
		public bool IsWorldSuspended => _worldSuspended;

		public void RestoreStoryCheckpoint(Vector2 position, int hp, float ultimateMeter) {
			GlobalPosition = position;
			Velocity = Vector2.Zero;
			CurrentHP = Math.Clamp(hp, 1, MaximumHP);
			// Checkpoint reconstruction discards old movement history (V7.6 temporal
			// contract): a new Echo Step generation opens here too.
			ResetEchoStepHistory();
			_ultimateMeter?.SetValue(ultimateMeter);
			CurrentUltimateMeter = _ultimateMeter?.CurrentValue ?? Mathf.Clamp(ultimateMeter, 0f, 100f);
			FTT.Core.EventBus.Instance?.RaisePlayerHPChanged(new FTT.Core.PlayerHPPayload {
				PlayerIndex = PlayerIndex,
				CurrentHP = CurrentHP,
				MaxHP = MaximumHP,
				DamageAmount = 0
			});
		}

		// === Movement Helpers ===

		private void ApplyGravity(float dt) {
			if (IsOnFloor()) {
				StoryFloatTimer = 0f;
				return;
			}

			float effectiveGravity = BaseGravity * (0.8f + 0.4f * (Data?.Weight ?? 1.0f)) * EnvironmentGravityScale;
			float multiplier = 1.0f;

			if (StoryFloatTimer > 0f) {
				StoryFloatTimer = Mathf.Max(0f, StoryFloatTimer - dt);
				multiplier = 0.15f;
				if (Velocity.Y > 0) {
					Velocity = new Vector2(Velocity.X, Mathf.Min(Velocity.Y, 60f));
				}
			} else if (Velocity.Y > 0) {
				multiplier = FallGravityMultiplier;
			} else if (!_jumpHeld && Velocity.Y < 0) {
				multiplier = ShortHopGravityMultiplier;
			}

			var vel = Velocity;
			vel.Y += effectiveGravity * multiplier * dt * 60f;
			vel.Y = Mathf.Min(vel.Y, TerminalVelocity);
			Velocity = vel;
		}

		private void ApplyHorizontalMovement(float hAxis, float dt) {
			float maxSpeed = EffectiveMoveSpeed * StatusMovementMultiplier * 60f;
			float targetSpeed = hAxis * maxSpeed;
			// Grounded movement runs two ramps (§2.1): the 14-frame accel ramp
			// when building speed toward a same-signed target, the 12-frame
			// decel ramp when the target is zero or reverses against current
			// velocity. Mirrors the Fighter sim's ApplyNormalMovement selection.
			bool decelerating = Mathf.IsZeroApprox(targetSpeed)
				|| (targetSpeed > 0f && Velocity.X < 0f)
				|| (targetSpeed < 0f && Velocity.X > 0f);
			float rampFrames = decelerating ? GroundDecelRampFrames : GroundRampFrames;
			float step = maxSpeed / rampFrames * dt * 60f;
			var vel = Velocity;
			vel.X = Mathf.MoveToward(vel.X, targetSpeed, step);
			Velocity = vel;
		}

		private void ApplyFriction(float dt) {
			float friction = (Data?.GroundFriction ?? 20f) * dt * 60f;
			var vel = Velocity;
			vel.X = Mathf.MoveToward(vel.X, 0, friction);
			Velocity = vel;
		}

		private void ApplyAirControl(float dt) {
			float hAxis = GetHorizontalInput();
			float maxSpeed = EffectiveMoveSpeed * StatusMovementMultiplier * 60f;
			float targetSpeed = hAxis * maxSpeed;
			bool isAccelerating = Mathf.Abs(targetSpeed) >= Mathf.Abs(Velocity.X)
				|| (targetSpeed > 0 && Velocity.X < 0) || (targetSpeed < 0 && Velocity.X > 0);
			// The 4-frame air ramp is the designed constant the Fighter sim uses;
			// the 8-frame ground ramp was accidental here (audit M-17).
			float rampFrames = isAccelerating ? AirAccelRampFrames : AirDecelRampFrames;
			// V7.2 wiring: AirControlMultiplier was authored on all nine characters
			// (0.4 Lincoln .. 0.75 Pocahontas) and read by nothing — it scales how
			// quickly held input changes airborne velocity (aerial identity).
			float step = maxSpeed / rampFrames * dt * 60f * (Data?.AirControlMultiplier ?? 1f);
			var vel = Velocity;
			vel.X = Mathf.MoveToward(vel.X, targetSpeed, step);
			Velocity = vel;
		}

			// === Input Helpers ===

		private float GetHorizontalInput() {
			return IsMovementRooted ? 0.0f : CurrentInputFrame.Horizontal;
		}

		private bool CheckRollInput() {
			if (IsMovementRooted || !IsOnFloor()) return false;
			if (!CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Roll)) return false;
			float input = GetHorizontalInput();
			_rollDirection = Mathf.Abs(input) > 0.1f ? Math.Sign(input) : (IsFacingRight ? 1 : -1);
			TransitionTo(CharacterState.Rolling);
			return true;
		}

		private bool CheckJumpInput() {
			if (IsMovementRooted) return false;
			_jumpHeld = CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.Jump);

			if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Jump)) {
				_jumpBufferTimer = JumpBufferTime;
			}
			_jumpBufferTimer -= (float)GetPhysicsProcessDeltaTime();

			if (_jumpBufferTimer > 0) {
				if (IsOnFloor() || _coyoteTimer > 0 || RemainingJumps > 0) {
					PerformJump();
					_jumpBufferTimer = 0;
					return true;
				}
			}
			return false;
		}

		private void PerformJump() {
			var vel = Velocity;
			vel.Y = -EffectiveJumpForce * StatusJumpMultiplier * 54f;
			Velocity = vel;
			RemainingJumps--;
			_coyoteTimer = 0;
			TransitionTo(CharacterState.Airborne);
		}

		private bool CheckAttackInput() {
			// V7.6 Time Freeze: the swing (and the grab chord below it) is
			// discarded outright — never buffered, so thaw brings no burst.
			if (TimeFrozen) return false;
			// V7.2: BasicAttack while Block is held is the GRAB chord — from
			// neutral the same-frame press grabs (no block rises, no swing
			// fires). Grounded only, like the stance it answers.
			if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.BasicAttack)
				&& CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.Block)
				&& IsOnFloor()) {
				return TryStartGrab();
			}
			if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.BasicAttack)) {
				_attackStartedAerial = !IsOnFloor() || CurrentState == CharacterState.Airborne;
				_attackStartedCrouched = CurrentState == CharacterState.Crouching;
				// Gameplay feel §2.8 — one shared selection rule with the Fighter
				// simulation. Up wins; airborne Down is the down-air; grounded
				// Down (including the Crouching state) is the normal string.
				_attackVariant = FTT.Combat.BasicComboRules.SelectAttackVariant(
					upHeld: CurrentInputFrame.Vertical < FTT.Combat.BasicComboRules.StoryUpInputThreshold,
					downHeld: CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.Down),
					airborne: _attackStartedAerial);
				if (_attackStartedAerial) {
					AerialComboCounter = 0;
					GroundComboCounter = 0;
				} else {
					GroundComboCounter = 0;
				}
				ComboCounter = 0;
				StartComboHit();
				return true;
			}
			return false;
		}

		private void StartComboHit() {
			int comboIdx = GetActiveComboIndex();
			ComboCounter = comboIdx;
			_attackFramesRemaining = GetActiveComboTimeline().TotalFrames;
			_attackHitActive = false;
			_attackInRecovery = false;
			_nextAttackBuffered = false;
			_inRecoveryHold = false;

			if (_attackVariant != FTT.Combat.BasicComboRules.VariantChain) {
				StartDirectionalAttack();
				return;
			}

			if (_meleeHitbox != null) {
				float baseDmg = (Data?.BasicAttackDamage ?? 10f) * StoryBasicDamageMultiplier;
				_meleeHitbox.Damage = baseDmg * (_stringProfile.DamageTenths[comboIdx] / 10f);
				_meleeHitbox.AttackID = $"{Data?.CharacterID ?? "fighter"}.basic";
				_meleeHitbox.HitboxID = $"combo_{comboIdx + 1}";
				// Package 11 A4: the V6 Kinetic Splitting rider that re-classed
				// combo hit 3 as Special is RETIRED - it restated the baseline
				// and granted nothing (recon F §6.8). The V7.6 perk lives
				// entirely on LincolnSplittingStrike (the ground bounce and the
				// +50% against Extractors and enemy constructs).
				_meleeHitbox.AttackClass = FTT.Combat.AttackClass.Basic;
				// M08 (Package 12 W3): the string's authored hit contract.
				// M07 (Package 12 W3b): read through the profile, so Lincoln's
				// hit 2 launches in Story exactly as it does in the sim.
				_meleeHitbox.Launches = FTT.Combat.BasicComboRules.StringHitLaunchesFor(_stringProfile, comboIdx);
				_meleeHitbox.Delivery = FTT.Combat.HitDelivery.DirectHit;
				_meleeHitbox.Origin = FTT.Combat.HitOrigin.Basic;

				float baseKB = Data?.BasicAttackKnockback ?? 3f;
				// Hitstun and the horizontal knockback multiplier both come from
				// the shared rulebook, the same tables the Fighter sim applies.
				// The stun holds the victim through the chain gap to the next
				// hit; the finisher launches them away.
				_meleeHitbox.HitstunDuration = FTT.Combat.BasicComboRules.HitstunFrames[comboIdx] / 60f;
				bool finisher = comboIdx == 2;
				// V7.1 string riders: the finisher's authored knockback tenths
				// (Mozart's 5.5x shove) and hit 2's vertical launch scale
				// (Lincoln's heavy upward swing) come from the string profile;
				// the vertical components track their multipliers so every
				// profile keeps the template's launch angles.
				float kbMultiplier = finisher
					? _stringProfile.FinisherKnockbackTenths / 10f
					: FTT.Combat.BasicComboRules.KnockbackMultipliers[comboIdx];
				if (finisher) {
					_meleeHitbox.KnockbackForce = new Vector2(baseKB * kbMultiplier, -2f * kbMultiplier);
				} else if (comboIdx == 1) {
					_meleeHitbox.KnockbackForce = new Vector2(
						baseKB * kbMultiplier,
						-1.5f * (_stringProfile.Hit2VerticalLaunchTenths / 10f));
				} else {
					_meleeHitbox.KnockbackForce = new Vector2(baseKB * kbMultiplier, -1f);
				}

				// V7.1 string riders: only the authored finisher status (Tesla's
				// priming Static Charge, Cleopatra's venom mark). Earlier hits
				// always clear the shared hitbox's status fields so nothing
				// leaks between swings.
				_meleeHitbox.AppliedStatus = finisher
					? (FTT.Core.StatusType)_stringProfile.FinisherStatusType
					: FTT.Core.StatusType.None;
				_meleeHitbox.StatusDuration = finisher
					? _stringProfile.FinisherStatusFrames / 60f
					: 0f;
				_meleeHitbox.StatusIntensity = finisher
					? _stringProfile.FinisherStatusIntensityMilli / 1000f
					: 1f;
				// V7.6 F07 rider: the finisher's caster-owned combo MARK, applied
				// alongside (never instead of) the status. Tesla's Conductive mark
				// rides here at 90 frames — 150 with the Story-only
				// tesla_conductive_hold Resonance node, scaled at this application
				// site so the cross-mode BasicComboRules table stays untouched.
				_meleeHitbox.ComboMark = finisher
					? (FTT.Combat.ComboMarkType)_stringProfile.FinisherMarkType
					: FTT.Combat.ComboMarkType.None;
				_meleeHitbox.ComboMarkFrames = finisher ? FinisherConductiveMarkFrames() : 0;
			}

			if (_aerialHitboxMarker != null && _attackStartedAerial) {
				Vector2 offset = AerialComboHitboxOffsets[comboIdx];
				_aerialHitboxMarker.Position = new Vector2(IsFacingRight ? offset.X : -offset.X, offset.Y);
			}

			TransitionTo(CharacterState.Attacking);
			PlayAnimation($"basic_attack_{comboIdx + 1}");
			string animationName = $"basic_{(_attackStartedAerial ? "air" : "ground")}_{comboIdx + 1}";
			// The shared combat-animation library is authored to the template
			// string; a hit whose authored startup deviates (V7.1 string
			// profiles) runs on the frame clock so per-character timing stays
			// authoritative while the sheet remains presentation.
			bool templateTiming = _attackStartedAerial
				? _stringProfile.AerialStartupFrames[comboIdx]
					== FTT.Combat.BasicComboRules.AerialStartupFrames[comboIdx]
				: _stringProfile.GroundStartupFrames[comboIdx]
					== FTT.Combat.BasicComboRules.GroundStartupFrames[comboIdx];
			_attackAnimationDriven = templateTiming
				&& _combatAnimationPlayer?.HasAnimation(animationName) == true;
			if (_attackAnimationDriven) {
				// Story-only ComboSpeed minors run the basic string faster; the
				// animation clock carries the hit-activation callbacks with it.
				_combatAnimationPlayer.SpeedScale = StoryComboSpeedMultiplier;
				_combatAnimationPlayer.Play(animationName);
			}
			_attackFrameProgress = 0f;
		}

		/// <summary>
		/// Arms the up-attack or down-air (§2.8): a single strike outside the
		/// three-hit chain. Damage, hitstun and both knockback components come
		/// from <see cref="FTT.Combat.BasicComboRules"/>, the same table the
		/// Fighter simulation reads. Both variants have dedicated retro sprite
		/// animations; gameplay timing remains on the frame clock.
		/// </summary>
		private void StartDirectionalAttack() {
			bool upAttack = _attackVariant == FTT.Combat.BasicComboRules.VariantUpAttack;
			if (_meleeHitbox != null) {
				float baseDmg = (Data?.BasicAttackDamage ?? 10f) * StoryBasicDamageMultiplier;
				_meleeHitbox.Damage = baseDmg * FTT.Combat.BasicComboRules.DirectionalAttackDamageMultiplier;
				_meleeHitbox.AttackID = $"{Data?.CharacterID ?? "fighter"}.basic";
				_meleeHitbox.HitboxID = upAttack
					? FTT.Combat.BasicComboRules.UpAttackHitboxID
					: FTT.Combat.BasicComboRules.DownAirHitboxID;
				_meleeHitbox.AttackClass = FTT.Combat.AttackClass.Basic;
				_meleeHitbox.Launches = FTT.Combat.BasicComboRules.DirectionalAttackLaunches;
				_meleeHitbox.Delivery = FTT.Combat.HitDelivery.DirectHit;
				_meleeHitbox.Origin = FTT.Combat.HitOrigin.Basic;
				_meleeHitbox.HitstunDuration =
					FTT.Combat.BasicComboRules.DirectionalAttackHitstunFrames / 60f;
				// Directional strikes carry no rider status; clear the shared
				// hitbox so a finisher's authored status cannot leak in.
				_meleeHitbox.AppliedStatus = FTT.Core.StatusType.None;
				_meleeHitbox.StatusDuration = 0f;
				_meleeHitbox.StatusIntensity = 1f;
				// Both strikes launch: a small horizontal nudge and a strong
				// upward component (Godot 2D Y is down, so upward is negative).
				float baseKB = Data?.BasicAttackKnockback ?? 3f;
				_meleeHitbox.KnockbackForce = new Vector2(
					baseKB * FTT.Combat.BasicComboRules.DirectionalAttackHorizontalKnockback,
					-baseKB * FTT.Combat.BasicComboRules.DirectionalAttackVerticalKnockback);
			}

			TransitionTo(CharacterState.Attacking);
			PlayAnimation(upAttack ? "up_attack" : "down_attack");
			// Directional gameplay timing is fixed by BasicComboRules rather than
			// animation callbacks, so only presentation uses the authored sheet.
			_attackAnimationDriven = false;
			_attackFrameProgress = 0f;
		}

		public void OnAttackActiveStarted() {
			if (CurrentState != CharacterState.Attacking || _attackHitActive) return;
			_attackHitActive = true;
			int comboIdx = GetActiveComboIndex();
			bool directional = _attackVariant != FTT.Combat.BasicComboRules.VariantChain;
			bool upAttack = _attackVariant == FTT.Combat.BasicComboRules.VariantUpAttack;
			// Reach = template pixels x the authored V7.1 profile scale (chain
			// hits only; directional strikes stay universal) x the Story-only
			// AttackRange Resonance minor.
			Vector2 profileScale = directional
				? Vector2.One
				: new Vector2(
					_stringProfile.ReachWidthPercent / 100f,
					_stringProfile.ReachHeightPercent / 100f);
			Vector2 hitboxSize = (directional
				? (upAttack ? UpAttackHitboxSize : DownAirHitboxSize)
				: _attackStartedAerial
					? AerialComboHitboxSizes[comboIdx]
					: ComboHitboxSizes[comboIdx]) * profileScale * StoryAttackRangeMultiplier;
			Vector2 hitboxOffset = directional
				? (upAttack ? UpAttackHitboxOffset : DownAirHitboxOffset)
				: _attackStartedAerial
					? (_aerialHitboxMarker?.Position ?? AerialComboHitboxOffsets[comboIdx])
					: ComboHitboxOffsets[comboIdx];
			float facingMul = IsFacingRight ? 1f : -1f;
			// The directional boxes are centred on the fighter, so facing does
			// not mirror them (their X offset is zero by construction). Chain
			// offsets scale with the profile width so a wider box extends the
			// front edge instead of growing backward through the fighter.
			Vector2 resolvedOffset = directional
				? hitboxOffset
				: _attackStartedAerial
					? new Vector2(hitboxOffset.X * profileScale.X, hitboxOffset.Y)
					: new Vector2(hitboxOffset.X * profileScale.X * facingMul, hitboxOffset.Y);

			if (_meleeHitbox?.GetChildCount() > 0 && _meleeHitbox.GetChild(0) is CollisionShape2D hitShape) {
				if (hitShape.Shape is RectangleShape2D rectShape) rectShape.Size = hitboxSize;
				hitShape.Position = resolvedOffset;
			}
			_meleeHitbox?.Activate();

		}

		public void OnAttackActiveEnded() {
			// Reaching the end of the active window mid-swing means the recovery
			// frames are running — the cancellable part of the swing.
			if (_attackHitActive && CurrentState == CharacterState.Attacking) _attackInRecovery = true;
			_attackHitActive = false;
			_meleeHitbox?.Deactivate();
		}

		private void OnCombatAnimationFinished(StringName animationName) {
			if (CurrentState != CharacterState.Attacking || !_attackAnimationDriven) return;
			if (!animationName.ToString().StartsWith("basic_", StringComparison.Ordinal)) return;
			CompleteCurrentComboHit();
		}

		private void CompleteCurrentComboHit() {
			OnAttackActiveEnded();
			_attackAnimationDriven = false;
			int comboIndex = GetActiveComboIndex();

			// A directional attack exits straight out: no buffered continuation,
			// no chain-hold window, chain reset (§2.8).
			if (_attackVariant != FTT.Combat.BasicComboRules.VariantChain) {
				_attackVariant = FTT.Combat.BasicComboRules.VariantChain;
				ResetActiveCombo();
				_nextAttackBuffered = false;
				_inRecoveryHold = false;
				TransitionTo(IsOnFloor() ? CharacterState.Idle : CharacterState.Airborne);
				return;
			}

			if (comboIndex < 2 && _nextAttackBuffered) {
				AdvanceActiveCombo();
				StartComboHit();
			} else if (comboIndex >= 2) {
				ResetActiveCombo();
				_nextAttackBuffered = false;
				_inRecoveryHold = false;
				TransitionTo(IsOnFloor() ? CharacterState.Idle : CharacterState.Airborne);
			} else {
				_inRecoveryHold = true;
				_comboBufferFramesRemaining = ComboBufferFrames;
				_nextAttackBuffered = false;
			}
		}

		private void CancelActiveAttack() {
			_combatAnimationPlayer?.Stop();
			_attackAnimationDriven = false;
			_inRecoveryHold = false;
			_nextAttackBuffered = false;
			_attackVariant = FTT.Combat.BasicComboRules.VariantChain;
			OnAttackActiveEnded();
			_attackInRecovery = false;
		}

		private int GetActiveComboIndex() => Mathf.Clamp(
			_attackStartedAerial ? AerialComboCounter : GroundComboCounter,
			0,
			2);

		private FTT.Combat.CombatFrameTimeline GetActiveComboTimeline() =>
			_attackVariant == FTT.Combat.BasicComboRules.VariantUpAttack ? UpAttackTimeline
			: _attackVariant == FTT.Combat.BasicComboRules.VariantDownAir ? DownAirTimeline
			: (_attackStartedAerial ? _aerialComboTimelines : _groundComboTimelines)[GetActiveComboIndex()];

		private void AdvanceActiveCombo() {
			if (_attackStartedAerial) AerialComboCounter = Mathf.Min(2, AerialComboCounter + 1);
			else GroundComboCounter = Mathf.Min(2, GroundComboCounter + 1);
			ComboCounter = GetActiveComboIndex();
		}

		private void ResetActiveCombo() {
			if (_attackStartedAerial) AerialComboCounter = 0;
			else GroundComboCounter = 0;
			ComboCounter = 0;
		}

		private bool CheckSpecialInput() {
			if (TimeFrozen) return false;
			// V7.6 Suppression: both special casts are refused outright. The
			// cooldown timers are untouched (they keep ticking in
			// UpdateCooldowns) and no meter is spent — only a dull null-tone.
			if (_abilityCastSuppressed) {
				if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Special1)
					|| CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Special2)) {
					PlaySuppressedCastRefusal();
				}
				return false;
			}
			// Package 11 A5 gate: a Dormant slot is refused outright. Cooldowns
			// are irrelevant — the ability has never been cast.
			if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Special1)
				&& IsAbilityUnlocked(FTT.Core.AbilitySlot.Special1)
				&& SpecialOneCooldownTimer <= 0) {
				if (_special1 != null && _special1.TryExecute()) {
					_pendingSpecialSlot = 1;
					_specialStartedAerial = !IsOnFloor();
					PlayAnimation("special_1");
					TransitionTo(CharacterState.UsingSpecial);
					return true;
				}
			}
			// Package 11 A5 gate.
			if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Special2)
				&& IsAbilityUnlocked(FTT.Core.AbilitySlot.Special2)
				&& SpecialTwoCooldownTimer <= 0) {
				if (_special2 != null && _special2.TryExecute()) {
					_pendingSpecialSlot = 2;
					_specialStartedAerial = !IsOnFloor();
					PlayAnimation("special_2");
					TransitionTo(CharacterState.UsingSpecial);
					return true;
				}
			}
			return false;
		}

		// === F04 Nexus Resonance Source (Package 11 A12, Story-only) ================
		// Level 4A's required Ultimate set-piece authorizes the hero's real Ultimate
		// from the nexus itself rather than from the meter. Deliberately a single
		// narrow interception in the Story ultimate-input path: nothing in
		// scripts/FighterSim/ knows this field exists, and Fighter Mode, the hub
		// Holodeck and the Calibration Drills always run the ordinary meter gate.

		/// <summary>
		/// The armed <see cref="FTT.Environment.NexusResonanceSource"/>, or null.
		/// Owned by the source: it sets this on Interact and clears it on every exit
		/// path (leaving the cast area, death, reload, interruption, resolution, and
		/// its own <c>_ExitTree</c>), so a stale authorization cannot outlive the
		/// level scene or be carried to the boss.
		/// </summary>
		public FTT.Environment.NexusResonanceSource NexusUltimateSource { get; set; }

		/// <summary>True while a Nexus authorization is live for this character's Ultimate.</summary>
		public bool IsNexusUltimateAuthorized =>
			NexusUltimateSource != null
			&& IsInstanceValid(NexusUltimateSource)
			&& NexusUltimateSource.AuthorizesUltimate(_ultimate?.Data?.AbilityID ?? "");

		private bool CheckUltimateInput() {
			if (TimeFrozen) return false;
			// V7.6 Suppression: the ultimate CAST is locked; the meter is
			// neither spent nor reset, so a full meter is still full at thaw.
			if (_abilityCastSuppressed) {
				if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Ultimate)) {
					PlaySuppressedCastRefusal();
				}
				return false;
			}
			// Package 11 A5 gate: the METER is never locked (Defy History and
			// Level 4's dampening beams need it from Level 0) — only the cast is.
			if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Ultimate)
				&& IsAbilityUnlocked(FTT.Core.AbilitySlot.Ultimate)) {
				bool meterReady = _ultimateMeter != null ? _ultimateMeter.IsFull : CurrentUltimateMeter >= 100f;
				// F04: the authorization takes priority over ordinary meter spending
				// while armed, and works at any meter value including zero.
				bool nexusAuthorized = IsNexusUltimateAuthorized;
				if ((meterReady || nexusAuthorized) && _ultimate != null) {
					if (nexusAuthorized) return TryCastNexusUltimate();
					if (_ultimate.TryExecute()) {
						BeginUltimateCast();
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>
		/// The puzzle cast. Every roster ultimate gates itself on a full meter and
		/// consumes it in its own startup, so the authorization lends the meter for
		/// exactly the length of the execute call and then restores the value the
		/// player actually had. The net effect is the F04 contract: the cast neither
		/// fills nor consumes the meter, and a zero-meter cast leaves zero meter — so
		/// it can never light the F13 Defy seal.
		/// </summary>
		private bool TryCastNexusUltimate() {
			FTT.Environment.NexusResonanceSource source = NexusUltimateSource;
			float meterBefore = _ultimateMeter?.CurrentValue ?? CurrentUltimateMeter;
			_ultimateMeter?.SetValue(FTT.Combat.UltimateMeter.MaxValue);
			bool executed = _ultimate.TryExecute();
			_ultimateMeter?.SetValue(meterBefore);
			CurrentUltimateMeter = _ultimateMeter?.CurrentValue ?? meterBefore;
			if (!executed) {
				source?.NotifyCastInterrupted();
				return false;
			}
			BeginUltimateCast();
			source?.ResolvePuzzleTarget();
			return true;
		}

		/// <summary>Shared tail of both ultimate cast paths.</summary>
		private void BeginUltimateCast() {
			_ultimateStartedAerial = !IsOnFloor();
			PlayAnimation("ultimate");
			TransitionTo(CharacterState.UsingUltimate);
			FTT.Core.EventBus.Instance?.RaiseUltimateActivation(new FTT.Core.UltimateActivationPayload {
				PlayerIndex = PlayerIndex,
				AbilityID = _ultimate.Data?.AbilityID ?? "",
				Position = GlobalPosition
			});
		}

		private bool CheckBlockInput() {
			// V7.3: an empty shield or a running shatter lockout never raises
			// the stance (a null BlockSystem keeps the legacy behavior).
			if (CurrentInputFrame.IsHeld(FTT.Core.GameplayButtons.Block)
				&& (_blockSystem == null || _blockSystem.CanRaiseStance)) {
				TransitionTo(CharacterState.Blocking);
				return true;
			}
			return false;
		}

		private bool CheckMovementAbilityInput() {
			if (TimeFrozen) return false;
			if (IsMovementRooted) return false;
			// V7.6 Suppression: the movement ability is locked with the specials
			// (the roll and ordinary movement stay available).
			if (_abilityCastSuppressed) {
				if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.MovementAbility)) {
					PlaySuppressedCastRefusal();
				}
				return false;
			}
			// Package 11 A5 gate.
			if (!IsAbilityUnlocked(FTT.Core.AbilitySlot.MovementAbility)) return false;
			if (!CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.MovementAbility)) return false;
			// Second Glide (Story-only traversal node, Pocahontas): ONE extra
			// Breeze Glide entry per airtime, which is the only thing in the
			// kit that may start a movement ability while its cooldown runs.
			// The allowance is consumed atomically before TryExecute, and a
			// refused execute leaves it spent by design - the input was
			// accepted. The latch clears on grounding and on a stock loss.
			bool secondGlide = MovementAbilityCooldownTimer > 0
				&& _movementAbility is Abilities.PocahontasBreezeGlide glide
				&& glide.TryConsumeSecondGlide();
			if (MovementAbilityCooldownTimer > 0 && !secondGlide) return false;
			if (_movementAbility != null && _movementAbility.TryExecute()) {
				PlayAnimation("movement_ability");
				TransitionTo(CharacterState.UsingMovementAbility);
				return true;
			}
			return false;
		}

		/// <summary>
		/// Clears every per-airtime Resonance traversal latch. Called on
		/// grounding and on a stock loss / respawn so a single jump can never
		/// buy more than one extra entry.
		/// </summary>
		private void ResetAirtimeTraversalLatches() {
			(_movementAbility as Abilities.PocahontasBreezeGlide)?.ResetAirtimeAllowance();
		}

		private bool CheckInteractInput() {
			return false; // Interaction handled by interaction system
		}

		private void CheckDropThrough() {
			if (CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Down)) {
				if (_downTapFramesRemaining > 0) {
					TryDropThrough();
					_downTapFramesRemaining = 0;
				} else {
					_downTapFramesRemaining = FTT.Combat.StoryCombatRules.DownDoubleTapFrames;
				}
			}
		}

		private void TryDropThrough() {
			if (!FTT.Combat.StoryCombatRules.IsDropThroughAllowed(CurrentState)) return;

			// Find one-way platform below
			if (IsOnFloor()) {
				var collision = GetLastSlideCollision();
				if (collision != null) {
					var collider = collision.GetCollider() as PhysicsBody2D;
					if (collider == null || !collider.IsInGroup("OneWayPlatform")) return;

					_dropThroughPlatform = collider;
					AddCollisionExceptionWith(collider);
					_dropThroughFramesRemaining = FTT.Combat.StoryCombatRules.DropThroughFrames;
					_dropThroughTimer = DropThroughDuration;
					Velocity = new Vector2(Velocity.X, Mathf.Max(90f, Velocity.Y));
					if (CurrentState != CharacterState.Attacking) TransitionTo(CharacterState.Airborne);
				}
			}
		}

		private void UpdateDropThrough(float dt) {
			if (_dropThroughFramesRemaining > 0) {
				_dropThroughFramesRemaining--;
				_dropThroughTimer = Mathf.Max(0f, _dropThroughTimer - dt);
				if (_dropThroughFramesRemaining <= 0 && _dropThroughPlatform != null) {
					RemoveCollisionExceptionWith(_dropThroughPlatform);
					_dropThroughPlatform = null;
				}
			}
		}

		// === Hurtbox ===

		private void SetCrouchHurtbox() {
			if (_collisionShape?.Shape is RectangleShape2D rect) {
				float crouchedHeight = _normalHurtboxHeight * FTT.Combat.StoryCombatRules.CrouchHurtboxScale;
				rect.Size = new Vector2(rect.Size.X, crouchedHeight);
				_collisionShape.Position = new Vector2(
					_normalBodyShapePosition.X,
					_normalBodyShapePosition.Y + (_normalHurtboxHeight - crouchedHeight) * 0.5f);
			}
			if (_hurtboxShape?.Shape is RectangleShape2D hurtboxRect && _normalHurtboxSize.Y > 0f) {
				float crouchedHeight = _normalHurtboxSize.Y * FTT.Combat.StoryCombatRules.CrouchHurtboxScale;
				hurtboxRect.Size = new Vector2(_normalHurtboxSize.X, crouchedHeight);
				_hurtboxShape.Position = new Vector2(
					_normalHurtboxPosition.X,
					_normalHurtboxPosition.Y + (_normalHurtboxSize.Y - crouchedHeight) * 0.5f);
			}
		}

		private void RestoreHurtboxHeight() {
			if (_collisionShape?.Shape is RectangleShape2D rect) {
				rect.Size = new Vector2(rect.Size.X, _normalHurtboxHeight);
				_collisionShape.Position = _normalBodyShapePosition;
			}
			if (_hurtboxShape?.Shape is RectangleShape2D hurtboxRect && _normalHurtboxSize.Y > 0f) {
				hurtboxRect.Size = _normalHurtboxSize;
				_hurtboxShape.Position = _normalHurtboxPosition;
			}
		}

		// === Visual ===

		private void UpdateFacing(float hAxis) {
			if (Mathf.Abs(hAxis) > 0.1f) {
				IsFacingRight = hAxis > 0;
				UpdateSpriteFlip();
			}
		}

		private void UpdateSpriteFlip() {
			if (_animatedSprite != null) {
				_animatedSprite.FlipH = !IsFacingRight;
			}
		}

		/// <summary>
		/// The shared outline/glow arbiter for this body, or null before it is
		/// attached. Presentation only; nothing gameplay-side may read it.
		/// </summary>
		public FTT.Combat.GlowPresentationController Glow => _glow;

		private void UpdateHyperArmorPresentation() {
			bool isActive = HasActiveHyperArmor;
			if (_hyperArmorPresentationActive != isActive) {
				_hyperArmorPresentationActive = isActive;
				// The arbiter also subscribes to this event, but a direct push keeps
				// the shell correct when the bus autoload is absent (tests, tools).
				_glow?.SetHyperArmor(isActive);
				FTT.Core.EventBus.Instance?.RaiseHyperArmorChanged(new FTT.Core.HyperArmorPayload {
					PlayerIndex = PlayerIndex,
					IsActive = isActive
				});
			}

			bool spawnInvulnerable = IsPostRewindInvulnerable;
			if (_spawnInvulnerabilityPresentationActive == spawnInvulnerable) return;
			_spawnInvulnerabilityPresentationActive = spawnInvulnerable;
			_glow?.SetSpawnInvulnerability(spawnInvulnerable);
		}

		private void PlayAnimation(string animName) {
			if (_animatedSprite == null) return;
			_animatedSprite.SpeedScale = StatusAnimationMultiplier;
			if (_animatedSprite.Animation != animName
				&& _animatedSprite.SpriteFrames?.HasAnimation(animName) == true) {
				_animatedSprite.Play(animName);
			}
		}

		public void ResetStatusModifiers() {
			StatusMovementMultiplier = 1.0f;
			StatusJumpMultiplier = 1.0f;
			StatusAnimationMultiplier = 1.0f;
			StatusDamageTakenMultiplier = 1.0f;
			IsMovementRooted = false;
			if (_animatedSprite != null) _animatedSprite.SpeedScale = 1.0f;
		}

		// === Cooldowns ===

		private void UpdateCooldowns(float dt) {
			// Story-only CooldownReduction minors shorten every ability cooldown by
			// ticking the shared timers faster; abilities keep assigning the
			// authored CooldownDuration so the .tres numbers stay canonical.
			float cooldownDt = dt / Mathf.Max(0.05f, StoryCooldownMultiplier);
			if (SpecialOneCooldownTimer > 0) SpecialOneCooldownTimer -= cooldownDt;
			if (SpecialTwoCooldownTimer > 0) SpecialTwoCooldownTimer -= cooldownDt;
			if (MovementAbilityCooldownTimer > 0) MovementAbilityCooldownTimer -= cooldownDt;
		}

		// === Package 12 W2 region: F10 resource publish / restore (GAP-01) ====
		// Additive accessors. StoryManager publishes the live hero's continuous
		// combat resources into the attempt record at every checkpoint, durable
		// snapshot and critical event; StoryLevelControllerBase restores them
		// onto the freshly built hero on an ordinary load. Loading restores —
		// it never replenishes a spent resource (STORY_PERSISTENCE F10).

		/// <summary>
		/// Rally damage-taken meter earned but not yet credited: the echo portion
		/// of a hit accrues meter only as it drains (no double-earning), so the
		/// live pool is exactly the outstanding meter record.
		/// </summary>
		public float RallyUncreditedMeter =>
			MathF.Max(0f, _echoPool) * FTT.Combat.UltimateMeter.PointsPerDamageTaken;

		/// <summary>Writes this hero's continuous resources into <paramref name="timers"/>.</summary>
		public void CaptureStoryResourceTimers(FTT.Core.StoryPlayerResourceTimers timers) {
			if (timers == null) return;
			if (_blockSystem != null) {
				timers.BlockCharges = _blockSystem.CurrentCharges;
				timers.BlockRegenSeconds = _blockSystem.RegenProgressSeconds;
				timers.BlockLockoutSeconds = _blockSystem.LockoutRemainingSeconds;
			} else {
				timers.BlockCharges = CurrentBlockCharges;
				timers.BlockRegenSeconds = 0f;
				timers.BlockLockoutSeconds = 0f;
			}
			timers.AbilityCooldowns ??= new Dictionary<string, float>();
			timers.AbilityCooldowns[FTT.Core.LegacyUnlockSchedule.SpecialOneKey] = MathF.Max(0f, SpecialOneCooldownTimer);
			timers.AbilityCooldowns[FTT.Core.LegacyUnlockSchedule.SpecialTwoKey] = MathF.Max(0f, SpecialTwoCooldownTimer);
			timers.AbilityCooldowns[FTT.Core.LegacyUnlockSchedule.MovementKey] = MathF.Max(0f, MovementAbilityCooldownTimer);
			timers.EchoStepCooldownFrames = Math.Max(0, _echoStepCooldownFrames);
			timers.RallyUncreditedMeter = RallyUncreditedMeter;
			timers.WardenclyffeRechargeSeconds = MathF.Max(0f, WardenclyffeDamageDelaySeconds);
		}

		/// <summary>
		/// Restores persisted resources onto this (freshly constructed) hero.
		/// A record that was never captured (<c>BlockCharges &lt; 0</c>, the
		/// default of a fresh attempt) leaves the fresh-entry kit untouched.
		/// The Rally pool itself is NOT restored — T01a discards it on
		/// reconstruction, and its uncredited meter is settled once by the
		/// caller into the meter value it passes to
		/// <see cref="RestoreStoryCheckpoint"/>. The Time Freeze cooldown lives
		/// on <c>StoryManager</c> and is restored there.
		/// </summary>
		public void RestoreStoryResourceTimers(FTT.Core.StoryPlayerResourceTimers timers) {
			if (timers == null || timers.BlockCharges < 0) return;
			if (_blockSystem != null) {
				_blockSystem.RestorePersistedState(
					timers.BlockCharges, timers.BlockRegenSeconds, timers.BlockLockoutSeconds);
				CurrentBlockCharges = _blockSystem.CurrentCharges;
			} else {
				CurrentBlockCharges = Math.Clamp(timers.BlockCharges, 0, MaximumBlockCharges);
			}
			if (timers.AbilityCooldowns != null) {
				if (timers.AbilityCooldowns.TryGetValue(FTT.Core.LegacyUnlockSchedule.SpecialOneKey, out float special1)) {
					SpecialOneCooldownTimer = MathF.Max(0f, special1);
				}
				if (timers.AbilityCooldowns.TryGetValue(FTT.Core.LegacyUnlockSchedule.SpecialTwoKey, out float special2)) {
					SpecialTwoCooldownTimer = MathF.Max(0f, special2);
				}
				if (timers.AbilityCooldowns.TryGetValue(FTT.Core.LegacyUnlockSchedule.MovementKey, out float movement)) {
					MovementAbilityCooldownTimer = MathF.Max(0f, movement);
				}
			}
			_echoStepCooldownFrames = Math.Max(0, timers.EchoStepCooldownFrames);
			WardenclyffeDamageDelaySeconds = MathF.Max(0f, timers.WardenclyffeRechargeSeconds);
		}

		// === end Package 12 W2 region =======================================

		// === Ledge Detection ===

		private void OnLedgeAreaEntered(Area2D area) {
			if (area is FTT.Environment.LedgeGrabPoint ledge) TryGrabLedge(ledge);
		}

		/// <summary>
		/// Story ledge capture. Gameplay-feel plan §2.11 widened the vertical gate
		/// from "falling only" to "falling, or rising slowly", so jumping up to a
		/// ledge catches it the way the Fighter simulation now does. Godot screen
		/// space is +Y down, so -150 px/s is the rising side of the band.
		/// </summary>
		public const float LedgeGrabMaximumRiseSpeed = -150f;

		public bool TryGrabLedge(FTT.Environment.LedgeGrabPoint ledge) {
			if (ledge == null
				|| CurrentState != CharacterState.Airborne
				|| Velocity.Y < LedgeGrabMaximumRiseSpeed) return false;
			// V7.3: the regrab lockout (armed when a trump forced this player
			// off) and the per-airtime grab budget both refuse the capture,
			// mirroring the sim's CanGrab gates.
			if (_ledgeRegrabLockoutSeconds > 0f) return false;
			if (_ledgeGrabsThisAirtime >= FTT.Combat.BasicComboRules.LedgeRegrabsPerAirtime) return false;
			if (!ledge.IsInGroup("Ledge") || !ledge.TryAcquire(this)) return false;

			_ledgeGrabsThisAirtime++;
			_activeLedge = ledge;
			GlobalPosition = ledge.HangPosition;
			IsFacingRight = ledge.StageIsToRight;
			UpdateSpriteFlip();
			TransitionTo(CharacterState.LedgeHanging);
			return true;
		}

		/// <summary>
		/// V7.3 ledge trump, Story half: called by
		/// <see cref="FTT.Environment.LedgeGrabPoint.TryAcquire"/> when a second
		/// grabber contests the edge this player hangs. The hanger leaves through
		/// the normal drop path with the regrab lockout armed (the sim's
		/// 30-frame <c>FighterLedgeRules.RegrabLockoutFrames</c>), so it cannot
		/// instantly trump back.
		/// </summary>
		public void ForceLedgeTrumpRelease() {
			if (CurrentState != CharacterState.LedgeHanging) return;
			_ledgeRegrabLockoutSeconds =
				FTT.FighterSim.FighterLedgeRules.RegrabLockoutFrames / 60f;
			DropFromLedge();
		}

		// ====================================================================
		// === Package 11 A1 — status slots, Suppression, Conductive mark   ===
		// ====================================================================

		// --- V7.6 StatusSlots contract (IStatusEffectTarget) ---
		// The player's slot store is its child StatusController; this surface is
		// the contracted view over it, so the three controllers and the sim share
		// one routing table (FTT.Combat.StatusRouting) and one replacement rule.

		public FTT.Combat.StatusSlots ActiveStatuses {
			get => _statusController?.ActiveStatuses ?? FTT.Combat.StatusSlots.Empty;
			set { if (_statusController != null) _statusController.ActiveStatuses = value; }
		}

		public void ApplyStatusEffect(FTT.Core.StatusType status, float duration, float intensity) =>
			_statusController?.ApplyStatus(status, duration, intensity);

		public void ClearStatusEffect(FTT.Combat.StatusSlot slot) =>
			_statusController?.ClearStatusSlot(slot);

		/// <summary>Both slots. Death, respawn and Restart Level call this.</summary>
		public void ClearAllStatusEffects() => _statusController?.ClearAllStatusEffects();

		public Node2D TargetNode => this;

		public bool HasStatusEffect(FTT.Core.StatusType type) =>
			_statusController?.HasStatus(type) == true;

		// --- V7.6 Suppression: the ability-cast lock ---

		private bool _abilityCastSuppressed;

		/// <summary>
		/// True while <see cref="FTT.Core.StatusType.Suppression"/> occupies the
		/// control slot. Consulted by the four cast sites (Special 1 / Special 2
		/// in <c>CheckSpecialInput</c>, <c>CheckUltimateInput</c>,
		/// <c>CheckMovementAbilityInput</c>), which refuse without consuming a
		/// cooldown or meter. Everything else — basics, block, grab, Rally, DI,
		/// landing tech, Defy History, death rewinds and Time Freeze — is
		/// deliberately unaffected, and cooldowns keep ticking.
		/// </summary>
		public bool IsAbilityCastSuppressed => _abilityCastSuppressed;

		/// <summary>
		/// Driven by <see cref="FTT.Combat.SuppressionStrategy"/> on apply and
		/// remove. Publishes the four slots' Suppressed/Clear transitions on the
		/// bus (Package 11 §2.9) so the HUD draws the cold cross-out without this
		/// class knowing anything about the HUD.
		/// </summary>
		public void SetAbilityCastSuppressed(bool suppressed) {
			if (_abilityCastSuppressed == suppressed) return;
			_abilityCastSuppressed = suppressed;
			_glow?.SetAuraSmothered(suppressed);
			FTT.Core.AbilitySlotLockState state = suppressed
				? FTT.Core.AbilitySlotLockState.Suppressed
				: FTT.Core.AbilitySlotLockState.Clear;
			foreach (FTT.Core.AbilitySlot slot in SuppressibleSlots) {
				FTT.Core.EventBus.Instance?.RaiseAbilitySlotLockChanged(
					new FTT.Core.AbilitySlotLockPayload { Slot = slot, State = state });
			}
		}

		private static readonly FTT.Core.AbilitySlot[] SuppressibleSlots = {
			FTT.Core.AbilitySlot.Special1,
			FTT.Core.AbilitySlot.Special2,
			FTT.Core.AbilitySlot.MovementAbility,
			FTT.Core.AbilitySlot.Ultimate
		};

		/// <summary>
		/// The refusal feedback hook: a dull low null-tone rather than the
		/// ability's cast cue. Presentation only — it never consumes cooldown or
		/// meter. A8 may repoint this at an authored cue.
		/// </summary>
		private static void PlaySuppressedCastRefusal() =>
			FTT.Core.AudioManager.Instance?.PlayUISound(null, SuppressedRefusalPitch);

		private const float SuppressedRefusalPitch = 0.45f;

		// --- F07 Conductive mark (a caster-owned combo mark, NOT a status) ---
		// Tesla's finisher applies Static Charge (a 24-frame interrupt) AND a
		// separate Conductive mark. The mark occupies no status slot, causes no
		// action lock, contributes zero stagger budget, and may remain while the
		// target acts in armor. Lorentz Pulse chains gate on the mark, never on
		// Static Charge.

		private int _conductiveFramesRemaining;
		private int _conductiveSourcePlayerID = -1;

		/// <summary>Frames left on the Conductive mark; 0 when unmarked.</summary>
		public int ConductiveFramesRemaining => _conductiveFramesRemaining;

		/// <summary>Player index that applied the live mark; -1 when unmarked.</summary>
		public int ConductiveSourcePlayerID =>
			_conductiveFramesRemaining > 0 ? _conductiveSourcePlayerID : -1;

		public bool HasConductiveMark => _conductiveFramesRemaining > 0;

		public bool HasConductiveMarkFrom(int sourcePlayerID) =>
			_conductiveFramesRemaining > 0 && _conductiveSourcePlayerID == sourcePlayerID;

		/// <summary>
		/// Applies (or extends) a Conductive mark. One mark per target: a new
		/// source replaces the old one, and the same source takes the longer
		/// remaining time — never additive.
		/// </summary>
		public void ApplyConductiveMark(int sourcePlayerID, int frames) {
			if (frames <= 0 || CurrentState == CharacterState.Dead) return;
			if (_conductiveSourcePlayerID != sourcePlayerID || frames > _conductiveFramesRemaining) {
				_conductiveSourcePlayerID = sourcePlayerID;
				_conductiveFramesRemaining = frames;
			}
		}

		public void ClearConductiveMark() {
			_conductiveFramesRemaining = 0;
			_conductiveSourcePlayerID = -1;
		}

		/// <summary>Hitstop freezes the gameplay clock, so the mark holds with it.</summary>
		private void TickConductiveMark() {
			if (_conductiveFramesRemaining <= 0) return;
			_conductiveFramesRemaining--;
			if (_conductiveFramesRemaining <= 0) _conductiveSourcePlayerID = -1;
		}

		/// <summary>
		/// T01a: Tesla's death clears <b>his</b> Conductive marks even though his
		/// coils survive. Sweeps every live Story combatant and drops marks whose
		/// source is <paramref name="sourcePlayerID"/>.
		/// </summary>
		public static void ClearConductiveMarksFrom(SceneTree tree, int sourcePlayerID) {
			if (tree == null) return;
			Godot.Collections.Array<Node> players = tree.GetNodesInGroup("Players");
			using (var playersLifetime = players.AsDisposable()) {
				foreach (Node node in players) {
					if (node is PlayerController pc && pc.HasConductiveMarkFrom(sourcePlayerID)) {
						pc.ClearConductiveMark();
					}
				}
			}
			Godot.Collections.Array<Node> enemies = tree.GetNodesInGroup("Enemies");
			using (var enemiesLifetime = enemies.AsDisposable()) {
				foreach (Node node in enemies) {
					if (node is FTT.Enemies.EnemyController enemy
						&& enemy.HasConductiveMarkFrom(sourcePlayerID)) {
						enemy.ClearConductiveMark();
					} else if (node is FTT.Enemies.BossController boss
						&& boss.HasConductiveMarkFrom(sourcePlayerID)) {
						boss.ClearConductiveMark();
					}
				}
			}
		}

		/// <summary>
		/// The <c>tesla_conductive_hold</c> Resonance node (Story-only) extends
		/// the finisher's mark from 1.5 s to 2.5 s. A4 authors the node; the
		/// scaling is read here, at the application site — the cross-mode
		/// <see cref="FTT.Combat.BasicComboRules"/> constant is never edited for a
		/// Story perk. Linked coil-fence marks are deliberately NOT extended.
		/// </summary>
		public const string ConductiveHoldPerkKey = "tesla_conductive_hold";

		/// <summary>The Conductive frames this character's finisher applies; 0 when it applies none.</summary>
		public int FinisherConductiveMarkFrames() {
			int frames = _stringProfile.FinisherMarkFrames;
			if (frames <= 0) return 0;
			return HasStoryPerk(ConductiveHoldPerkKey)
				? FTT.Combat.BasicComboRules.ConductiveMarkUpgradedFrames
				: frames;
		}

		// === Package 11 A7a (V7.6 F14): the non-damaging block entry point ====

		/// <summary>
		/// Whether the roll's opening invulnerability window is live. Exposed
		/// because the Siphon Snare's break list names roll i-frames explicitly:
		/// rolling through a tether severs it before the next drain tick.
		/// </summary>
		public bool IsRollInvulnerable => _rollInvulnerable;

		/// <summary>
		/// Absorbs a cast that is <b>not a hit</b> — no HP damage, no hitstun, no
		/// knockback, no hitstop — with the ordinary Basic block response. The
		/// Eraser's Siphon Snare is the only caller today.
		///
		/// <para><see cref="FTT.Combat.BlockSystem.ResolveHit"/> assumes a damaging
		/// hit, so this builds a zero-damage Basic-class payload and routes it
		/// through the same rules rather than reimplementing them: the grounded
		/// stance (only <see cref="CharacterState.Blocking"/> reaches here, and the
		/// stance cannot rise airborne, at zero charges, or under a shatter
		/// lockout), the front-facing test, one charge spent, shieldstun on a
		/// non-shatter absorb, and the normal shatter when it was the last charge.
		/// Distinct-block perks — Henry's Bastion inside the block system, Shield
		/// of Orleans here — fire exactly once through the existing execution
		/// dedup. Because the payload's damage is zero, nothing downstream can
		/// chip HP.</para>
		///
		/// <para>Returns <see cref="FTT.Combat.BlockResult.NotBlocked"/> when the
		/// stance was not up, faced the wrong way, or had nothing left to spend —
		/// the caller then proceeds as if no block existed.</para>
		/// </summary>
		public FTT.Combat.BlockResult TryAbsorbNonDamagingCast(
			Vector2 castOrigin, string attackID, string hitboxID) {
			if (CurrentState != CharacterState.Blocking || _blockSystem == null) {
				return FTT.Combat.BlockResult.NotBlocked;
			}
			var cast = new FTT.Combat.HitPayload {
				AttackerIndex = -1,
				TargetIndex = PlayerIndex,
				AttackID = attackID ?? "",
				HitboxID = hitboxID ?? "",
				AttackClass = FTT.Combat.AttackClass.Basic,
				Damage = 0f,
				Knockback = Vector2.Zero,
				HitstunDuration = 0f,
				HitOrigin = castOrigin,
				AttackerFacingRight = castOrigin.X <= GlobalPosition.X,
				AppliedStatus = FTT.Core.StatusType.None,
				BlockChargeCost = 1,
				// Never a hit, so never a freeze — the construct/DoT exemption
				// covers exactly this shape of contact.
				ExemptFromHitstop = true
			};
			FTT.Combat.BlockResult result = _blockSystem.ResolveHit(cast);
			CurrentBlockCharges = _blockSystem.CurrentCharges;
			if (result == FTT.Combat.BlockResult.NotBlocked) return result;
			if (HasStoryPerk(ShieldOfOrleansPerkKey)) ApplyShieldOfOrleans(cast, result);
			if (result == FTT.Combat.BlockResult.Blocked) {
				FTT.Core.HapticFeedbackManager.Instance?.OnGuardImpact(PlayerIndex);
			}
			return result;
		}
	}
}
