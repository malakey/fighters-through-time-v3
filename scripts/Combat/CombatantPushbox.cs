using Godot;
using System;
using System.Collections.Generic;
using FTT.Core;

namespace FTT.Combat {

    /// <summary>
    /// Non-damaging combatant volume used for horizontal jostling. Hitboxes and
    /// hurtboxes remain query-only Areas and never become movement blockers.
    /// </summary>
    public partial class CombatantPushbox : Area2D {
        public const string GroupName = "CombatantPushboxes";

        [Export] public Vector2 BoxSize = new(30f, 48f);
        [Export] public bool BlocksRollThrough;
        public bool PushEnabled { get; private set; } = true;

        public CharacterBody2D Body => GetParentOrNull<CharacterBody2D>();

        public override void _Ready() {
            AddToGroup(GroupName);
        }

        public void SetPushEnabled(bool enabled) {
            PushEnabled = enabled;
        }

        public bool ResolveStoryOverlaps(int preferredDirection = 0) {
            CharacterBody2D body = Body;
            if (!PushEnabled || body == null || GetTree() == null) return false;
            bool resolvedAny = false;

            var candidates = new List<CombatantPushbox>();
            Godot.Collections.Array<Node> members = GetTree().GetNodesInGroup(GroupName);
            using var membersLifetime = members.AsDisposable();
            foreach (Node node in members) {
                if (node is CombatantPushbox other
                    && other != this
                    && other.PushEnabled
                    && other.Body is CharacterBody2D otherCandidate
                    // A pool-parked combatant is still in the tree (under the
                    // pool's inactive container) and still in this group, but
                    // its processing is disabled and its body must not be
                    // jostled — MoveAndCollide on it errors with a null space.
                    && otherCandidate.CanProcess()
                    && CanInteractWith(other)) {
                    candidates.Add(other);
                }
            }
            candidates.Sort(CompareStable);

            // Two bounded passes settle small groups without an unbounded solver.
            for (int pass = 0; pass < 2; pass++) {
                foreach (CombatantPushbox other in candidates) {
                    float overlap = GetHorizontalOverlap(other);
                    if (overlap <= 0f) continue;
                    resolvedAny = true;

                    int separationDirection = preferredDirection != 0
                        ? Math.Sign(preferredDirection)
                        : GetStableSeparationDirection(other);
                    float requestedForSelf = overlap * 0.5f * separationDirection;
                    float movedSelf = MoveHorizontal(body, requestedForSelf);
                    float remaining = Mathf.Max(0f, overlap - Mathf.Abs(movedSelf));
                    CharacterBody2D otherBody = other.Body;
                    float movedOther = MoveHorizontal(otherBody, -remaining * separationDirection);
                    remaining = Mathf.Max(0f, remaining - Mathf.Abs(movedOther));
                    if (remaining > 0f) MoveHorizontal(body, remaining * separationDirection);
                    CancelInwardVelocity(body, otherBody, separationDirection);
                }
            }
            return resolvedAny;
        }

        /// <summary>
        /// Keeps rolls from crossing explicitly immovable combatants such as large
        /// bosses while ordinary enemy pushboxes remain pass-through.
        /// </summary>
        public bool ResolveBlockingRollOverlaps(int rollDirection) {
            CharacterBody2D body = Body;
            if (body == null || GetTree() == null || rollDirection == 0) return false;
            bool blocked = false;
            Godot.Collections.Array<Node> members = GetTree().GetNodesInGroup(GroupName);
            using var membersLifetime = members.AsDisposable();
            foreach (Node node in members) {
                if (node is not CombatantPushbox other
                    || other == this
                    || !other.PushEnabled
                    || !other.BlocksRollThrough
                    || other.Body is not CharacterBody2D otherBlocking
                    || !otherBlocking.CanProcess()
                    || !CanInteractWith(other)) continue;
                float overlap = GetHorizontalOverlap(other);
                if (overlap <= 0f) continue;
                MoveHorizontal(body, -Math.Sign(rollDirection) * overlap);
                var velocity = body.Velocity;
                velocity.X = 0f;
                body.Velocity = velocity;
                blocked = true;
            }
            return blocked;
        }

        public float GetHorizontalOverlap(CombatantPushbox other) {
            if (other == null) return 0f;
            float verticalOverlap = (BoxSize.Y + other.BoxSize.Y) * 0.5f
                - Mathf.Abs(GlobalPosition.Y - other.GlobalPosition.Y);
            if (verticalOverlap <= 0f) return 0f;
            return Mathf.Max(
                0f,
                (BoxSize.X + other.BoxSize.X) * 0.5f
                    - Mathf.Abs(GlobalPosition.X - other.GlobalPosition.X));
        }

        private bool CanInteractWith(CombatantPushbox other) =>
            (CollisionMask & other.CollisionLayer) != 0
            && (other.CollisionMask & CollisionLayer) != 0;

        private int GetStableSeparationDirection(CombatantPushbox other) {
            float delta = GlobalPosition.X - other.GlobalPosition.X;
            if (!Mathf.IsZeroApprox(delta)) return delta > 0f ? 1 : -1;
            return GetInstanceId() < other.GetInstanceId() ? -1 : 1;
        }

        private static int CompareStable(CombatantPushbox first, CombatantPushbox second) {
            int position = first.GlobalPosition.X.CompareTo(second.GlobalPosition.X);
            return position != 0 ? position : first.GetInstanceId().CompareTo(second.GetInstanceId());
        }

        private static float MoveHorizontal(CharacterBody2D body, float amount) {
            if (body == null || !body.IsInsideTree() || Mathf.IsZeroApprox(amount)) return 0f;
            float before = body.GlobalPosition.X;
            body.MoveAndCollide(new Vector2(amount, 0f), false, 0.001f, false);
            return body.GlobalPosition.X - before;
        }

        private static void CancelInwardVelocity(
            CharacterBody2D first,
            CharacterBody2D second,
            int firstSeparationDirection) {
            var firstVelocity = first.Velocity;
            if (firstVelocity.X * firstSeparationDirection < 0f) firstVelocity.X = 0f;
            first.Velocity = firstVelocity;

            var secondVelocity = second.Velocity;
            if (secondVelocity.X * firstSeparationDirection > 0f) secondVelocity.X = 0f;
            second.Velocity = secondVelocity;
        }
    }
}
