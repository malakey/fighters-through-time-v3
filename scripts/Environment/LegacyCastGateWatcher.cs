using Godot;
using FTT.Characters;
using FTT.Combat;

namespace FTT.Environment {

    /// <summary>
    /// The missing fifth <see cref="LegacyGateMode"/>, implemented as a child node
    /// rather than as an enum member (Package 11 B2; see the plan's §9 B2 block).
    ///
    /// <para><b>Why it exists.</b> A12's four gate modes cover the four shapes
    /// Einstein's kit takes: a struck hitbox (<c>Strike</c>), a placed
    /// <c>story_zone</c> (<c>Zone</c>), a movement-ability arrival
    /// (<c>Traversal</c>) and the F04 Ultimate set-piece (<c>Nexus</c>). Three of
    /// the nine roster specials are none of those:</para>
    /// <list type="bullet">
    ///   <item><b>Deployed constructs</b> — Cleopatra's Serpent Nest and Tesla's
    ///     Tesla Coil. The construct's own hits carry the ability ID, but they are
    ///     delivered by a private <c>DirectSpaceState</c> sweep against the
    ///     <c>EnemyHurtbox</c> layer, so they never reach a gate surface that sits
    ///     on <c>PersistentObject</c> like every other authored strike surface.</item>
    ///   <item><b>Self-affecting field specials</b> — Shakespeare's The Tempest,
    ///     which lifts the caster and pushes adjacent <i>bodies</i>. It spawns no
    ///     zone and raises no hitbox at all, so nothing in the combat pipeline can
    ///     observe it.</item>
    /// </list>
    ///
    /// <para><b>What it recognises.</b> The hero performing the authored ability
    /// inside the mechanism's area — the same contract every other mode enforces
    /// (V01c: this one ability and nothing else). It reads the player's own
    /// <see cref="BaseSpecial"/> children, so a construct, a decoy, an enemy or an
    /// incidental physics contact can never stand in for the cast.</para>
    ///
    /// <para><b>Why not edit <see cref="LegacyKitGate"/>.</b> Adding
    /// <c>LegacyGateMode.Cast</c> is the right long-term shape, but
    /// <c>LegacyKitGate.cs</c> is A12's file and all three B-wave agents hit this
    /// same wall in parallel. Folding the three implementations into one enum
    /// member belongs to Phase C; until then this watcher keeps the behaviour
    /// entirely inside B2's own files. A gate carrying a watcher keeps its declared
    /// <see cref="LegacyKitGate.Mode"/> for presentation and for the base's route
    /// bookkeeping; the watcher is the resolver.</para>
    ///
    /// <para>Story-only. Nothing here is visible to <c>scripts/FighterSim/</c>.</para>
    /// </summary>
    public partial class LegacyCastGateWatcher : Node2D {

        /// <summary>How close the caster must be to the mechanism.</summary>
        [Export] public float ResolveRadius = 220f;

        /// <summary>The gate this watcher resolves. Assigned by the level controller.</summary>
        public LegacyKitGate Gate;

        /// <summary>
        /// The one ability that opens the gate. Mirrors
        /// <see cref="LegacyKitGate.RequiredAbilityID"/> and is never a slot or a
        /// display name.
        /// </summary>
        public string RequiredAbilityID = "";

        public override void _PhysicsProcess(double delta) {
            if (Gate == null || !IsInstanceValid(Gate) || Gate.IsResolved) return;
            if (GetTree()?.GetFirstNodeInGroup("StoryPlayer") is not PlayerController player) return;
            if (!IsInstanceValid(player)) return;
            if (!IsCasting(player)) return;
            TryRecognize(RequiredAbilityID, player.GlobalPosition);
        }

        /// <summary>
        /// Resolves the gate when the authored ability was cast within range. The
        /// single entry point — <c>_PhysicsProcess</c> and the tests both go through
        /// it, so the accepted ability and the accepted distance are stated once.
        /// </summary>
        public bool TryRecognize(string abilityID, Vector2 castPosition) {
            if (Gate == null || !IsInstanceValid(Gate)) return false;
            if (castPosition.DistanceTo(GlobalPosition) > ResolveRadius) return false;
            return Gate.TryResolve(abilityID);
        }

        /// <summary>
        /// True while one of the player's own ability nodes is mid-execution with the
        /// authored ability ID. Reads <see cref="BaseSpecial.Data"/> rather than a
        /// slot, so an ability moved between slots by a later pass still matches.
        /// </summary>
        private bool IsCasting(PlayerController player) {
            foreach (Node child in player.GetChildren()) {
                if (child is not BaseSpecial special || !IsInstanceValid(special)) continue;
                if (!special.IsExecuting) continue;
                if (special.Data?.AbilityID == RequiredAbilityID) return true;
            }
            return false;
        }
    }
}
