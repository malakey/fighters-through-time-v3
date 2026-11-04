using System;
using System.Collections.Generic;
using Godot;

namespace FTT.Environment {

    /// <summary>
    /// The record of one optional route (rulings 7c, 7c+): an existing dust source at
    /// the end of a harder base-kit line, with a horizontal-burst shortcut and a
    /// rise-or-glide shortcut that movement abilities make quick. Nothing here adds
    /// dust: <see cref="RewardSourceID"/> names a source the level's manifest already
    /// has (F05). A shortcut gates nobody out: every hero reaches the reward along
    /// <see cref="BaseLine"/>. The record is read by <c>TraversalStretchContentTests</c>
    /// and <c>OptionalRouteReachTests</c>.
    /// </summary>
    public partial class OptionalRoute : Node {
        /// <summary>Every route joins this group.</summary>
        public const string GroupName = "story_optional_route";

        private readonly List<TraversalSurface> _baseLine = new();

        /// <summary>Stable ID, <c>level_NN.route_name</c>.</summary>
        public string RouteID { get; private set; } = "";
        /// <summary>The manifest source the route pays: an Extractor share or the secret.</summary>
        public string RewardSourceID { get; private set; } = "";
        /// <summary>Where the reward stands.</summary>
        public Vector2 RewardPosition { get; private set; }

        /// <summary>The base-kit line, from its entry surface on the level's main path to the reward's surface.</summary>
        public IReadOnlyList<TraversalSurface> BaseLine => _baseLine;

        /// <summary>The surface a burst leaves from.</summary>
        public TraversalSurface BurstFrom { get; private set; }
        /// <summary>The surface a burst lands on.</summary>
        public TraversalSurface BurstTo { get; private set; }
        public bool HasBurstShortcut { get; private set; }

        /// <summary>The surface a rise or a glide leaves from.</summary>
        public TraversalSurface RiseFrom { get; private set; }
        /// <summary>The surface a rise or a glide lands on.</summary>
        public TraversalSurface RiseTo { get; private set; }
        public bool HasRiseShortcut { get; private set; }

        /// <summary>Records a route. Throws on a reward with no source: a route never adds dust.</summary>
        public static OptionalRoute Create(string routeID, string rewardSourceID, Vector2 rewardPosition,
            TraversalSurface entry) {
            if (string.IsNullOrEmpty(routeID)) throw new ArgumentException("A route needs a stable ID.", nameof(routeID));
            if (string.IsNullOrEmpty(rewardSourceID)) {
                throw new ArgumentException($"{routeID}: a route pays an existing manifest source, never new dust.", nameof(rewardSourceID));
            }
            var route = new OptionalRoute {
                Name = $"Route_{routeID.Replace('.', '_')}",
                RouteID = routeID,
                RewardSourceID = rewardSourceID,
                RewardPosition = rewardPosition
            };
            route._baseLine.Add(entry);
            route.AddToGroup(GroupName);
            return route;
        }

        /// <summary>Adds the next surface of the base-kit line.</summary>
        public void AddBaseLineSurface(TraversalSurface surface) => _baseLine.Add(surface);

        /// <summary>Marks the horizontal-burst shortcut.</summary>
        public void MarkBurstShortcut(TraversalSurface from, TraversalSurface to) {
            BurstFrom = from;
            BurstTo = to;
            HasBurstShortcut = true;
        }

        /// <summary>Marks the rise-or-glide shortcut.</summary>
        public void MarkRiseShortcut(TraversalSurface from, TraversalSurface to) {
            RiseFrom = from;
            RiseTo = to;
            HasRiseShortcut = true;
        }
    }
}
