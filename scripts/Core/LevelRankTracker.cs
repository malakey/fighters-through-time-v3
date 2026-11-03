using FTT.Characters;
using Godot;

namespace FTT.Core {

    /// <summary>
    /// Feeds the level rank's damage tally from the campaign hero's
    /// <see cref="PlayerController.DamageTaken"/> observations. Attached once per level
    /// scene by <c>StorySceneBootstrapper</c> (which also covers Florence, which does not
    /// extend the level base class). The hero is never the Mirror clone: the clone leaves
    /// the <c>StoryPlayer</c> group and is <see cref="PlayerController.IsStoryHostile"/>.
    ///
    /// <para>The event can run inside a physics flush, so the handler writes no node
    /// state: <see cref="StoryManager.AddRankDamage"/> only adds to the attempt's numbers.</para>
    /// </summary>
    public partial class LevelRankTracker : Node {
        public const string NodeName = "LevelRankTracker";

        private PlayerController _hero;

        /// <summary>The hero this tracker is subscribed to, or null. Test seam.</summary>
        internal PlayerController BoundHero => _hero;

        public override void _PhysicsProcess(double delta) => BindHero();

        /// <summary>Subscribes to the scene's hero once it exists. Cheap while it stays bound.</summary>
        internal void BindHero() {
            if (_hero != null && IsInstanceValid(_hero) && _hero.IsInsideTree()) return;
            Unbind();
            if (GetTree()?.GetFirstNodeInGroup("StoryPlayer") is not PlayerController hero
                || hero.IsStoryHostile) {
                return;
            }
            _hero = hero;
            _hero.DamageTaken += OnHeroDamageTaken;
        }

        public override void _ExitTree() => Unbind();

        private void Unbind() {
            // The event is a plain C# event: leaving it touches no engine state,
            // so a hero already freed is safe to unsubscribe from.
            if (_hero != null) _hero.DamageTaken -= OnHeroDamageTaken;
            _hero = null;
        }

        private static void OnHeroDamageTaken(PlayerDamageObservation observation) {
            if (LevelRankRules.CountsTowardTally(observation.Kind, observation.Delivery)) {
                StoryManager.Instance?.AddRankDamage(observation.Damage);
            }
        }
    }
}
