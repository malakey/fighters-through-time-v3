using System;
using System.Collections.Generic;
using Godot;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>How one struck era reads on the hub's triage readout.</summary>
    public enum TriageShardState {
        /// <summary>Sealed on an earlier visit: it has mended out of reach (S15) and is gone.</summary>
        Hidden,
        /// <summary>Sealed by the level just completed: flares warm gold, then fades out.</summary>
        SealedFlare,
        /// <summary>Struck and unsealed: burns cold.</summary>
        Cold,
        /// <summary>The portal's destination, failing fastest: flashes red at the top.</summary>
        NextFlashing
    }

    /// <summary>
    /// Package 13 W3 (S11/S15): the wordless triage readout beside the Temporal
    /// Portal. The struck eras of Acts I–II drift as shards ranked by stability:
    /// the next level's era flashes red at the top (it is always the portal's
    /// destination), unsealed eras burn cold below it, and an era sealed by the
    /// level just completed flares warm gold and fades from the display — it has
    /// mended back into the timeline and out of the ship's reach. From Act III on
    /// the readout goes dark: the Wardens cannot see past the Void.
    ///
    /// <para>Non-interactive by construction — no collision, no input, no labels
    /// — so it can never read as a map or a level select. Placeholder
    /// presentation (flat shards on the shared palette); the stability ranking is
    /// the route order, because the route is the triage order (Sarah's S11 line:
    /// "we go where we'd lose the most").</para>
    /// </summary>
    public partial class HubTriageReadout : Node2D {
        public const string NodeName = "TriageReadout";

        /// <summary>Colour of a flashing next-era shard. The one red on the readout.</summary>
        public static readonly Color NextColor = FTT.UI.UIPalette.BossRed;

        /// <summary>The struck eras of Acts I–II, in route (triage) order.</summary>
        public static readonly IReadOnlyList<CampaignLevel> StruckEras = new[] {
            CampaignLevel.Florence, CampaignLevel.Orleans, CampaignLevel.Chicago, CampaignLevel.Paris,
            CampaignLevel.Titanic, CampaignLevel.Pompeii, CampaignLevel.Nassau, CampaignLevel.Egypt,
            CampaignLevel.Berlin, CampaignLevel.London, CampaignLevel.Gettysburg, CampaignLevel.Lunar
        };

        /// <summary>The readout is dark from Act III on, and after the campaign.</summary>
        public static bool IsDark(CampaignLevel nextLevel, bool campaignCompleted) =>
            campaignCompleted || StoryManager.IsActIIILevel(nextLevel);

        /// <summary>
        /// One era's state. <paramref name="justSealed"/> is the level the player
        /// completed to arrive here (the route predecessor of the next mission).
        /// </summary>
        public static TriageShardState StateFor(
            CampaignLevel era, CampaignLevel nextLevel, Func<CampaignLevel, bool> isSealed, CampaignLevel? justSealed) {
            bool sealedEra = isSealed != null && isSealed(era);
            if (sealedEra) return justSealed.HasValue && justSealed.Value == era ? TriageShardState.SealedFlare
                : TriageShardState.Hidden;
            return era == nextLevel ? TriageShardState.NextFlashing : TriageShardState.Cold;
        }

        /// <summary>
        /// The visible shards, top to bottom: the next era first, then the unsealed
        /// cold eras in triage order, then the just-sealed flare. Empty when dark.
        /// </summary>
        public static List<(CampaignLevel Era, TriageShardState State)> Rank(
            CampaignLevel nextLevel, bool campaignCompleted, Func<CampaignLevel, bool> isSealed,
            CampaignLevel? justSealed) {
            var ranked = new List<(CampaignLevel, TriageShardState)>();
            if (IsDark(nextLevel, campaignCompleted)) return ranked;
            var cold = new List<(CampaignLevel, TriageShardState)>();
            (CampaignLevel, TriageShardState)? flare = null;
            foreach (CampaignLevel era in StruckEras) {
                TriageShardState state = StateFor(era, nextLevel, isSealed, justSealed);
                switch (state) {
                    case TriageShardState.NextFlashing: ranked.Insert(0, (era, state)); break;
                    case TriageShardState.Cold: cold.Add((era, state)); break;
                    case TriageShardState.SealedFlare: flare = (era, state); break;
                }
            }
            ranked.AddRange(cold);
            if (flare.HasValue) ranked.Add(flare.Value);
            return ranked;
        }

        /// <summary>The route predecessor of <paramref name="nextLevel"/>, or null at the route's start.</summary>
        public static CampaignLevel? RoutePredecessor(CampaignLevel nextLevel) {
            IReadOnlyList<CampaignLevel> route = StoryManager.CampaignRoute;
            for (int index = 1; index < route.Count; index++) {
                if (route[index] == nextLevel) return route[index - 1];
            }
            return null;
        }

        /// <summary>The shards as built, top to bottom. Test seam.</summary>
        public IReadOnlyList<(CampaignLevel Era, TriageShardState State)> Shards => _shards;
        private List<(CampaignLevel Era, TriageShardState State)> _shards = new();

        /// <summary>True when the readout is dark (Act III or later). Test seam.</summary>
        public bool Dark { get; private set; }

        /// <summary>Builds the shards for the current campaign state. Idempotent: it clears first.</summary>
        public void Build(CampaignLevel nextLevel, bool campaignCompleted, Func<CampaignLevel, bool> isSealed) {
            Godot.Collections.Array<Node> children = GetChildren();
            using var childrenLifetime = children.AsDisposable();
            foreach (Node child in children) {
                RemoveChild(child);
                child.QueueFree();
            }
            Dark = IsDark(nextLevel, campaignCompleted);
            _shards = Rank(nextLevel, campaignCompleted, isSealed, RoutePredecessor(nextLevel));

            // The dark screen itself stays: an unlit panel reads as "we can't see".
            AddChild(new ColorRect {
                Name = "Screen",
                Size = new Vector2(96, 250),
                Position = new Vector2(-48, -250),
                Color = new Color(0.02f, 0.03f, 0.06f, 0.9f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            });
            if (Dark) return;

            for (int index = 0; index < _shards.Count; index++) {
                (CampaignLevel era, TriageShardState state) = _shards[index];
                var shard = new ColorRect {
                    Name = $"Shard_{era}",
                    Size = new Vector2(state == TriageShardState.NextFlashing ? 44 : 30, 12),
                    Position = new Vector2(-22 + (index % 2 == 0 ? 0 : 10), -238 + index * 18),
                    RotationDegrees = index % 2 == 0 ? -6f : 5f,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Color = state switch {
                        TriageShardState.NextFlashing => NextColor,
                        TriageShardState.SealedFlare => FTT.UI.UIPalette.ResonanceGoldBright,
                        _ => FTT.UI.UIPalette.UnboundColdDim
                    }
                };
                AddChild(shard);
                if (state == TriageShardState.NextFlashing) {
                    Tween flash = shard.CreateTween().SetLoops();
                    flash.TweenProperty(shard, "modulate:a", 0.35f, 0.45);
                    flash.TweenProperty(shard, "modulate:a", 1f, 0.45);
                } else if (state == TriageShardState.SealedFlare) {
                    Tween flare = shard.CreateTween();
                    flare.TweenProperty(shard, "scale", new Vector2(1.4f, 1.4f), 0.4);
                    flare.TweenInterval(1.2);
                    flare.TweenProperty(shard, "modulate:a", 0f, 1.4);
                }
            }
        }
    }
}
