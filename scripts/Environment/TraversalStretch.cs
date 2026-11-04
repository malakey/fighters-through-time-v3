using System;
using System.Collections.Generic;
using Godot;

namespace FTT.Environment {

    /// <summary>
    /// The record of one traversal stretch: the span between two encounter locks that
    /// a hero crosses by platforming (rulings 7a, 7b). It carries no behaviour. The
    /// geometry is ordinary level geometry; this node says which of it is the stretch,
    /// so <c>TraversalStretchContentTests</c> can sweep every level. A base level gets
    /// one from <c>StoryLevelControllerBase.BeginStretch</c>; Florence, which does not
    /// extend the base, calls <see cref="Create"/> and adds the child itself.
    /// </summary>
    public partial class TraversalStretch : Node {
        /// <summary>Every stretch joins this group.</summary>
        public const string GroupName = "story_traversal_stretch";

        private readonly List<TraversalSurface> _steps = new();

        /// <summary>Stable ID, <c>level_NN.stretch_name</c>.</summary>
        public string StretchID { get; private set; } = "";
        public float MinX { get; private set; }
        public float MaxX { get; private set; }
        /// <summary>The deck at both ends of the stretch.</summary>
        public float DeckY { get; private set; }
        /// <summary>Where the west deck ends.</summary>
        public float WestDeckEndX { get; private set; }
        /// <summary>Where the east deck begins.</summary>
        public float EastDeckStartX { get; private set; }
        /// <summary>The stretch's own room trigger, which holds its camera rect.</summary>
        public RoomTransitionTrigger Room { get; private set; }

        /// <summary>True once a trench is recorded.</summary>
        public bool HasTrench { get; private set; }
        /// <summary>The top of the trench floor.</summary>
        public float TrenchFloorY { get; private set; }
        /// <summary>The run of the trench's two ramps.</summary>
        public float TrenchRampRun { get; private set; }

        /// <summary>
        /// The line a hero hops: west deck, every step in authored order, east deck.
        /// Each consecutive pair obeys <see cref="TraversalRules.ValidateLine"/>.
        /// </summary>
        public IReadOnlyList<TraversalSurface> Line {
            get {
                var line = new List<TraversalSurface>(_steps.Count + 2) { new(MinX, WestDeckEndX, DeckY) };
                line.AddRange(_steps);
                line.Add(new TraversalSurface(EastDeckStartX, MaxX, DeckY));
                return line;
            }
        }

        /// <summary>Records a stretch. Throws on a span that cannot be one.</summary>
        public static TraversalStretch Create(string stretchID, float minX, float maxX, float deckY,
            float westDeckEndX, float eastDeckStartX, RoomTransitionTrigger room) {
            if (string.IsNullOrEmpty(stretchID)) throw new ArgumentException("A stretch needs a stable ID.", nameof(stretchID));
            if (maxX <= minX) throw new ArgumentException($"{stretchID}: the span {minX}..{maxX} is empty.", nameof(maxX));
            if (westDeckEndX < minX || eastDeckStartX > maxX || eastDeckStartX < westDeckEndX) {
                throw new ArgumentException($"{stretchID}: the decks {westDeckEndX} and {eastDeckStartX} must lie in order inside {minX}..{maxX}.");
            }
            if (room == null) throw new ArgumentNullException(nameof(room), $"{stretchID}: a stretch needs its own room trigger.");
            var stretch = new TraversalStretch {
                Name = $"Stretch_{stretchID.Replace('.', '_')}",
                StretchID = stretchID,
                MinX = minX,
                MaxX = maxX,
                DeckY = deckY,
                WestDeckEndX = westDeckEndX,
                EastDeckStartX = eastDeckStartX,
                Room = room
            };
            stretch.AddToGroup(GroupName);
            return stretch;
        }

        /// <summary>Adds the next surface of the line.</summary>
        public void AddLineSurface(TraversalSurface surface) => _steps.Add(surface);

        /// <summary>Records the trench under the line.</summary>
        public void RecordTrench(float floorY, float rampRun) {
            HasTrench = true;
            TrenchFloorY = floorY;
            TrenchRampRun = rampRun;
        }
    }
}
