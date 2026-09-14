using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Environment;
using Godot;

namespace FTT.Enemies {

    /// <summary>
    /// The outcome of a Siphon Snare's ONE attachment check. Every value except
    /// <see cref="Attached"/> means the cast produced no tether — and, per the
    /// contract, still committed its full 10 s cooldown with no early refund.
    /// </summary>
    public enum SiphonAttachResult {
        /// <summary>No Siphon cast has resolved yet.</summary>
        None = 0,
        /// <summary>A tether attached and is draining.</summary>
        Attached,
        /// <summary>
        /// Nobody eligible was inside the 3-unit radius with line of sight: out of
        /// range, behind authored solid cover, dead, at zero meter, Suppressed,
        /// invulnerable, or already tethered. A miss leaves no lingering pulse.
        /// </summary>
        NoTarget,
        /// <summary>
        /// A legal grounded, front-facing block absorbed the cast for one charge.
        /// No drain, no HP chip, no lingering pulse.
        /// </summary>
        Blocked,
        /// <summary>Another Eraser's tether already owned the target and kept it.</summary>
        AlreadyTethered
    }

    /// <summary>Why a <see cref="SiphonTetherChannel"/> stopped draining.</summary>
    public enum SiphonBreakReason {
        /// <summary>Still live.</summary>
        None = 0,
        /// <summary>The authored 3 s channel cap ran out.</summary>
        DurationElapsed,
        /// <summary>The 30-point per-cast cap was reached.</summary>
        CapReached,
        /// <summary>The target's Influence meter hit zero.</summary>
        MeterEmpty,
        /// <summary>The target left the 3-unit radius.</summary>
        OutOfRange,
        /// <summary>Authored solid cover came between caster and target.</summary>
        LineOfSightBlocked,
        /// <summary>The target became invulnerable — roll i-frames included.</summary>
        TargetInvulnerable,
        /// <summary>Either actor died or left the tree.</summary>
        ActorGone,
        /// <summary>A hit the caster's armor did NOT reject interrupted it.</summary>
        CasterInterrupted,
        /// <summary>Suppression was newly applied to the target.</summary>
        TargetSuppressed,
        /// <summary>A same-frame contested attach with a lower stable actor ID won.</summary>
        Contested,
        /// <summary>Level/room departure, checkpoint reconstruction, or pooling.</summary>
        Released
    }

    /// <summary>
    /// V7.6 F14 Option A (Package 11 A7a) — the Eraser's Siphon Snare channel.
    ///
    /// <para><b>This is not a hitbox.</b> It is the third beat of
    /// <c>cast → attachment check → channel</c>: the executor runs the single
    /// attachment check at telegraph end and, only on success, builds one of
    /// these. While it lives it drains
    /// <c>min(currentMeter, MeterDrainPerSecond × liveDeltaSeconds)</c> Influence
    /// per live tick, preserving fractions, capped at
    /// <see cref="EnemyAbilityData.MeterDrainCap"/> for the whole cast. There is
    /// <b>no</b> initial lump, no HP damage, no hitstun, knockback or movement
    /// lock, no status-slot entry, no Rally echo, no damage-based meter credit and
    /// no hitstop. Nothing is transferred to the Eraser. The player may act
    /// freely; the Eraser may not.</para>
    ///
    /// <para>Membership in <c>"persistent_construct"</c> puts it inside both
    /// <c>ChronalRewindManager.FrozenSimulationGroups</c> and
    /// <c>TimeFreezeController.FrozenTimeGroups</c>, so the death rewind and the
    /// V7.6 Time Freeze both pause the channel, the drain and the clock. Resuming
    /// re-evaluates every break condition <b>before</b> the next drain tick and
    /// never runs a catch-up tick — the freeze is a pause, never a debt.</para>
    ///
    /// <para>Like <see cref="DilationFieldZone"/> the Area2D body exists so the
    /// tether is physically present, but the per-tick evaluation is pure distance
    /// and raycast maths against a directly held target reference, which keeps
    /// the behaviour deterministic under headless direct calls where no physics
    /// frame flushes overlaps.</para>
    /// </summary>
    public partial class SiphonTetherChannel : Area2D, IStoryRewindSimulation, IStoryTimeFreezable {

