using Godot;
using FTT.Characters;
using FTT.Core;

namespace FTT.Environment {

    public enum ChronalExtractorVisualState { Idle, Damaged, Destroyed }

    public partial class ChronalExtractor : DamageableEnvironmentObject, IStoryRewindSimulation {
        [Export(PropertyHint.Range, "0,100,1")] public int HazardDamage = 20;
        [Export] public Vector2 HazardKnockback = new(420f, -260f);
        [Export(PropertyHint.Range, "0,100,1")] public float UltimateDrain = 20f;
        /// <summary>
        /// The design targets 25 per break, but the shipped per-level ledger
        /// (docs/DUST_ECONOMY.md) is balanced around 15 and the full dust
        /// economy rebalance is explicitly deferred (V7.2 ruling) — the value
        /// stays 15 until that pass retunes the ledger.
        /// </summary>
        [Export(PropertyHint.Range, "1,100,1")] public int DustReward = 15;
        /// <summary>V7 idle cycle: the visible charge-up before the burst.</summary>
        [Export] public float TelegraphSeconds = 2.5f;
        /// <summary>V7 idle cycle: the safe window in which attacks are free.</summary>
        [Export] public float SafeWindowSeconds = 4.0f;

        /// <summary>The scene-tree group every live Extractor joins.</summary>
        public const string GroupName = "chronal_extractor";

        /// <summary>
        /// Package 11 A4 (Resonance V7.6). The sanctioned indirect channel
        /// between a grid and the Timeline Integrity timer: Leonardo's
        /// Clockwork Overdrive bolts double against an Extractor, Lincoln's
        /// Kinetic Splitting adds 50%, and the generic <c>ExtractorDamage</c>
        /// lane rides on top. Story-only - a null attacker (enemy, hazard,
        /// Fighter body) resolves neutral.
        /// </summary>
        protected override float ResolveIncomingDamageMultiplier(in FTT.Combat.HitPayload payload) =>
            StoryExtractorDamage.ExtractorMultiplier(
                FindAttackingPlayer(payload.AttackerIndex), payload.AttackID ?? "");

        private Area2D _hazardArea;
        private bool _telegraphing;
        private float _cycleTimer;
        public ChronalExtractorVisualState VisualState { get; private set; }
        public int DischargeCount { get; private set; }

        /// <summary>True during the charge-up (red indicator) phase. Test seam.</summary>
        public bool IsTelegraphing => _telegraphing;

        // V7.6 F01 (Package 11 A3): the per-machine siphon is RETIRED. There is
        // no engagement distance, no grace window, no share cap and no
        // per-machine drain — Timeline Integrity is a normalized level clock
        // that runs globally from level load whether this machine has ever been
        // seen or not. A living Extractor's only contribution is the +0.2 it
        // adds to the drain FACTOR, which StoryManager reads from the
        // living-machine count; breaking one slows the future rate and never
        // moves the gauge by a point.

        private bool _rewindFrozen;

        /// <summary>The world freeze during a death rewind (and the collapse
        /// beat) still pauses the discharge cycle.</summary>
        public void SetStoryRewindFrozen(bool frozen) => _rewindFrozen = frozen;

        public override void _Ready() {
            MaxHP = 100;
            HitsToBreak = 0;
            base._Ready();
            AddToGroup(GroupName);
            _hazardArea = GetNodeOrNull<Area2D>("HazardArea");
            _telegraphing = false;
            _cycleTimer = SafeWindowSeconds;
            Publish();
        }

        /// <summary>
        /// V7 redesign — the Extractor discharges on an IDLE CYCLE, not per
        /// hit: a ~2.5 s visible charge-up, one burst, then a ~4 s safe window
        /// in which attacks are free. Skilled play destroys one while eating
        /// zero or one discharge; attacking no longer triggers anything.
        /// </summary>
        public override void _PhysicsProcess(double delta) {
            if (IsDestroyed || _rewindFrozen) return;
            _cycleTimer -= (float)delta;
            if (_cycleTimer > 0f) return;
            if (_telegraphing) {
                _telegraphing = false;
                _cycleTimer = SafeWindowSeconds;
                Discharge();
            } else {
                _telegraphing = true;
                _cycleTimer = TelegraphSeconds;
                // The warning phase drives the red indicator and the klaxon.
                EventBus.Instance?.RaiseHazardStateChanged(new HazardStatePayload {
                    HazardID = ObjectID,
                    Phase = HazardPhase.Warning,
                    Duration = TelegraphSeconds
                });
                ApplyStatePresentation();
            }
        }

        public void Discharge() {
            if (IsDestroyed) return;
            DischargeCount++;
            EventBus.Instance?.RaiseHazardStateChanged(new HazardStatePayload {
                HazardID = ObjectID,
                Phase = HazardPhase.Active,
                Duration = 0.25f
            });
            EmitDischargeVfx();
            ApplyStatePresentation();
            if (_hazardArea == null) return;
            Godot.Collections.Array<Node2D> bodies = _hazardArea.GetOverlappingBodies();
            using var bodiesLifetime = bodies.AsDisposable();
            foreach (Node2D body in bodies) {
                if (body is PlayerController player) ApplyDischargeToPlayer(player);
            }
        }

        public void ApplyDischargeToPlayer(PlayerController player) {
            if (player == null || IsDestroyed) return;
            // V7.3: environmental damage routes through the chokepoint so the
            // victim-side pipeline (Defy flag consumption, Rally echo, meter)
            // runs — never raw ApplyDamage.
            player.ApplyEnvironmentalDamage(HazardDamage);
            float direction = player.GlobalPosition.X >= GlobalPosition.X ? 1f : -1f;
            player.Velocity += new Vector2(HazardKnockback.X * direction, HazardKnockback.Y);
            player.DrainUltimateMeter(UltimateDrain);
        }

