using FTT.Combat;
using FTT.Core;
using Godot;

namespace FTT.Environment {

    /// <summary>
    /// GR-LV4 (ruling 14): a cover fence in the Siege Cannon's arena. A solid body on
    /// Environment, so it shelters from a shell and stops a warp, and it takes no damage until
    /// its level sets <see cref="Breakable"/>. Not a puzzle object, so no bot goes out of its
    /// way to break one. A rewind leaves it as it stands, as it leaves every arena change.
    /// </summary>
    public partial class RailcutFence : DestructibleBlock {
        /// <summary>False through the boss's first phase: the fence is cover, and nothing breaks it.</summary>
        public bool Breakable { get; set; }

        protected override bool AcceptsEnvironmentDamage => Breakable;

        /// <summary>Builds a fence standing with its centre at <paramref name="position"/>; add it to the tree yourself.</summary>
        public static RailcutFence Create(string objectID, int hitsToBreak, Vector2 position, Vector2 size, Color fill) {
            var fence = new RailcutFence {
                Name = $"Fence_{objectID.Replace('.', '_')}",
                ObjectID = objectID,
                HitsToBreak = hitsToBreak,
                Position = position,
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0,
                RewindPolicy = StoryRewindPolicy.PreserveCurrentState
            };
            fence.AddChild(new CollisionShape2D {
                Name = "CollisionShape2D",
                Shape = new RectangleShape2D { Size = size }
            });
            fence.AddChild(new ColorRect {
                Name = "Visual",
                Size = size,
                Position = -size / 2f,
                Color = fill,
                MouseFilter = Control.MouseFilterEnum.Ignore
            });
            var hurtbox = new EnvironmentHurtboxAdapter {
                Name = "Hurtbox",
                CollisionLayer = CollisionLayers.PersistentObject,
                CollisionMask = CollisionLayers.PlayerHitbox,
                OwnerPlayerIndex = -1
            };
            hurtbox.AddChild(new CollisionShape2D {
                Name = "CollisionShape2D",
                Shape = new RectangleShape2D { Size = size }
            });
            fence.AddChild(hurtbox);
            return fence;
        }
    }
}