        /// <summary>Group every live tether joins, alongside the freeze sweeps.</summary>
        public const string ConstructGroup = "persistent_construct";

        /// <summary>
        /// Test/debug seam for "unobstructed line of sight through authored solid
        /// cover". Returns true when the segment is CLEAR. The default consults
        /// the 2D physics space against Environment | PersistentObject bodies and
        /// answers "clear" whenever no space exists (headless), so a test that
        /// does not care about cover never has to build a world.
        /// </summary>
        public static Func<Node2D, Vector2, Vector2, bool> LineOfSightProbe = DefaultLineOfSight;

        /// <summary>
        /// Every live tether, newest last. The one-tether-per-player rule and the
        /// stable-ID tiebreak are resolved here rather than on the player, so a
        /// pooled player instance carries no tether state between levels.
        /// </summary>
        private static readonly List<SiphonTetherChannel> Live = new();

        /// <summary>Read-only view for tests and for the executor's refusal checks.</summary>
        public static IReadOnlyList<SiphonTetherChannel> LiveChannels => Live;

        // === Authored configuration ===

        /// <summary>Attachment and break radius in pixels (3 units × 60 px).</summary>
        public float RadiusPixels { get; private set; } = 180f;

        /// <summary>Influence points drained per live second.</summary>
        public float DrainPerSecond { get; private set; } = 10f;

        /// <summary>Hard ceiling on Influence taken by this one cast.</summary>
        public float DrainCap { get; private set; } = 30f;

        /// <summary>Live seconds of tether this cast may run.</summary>
        public float MaxChannelSeconds { get; private set; } = 3f;

        // === Live state ===

        /// <summary>The Eraser maintaining this tether.</summary>
        public Node2D Caster { get; private set; }

        /// <summary>The tethered player.</summary>
        public PlayerController Target { get; private set; }

        /// <summary>
        /// Deterministic tiebreak identity for a same-frame contested attachment.
        /// Ordinal-least wins. Defaults to the caster node's name, which is stable
        /// within an authored level and settable by tests.
        /// </summary>
        public string StableActorID { get; set; } = "";

        /// <summary>Live seconds elapsed. Frozen time does not count.</summary>
        public float ChannelSeconds { get; private set; }

        /// <summary>Influence actually taken so far by this cast.</summary>
        public float DrainedTotal { get; private set; }

        /// <summary>True while draining.</summary>
        public bool IsLive { get; private set; }

        /// <summary>Why it stopped, or <see cref="SiphonBreakReason.None"/>.</summary>
        public SiphonBreakReason BreakReason { get; private set; } = SiphonBreakReason.None;

        /// <summary>The physics frame the tether attached on, for the contest rule.</summary>
        public ulong AttachFrame { get; private set; }

        private bool _frozen;
        private bool _interrupted;
        private bool _targetWasSuppressed;

        /// <summary>Raised once when the tether stops, with its reason.</summary>
        public event Action<SiphonTetherChannel, SiphonBreakReason> Broken;

        public bool IsStoryRewindFrozen => _frozen;

        public void SetStoryRewindFrozen(bool frozen) => _frozen = frozen;

        /// <summary>
        /// V7.6 Time Freeze shares the rewind latch: this class mutates nothing on
        /// the way in, so the channel clock, the drain and the cast all resume
        /// exactly where they stopped, with no catch-up tick.
        /// </summary>
        public void SetTimeFrozen(bool frozen) => _frozen = frozen;

        /// <summary>
        /// Test seam for the same-frame contest rule: forces the frame this tether
        /// records as its attachment. Two casts only contest when they share one,
        /// and a test cannot otherwise arrange a genuine tie without relying on
        /// where the physics clock happens to sit.
        /// </summary>
        internal ulong? AttachFrameOverride;

        /// <summary>Whether anything is currently tethering this player.</summary>
        public static SiphonTetherChannel ActiveFor(PlayerController player) {
            if (player == null) return null;
            for (int index = Live.Count - 1; index >= 0; index--) {
                SiphonTetherChannel channel = Live[index];
                if (!IsInstanceValid(channel)) { Live.RemoveAt(index); continue; }
                if (channel.IsLive && channel.Target == player) return channel;
            }
            return null;
        }

