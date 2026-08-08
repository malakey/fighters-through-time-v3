using System.Collections.Generic;
using FTT.Core;
using Godot;

namespace FTT.Combat {

    /// <summary>
    /// Suspends presentation work for nodes that are off-screen: animated sprite
    /// playback and particle emission stop when the owner leaves the view and
    /// resume when it returns.
    ///
    /// Presentation only. It never touches <c>_PhysicsProcess</c>, collision,
    /// simulation state, or any Story gameplay logic — an off-screen enemy keeps
    /// patrolling and keeps taking damage, it just stops animating.
    /// </summary>
    public partial class PresentationVisibilitySuspender : VisibleOnScreenNotifier2D {
        public const string NodeName = "PresentationVisibility";

        /// <summary>Presentation subtree root; children are walked for sprites/particles.</summary>
        [Export] public NodePath TargetPath { get; set; }

        private Node _target;
        private readonly List<AnimatedSprite2D> _sprites = new();
        private readonly List<GpuParticles2D> _gpuParticles = new();
        private readonly List<CpuParticles2D> _cpuParticles = new();

        /// <summary>True while the owner is considered off-screen and suspended.</summary>
        public bool IsSuspended { get; private set; }

        private bool _signalsBound;

        // Pooled owners re-enter the tree many times but _Ready runs once, so the
        // signal wiring lives on the enter/exit pair with an explicit guard.
        public override void _EnterTree() {
            if (_signalsBound) return;
            _signalsBound = true;
            ScreenEntered += OnScreenEntered;
            ScreenExited += OnScreenExited;
        }

        public override void _Ready() {
            if (_target == null && TargetPath != null && !TargetPath.IsEmpty) {
                _target = GetNodeOrNull(TargetPath);
            }
            _target ??= GetParent();
            Collect();
        }

        public override void _ExitTree() {
            // Never leave an entity frozen because its notifier was removed.
            if (IsSuspended) Resume();
            if (!_signalsBound) return;
            _signalsBound = false;
            ScreenEntered -= OnScreenEntered;
            ScreenExited -= OnScreenExited;
        }

        public void Bind(Node target) {
            _target = target;
            Collect();
        }

        private void Collect() {
            _sprites.Clear();
            _gpuParticles.Clear();
            _cpuParticles.Clear();
            if (_target == null) return;
            CollectFrom(_target);
        }

        private void CollectFrom(Node node) {
            if (node is AnimatedSprite2D animated) _sprites.Add(animated);
            if (node is GpuParticles2D gpu) _gpuParticles.Add(gpu);
            if (node is CpuParticles2D cpu) _cpuParticles.Add(cpu);
            Godot.Collections.Array<Node> children = node.GetChildren();
            using var lifetime = children.AsDisposable();
            foreach (Node child in children) {
                if (child is PresentationVisibilitySuspender) continue;
                CollectFrom(child);
            }
        }

        private void OnScreenEntered() => Resume();
        private void OnScreenExited() => Suspend();

        public void Suspend() {
            if (IsSuspended) return;
            IsSuspended = true;
            foreach (AnimatedSprite2D sprite in _sprites) {
                if (IsInstanceValid(sprite)) sprite.Pause();
            }
            foreach (GpuParticles2D particles in _gpuParticles) {
                if (IsInstanceValid(particles)) particles.Emitting = false;
            }
            foreach (CpuParticles2D particles in _cpuParticles) {
                if (IsInstanceValid(particles)) particles.Emitting = false;
            }
        }

        public void Resume() {
            if (!IsSuspended) return;
            IsSuspended = false;
            foreach (AnimatedSprite2D sprite in _sprites) {
                if (IsInstanceValid(sprite)) sprite.Play();
            }
            foreach (GpuParticles2D particles in _gpuParticles) {
                if (IsInstanceValid(particles)) particles.Emitting = true;
            }
            foreach (CpuParticles2D particles in _cpuParticles) {
                if (IsInstanceValid(particles)) particles.Emitting = true;
            }
        }

        /// <summary>
        /// Finds or creates the suspender under <paramref name="owner"/>, sized around
        /// <paramref name="presentation"/>. Returns null when there is nothing to watch.
        /// </summary>
        public static PresentationVisibilitySuspender AttachTo(Node2D owner, Node presentation,
            Vector2 size = default) {
            if (owner == null) return null;
            var existing = owner.GetNodeOrNull<PresentationVisibilitySuspender>(NodeName);
            if (existing != null) {
                existing.Bind(presentation ?? owner);
                return existing;
            }
            Vector2 extent = size == default ? new Vector2(160f, 200f) : size;
            var suspender = new PresentationVisibilitySuspender {
                Name = NodeName,
                Rect = new Rect2(-extent.X * 0.5f, -extent.Y, extent.X, extent.Y * 1.25f)
            };
            suspender.Bind(presentation ?? owner);
            owner.AddChild(suspender);
            return suspender;
        }
    }
}