        protected override void OnDamaged(int appliedDamage) {
            // V7: damage no longer triggers a discharge — the idle cycle owns
            // the hazard; hits only advance the visual damage state.
            VisualState = CurrentHP <= MaxHP / 2
                ? ChronalExtractorVisualState.Damaged
                : ChronalExtractorVisualState.Idle;
            Publish();
        }

        protected override void OnDestroyed() {
            VisualState = ChronalExtractorVisualState.Destroyed;
            // V7.3 Single Icon Rule: the reward is a physical pickup — the
            // wallet is paid (and the results line attributed) at collection,
            // never here. The pickup never expires and reads as a Large icon.
            StoryDropSystem.SpawnDustAward(
                DustReward, GlobalPosition, GetParent(), DustAwardSource.Extractor);
            // V7.6 F01: destroying an Extractor buys FUTURE time, never a
            // refill — the living count drops, which lowers the drain factor
            // for the rest of the level while the gauge itself does not move
            // by a single point. The per-attempt registry entry is what lets a
            // mid-level resume rebuild the machine broken.
            StoryManager.Instance?.NotifyExtractorDestroyed();
            StoryManager.Instance?.RecordExtractorDestroyed(ObjectID);
            // E01 drain feedback: a brief non-blocking notice on a REAL first
            // destruction while the clock runs. No "time gained", no refill,
            // no numeric readout — the notice says the drain slowed, nothing
            // more. Untimed levels and a locked PreBoss clock say nothing.
            if (StoryManager.Instance?.IsIntegrityClockRunning == true) {
                EnvironmentNotice.Post("integrity_drain_slowed", this);
            }
            // Package 8 B5. The discharge already sounds through the hazard event the
            // director subscribes to; the break itself is a separate, lower beat.
            EnvironmentAudioCues.PlayDestruction();
            Publish();
        }

        /// <summary>
        /// V7.3 mid-level resume: rebuilds this machine in its destroyed state
        /// WITHOUT paying dust, restoring Integrity, or re-registering — the
        /// original destruction already did all three in the attempt being
        /// resumed.
        /// </summary>
        public void RestoreDestroyedState() {
            RestoreState(0);
            Publish();
        }

        /// <summary>
        /// Package 8 B6: a shockwave at the discharge ring, so the 190-unit hazard
        /// radius that already damages the player is visible before it lands. Pooled
        /// and null-safe — a scene with no VFX pool simply gets no effect.
        /// </summary>
        private void EmitDischargeVfx() {
            if (!IsInsideTree()) return;
            PackedScene scene = FTT.Combat.VfxLibrary.Load(FTT.Combat.VfxEffectFamily.Shockwave);
            if (scene == null) {
                FTT.Combat.VfxEmitter.EmitEnvironment(GlobalPosition, GetParent(), DischargeColor);
                return;
            }
            FTT.Combat.VfxEmitter.EmitScene(scene, GlobalPosition, GetParent(), DischargeColor);
        }

        /// <summary>
        /// The Unbound's cold synthetic light (design §2's two-colour grammar,
        /// codified by Package 11 A6b). Every Chronal Extractor is Level 0 in
        /// miniature — the same cold beam drawing the same warm resonance out of
        /// an era — so it must not carry its own pigment. The literal that used
        /// to sit here was already exactly this colour; what was missing was the
        /// shared contract, and the comment called it "Apex Archive cyan" long
        /// after the V7.5 faction rename.
        /// </summary>
        public static readonly Color DischargeColor = FTT.UI.UIPalette.UnboundColdDischarge;

        protected override void ApplyStatePresentation() {
            base.ApplyStatePresentation();
            VisualState = IsDestroyed
                ? ChronalExtractorVisualState.Destroyed
                : CurrentHP <= MaxHP / 2 ? ChronalExtractorVisualState.Damaged : ChronalExtractorVisualState.Idle;
            if (GetNodeOrNull<CanvasItem>("DamagedVisual") is CanvasItem damaged) {
                damaged.Visible = VisualState == ChronalExtractorVisualState.Damaged;
            }
            // Package 8 B6 dressing: the destroyed husk, the sparks that only run
            // while the machine is wounded, and the core glow that dies with it.
            if (GetNodeOrNull<CanvasItem>("DestroyedVisual") is CanvasItem destroyed) {
                destroyed.Visible = VisualState == ChronalExtractorVisualState.Destroyed;
            }
            if (GetNodeOrNull<CpuParticles2D>("DamageSparks") is CpuParticles2D sparks) {
                sparks.Emitting = VisualState == ChronalExtractorVisualState.Damaged;
            }
            if (GetNodeOrNull<CanvasItem>("CoreGlow") is CanvasItem core) {
                core.Visible = !IsDestroyed;
                // The idle-cycle charge-up reads red — the promised warning —
                // ahead of the damaged-state amber and the idle white.
                core.Modulate = _telegraphing
                    ? new Color(1f, 0.2f, 0.15f, 1f)
                    : VisualState == ChronalExtractorVisualState.Damaged
                        ? new Color(1f, 0.45f, 0.2f, 1f)
                        : Colors.White;
            }
        }

        private void Publish() => EventBus.Instance?.RaiseExtractorStateChanged(new ExtractorStatePayload {
            ExtractorID = ObjectID,
            CurrentHP = CurrentHP,
            MaxHP = MaxHP,
            IsDestroyed = IsDestroyed
        });
    }
}