        /// <summary>
        /// Attachment. The ONLY way a channel goes live. The caller (the executor)
        /// has already proven range, line of sight and the refusal set; this
        /// resolves the one-tether rule and the same-frame contest, then arms.
        /// Returns false when the tether was refused — the cast still committed
        /// and its cooldown still runs, as the contract requires.
        /// </summary>
        public bool Attach(EnemyAbilityData ability, Node2D caster, PlayerController target) {
            if (ability == null || caster == null || target == null) return false;

            RadiusPixels = Mathf.Max(8f, ability.PulseRadius);
            DrainPerSecond = Mathf.Max(0f, ability.MeterDrainPerSecond);
            DrainCap = Mathf.Max(0f, ability.MeterDrainCap);
            MaxChannelSeconds = Mathf.Max(0.01f, ability.FieldDurationSeconds);
            Caster = caster;
            Target = target;
            if (string.IsNullOrEmpty(StableActorID)) StableActorID = caster.Name;
            AttachFrame = AttachFrameOverride ?? Engine.GetPhysicsFrames();

            SiphonTetherChannel incumbent = ActiveFor(target);
            if (incumbent != null) {
                // One active tether per player. A LATER cast simply fails; only a
                // genuinely simultaneous pair is resolved by stable actor ID, and
                // the ordinal-least ID keeps the tether.
                bool sameFrame = incumbent.AttachFrame == AttachFrame;
                bool outranksIncumbent = sameFrame
                    && string.CompareOrdinal(StableActorID, incumbent.StableActorID) < 0;
                if (!outranksIncumbent) return false;
                incumbent.Break(SiphonBreakReason.Contested);
            }

            IsLive = true;
            BreakReason = SiphonBreakReason.None;
            ChannelSeconds = 0f;
            DrainedTotal = 0f;
            _interrupted = false;
            _targetWasSuppressed = target.HasStatusEffect(StatusType.Suppression);
            Live.Add(this);
            return true;
        }

        public override void _Ready() {
            AddToGroup(ConstructGroup);
            Monitoring = false;
            Monitorable = false;
            CollisionLayer = 0;
            CollisionMask = CollisionLayers.Player;
            if (GetNodeOrNull<CollisionShape2D>("CollisionShape2D") == null) {
                AddChild(new CollisionShape2D {
                    Name = "CollisionShape2D",
                    Shape = new CircleShape2D { Radius = RadiusPixels }
                });
            }
        }

        public override void _PhysicsProcess(double delta) => TickChannel((float)delta);

        /// <summary>
        /// One live step. Break conditions are evaluated BEFORE the drain, so a
        /// tether that has already lost its target never takes one last sip — and
        /// a thawed freeze re-evaluates before it drains again.
        /// </summary>
        public void TickChannel(float delta) {
            if (!IsLive) return;
            if (_frozen) return;
            if (delta <= 0f) return;

            SiphonBreakReason reason = EvaluateBreak();
            if (reason != SiphonBreakReason.None) { Break(reason); return; }

            ChannelSeconds += delta;
            float wanted = DrainPerSecond * delta;
            float remainingCap = Mathf.Max(0f, DrainCap - DrainedTotal);
            float available = Mathf.Max(0f, Target.CurrentUltimateMeter);
            float taken = Mathf.Min(wanted, Mathf.Min(remainingCap, available));
            if (taken > 0f) {
                Target.DrainUltimateMeter(taken);
                DrainedTotal += taken;
            }

            if (DrainedTotal >= DrainCap - 0.0001f) { Break(SiphonBreakReason.CapReached); return; }
            if (Target.CurrentUltimateMeter <= 0.0001f) { Break(SiphonBreakReason.MeterEmpty); return; }
            if (ChannelSeconds >= MaxChannelSeconds) Break(SiphonBreakReason.DurationElapsed);
        }

        /// <summary>
        /// The six break conditions, in the contract's order. A broken tether never
        /// reattaches during that cast — <see cref="Break"/> is terminal.
        /// </summary>
        private SiphonBreakReason EvaluateBreak() {
            if (Caster == null || !IsInstanceValid(Caster) || !Caster.IsInsideTree()) {
                return SiphonBreakReason.ActorGone;
            }
            if (Target == null || !IsInstanceValid(Target) || !Target.IsInsideTree()) {
                return SiphonBreakReason.ActorGone;
            }
            if (Target.CurrentState is CharacterState.Dead or CharacterState.Respawning) {
                return SiphonBreakReason.ActorGone;
            }
            if (_interrupted) return SiphonBreakReason.CasterInterrupted;
            if (IsTargetInvulnerable(Target)) return SiphonBreakReason.TargetInvulnerable;

            bool suppressed = Target.HasStatusEffect(StatusType.Suppression);
            if (suppressed && !_targetWasSuppressed) return SiphonBreakReason.TargetSuppressed;
            _targetWasSuppressed = suppressed;

            if (Caster.GlobalPosition.DistanceTo(Target.GlobalPosition) > RadiusPixels) {
                return SiphonBreakReason.OutOfRange;
            }
            if (!HasLineOfSight(Caster, Caster.GlobalPosition, Target.GlobalPosition)) {
                return SiphonBreakReason.LineOfSightBlocked;
            }
            return SiphonBreakReason.None;
        }

        /// <summary>
        /// Invulnerability that severs a tether — roll i-frames explicitly
        /// included, per the contract's "player invulnerability including active
        /// roll i-frames".
        /// </summary>
        public static bool IsTargetInvulnerable(PlayerController player) =>
            player != null && (player.IsRollInvulnerable || player.IsPostRewindInvulnerable);

        /// <summary>
        /// A hit that the Eraser's V7.4 stagger armor did NOT reject. The caller
        /// (<see cref="EnemyController.ApplyStun"/>) only reaches this past its own
        /// <c>IsStaggerArmored</c> guard, so an armor-rejected hit never lands here.
        /// </summary>
        public void NotifyCasterInterrupted() {
            _interrupted = true;
            if (IsLive) Break(SiphonBreakReason.CasterInterrupted);
        }

        /// <summary>Level/room departure, checkpoint reconstruction, or despawn.</summary>
        public void Release() => Break(SiphonBreakReason.Released);

        /// <summary>
        /// Ends the tether once and frees the node. Idempotent: a second call after
        /// the first does nothing, so a break racing a queue-free cannot double-raise.
        /// </summary>
        public void Break(SiphonBreakReason reason) {
            if (!IsLive) return;
            IsLive = false;
            BreakReason = reason;
            Live.Remove(this);
            Broken?.Invoke(this, reason);
            if (IsInstanceValid(this) && IsInsideTree()) QueueFree();
        }

        public override void _ExitTree() {
            // A tether whose node leaves the tree (room unload, level exit, pooled
            // parent release) ends there and then; it must never keep draining a
            // player it can no longer see.
            if (IsLive) {
                IsLive = false;
                BreakReason = SiphonBreakReason.Released;
                Broken?.Invoke(this, SiphonBreakReason.Released);
            }
            Live.Remove(this);
        }

        /// <summary>Whether the segment from caster to target is clear of solid cover.</summary>
        public static bool HasLineOfSight(Node2D from, Vector2 origin, Vector2 destination) =>
            LineOfSightProbe?.Invoke(from, origin, destination) ?? true;

        /// <summary>
        /// The production probe. Headless-safe in both directions: no world, no
        /// space state or a null query result all read as "clear", so nothing ever
        /// throws inside a test that never built a physics world.
        /// </summary>
        public static bool DefaultLineOfSight(Node2D from, Vector2 origin, Vector2 destination) {
            if (from == null || !IsInstanceValid(from) || !from.IsInsideTree()) return true;
            World2D world = from.GetWorld2D();
            PhysicsDirectSpaceState2D space = world?.DirectSpaceState;
            if (space == null) return true;
            PhysicsRayQueryParameters2D query = PhysicsRayQueryParameters2D.Create(
                origin, destination, CollisionLayers.Environment | CollisionLayers.PersistentObject);
            query.CollideWithAreas = false;
            query.CollideWithBodies = true;
            // Godot.Collections.Dictionary is itself IDisposable, so the wrapper
            // is released deterministically rather than on the finalizer thread.
            using Godot.Collections.Dictionary result = space.IntersectRay(query);
            return result == null || result.Count == 0;
        }
    }
}
