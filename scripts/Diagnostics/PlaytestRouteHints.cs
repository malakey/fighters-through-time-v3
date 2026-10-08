using Godot;
using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Environment;

namespace FTT.Diagnostics {

    /// <summary>
    /// Per-level route hints for the Story playtest bots: a small data table of
    /// "while this is true, go there (and maybe press Interact / wait)" steps,
    /// consulted after the generic tasks (checkpoints, puzzles, breakables) and
    /// before plain travel. The generic navigation and puzzle solvers carry most
    /// of every level; a hint exists only where a level's intended route is not
    /// discoverable from geometry and puzzle state alone. Each entry names the
    /// level geometry it encodes, so a geometry change that invalidates it is
    /// easy to spot.
    /// </summary>
    public sealed class PlaytestRouteHints {
        /// <summary>What a scripted hint step may ask the bot to do.</summary>
        public interface IHintDriver {
            int Frame { get; }
            /// <summary>Navigate toward a point; false once arrived.</summary>
            bool GoTo(PlayerController player, Vector2 goal, float arriveRadius, float maxBelow, ref BotIntent intent);
        }

        /// <summary>A scripted step: returns true while it keeps control of the frame.</summary>
        public delegate bool HintScript(PlaytestWorld world, PlayerController player, IHintDriver driver, ref BotIntent intent);

        public sealed class Hint {
            public string Name = "";
            /// <summary>The hint applies while this is true (and it has not completed its hold).</summary>
            public Func<PlaytestWorld, PlayerController, bool> When = (_, _) => true;
            public Vector2 Goal;
            public float Radius = 30f;
            public float MaxBelow = 260f;
            /// <summary>Optional: an interactable to work instead of walking to <see cref="Goal"/>.</summary>
            public Func<PlaytestWorld, Node2D> Interact;
            /// <summary>Optional: a scripted maneuver instead of walking to <see cref="Goal"/>.</summary>
            public HintScript Script;
            /// <summary>Frames to stand at the goal once reached (0 = the hint ends on arrival).</summary>
            public int HoldFrames;

            private int _arrivedFrame = -1;
            private bool _finished;

            public bool Finished(PlaytestWorld world) => _finished;

            public void Arrived(PlaytestWorld world, PlayerController player, int frame) {
                if (_arrivedFrame < 0) _arrivedFrame = frame;
                if (HoldFrames <= 0 || frame - _arrivedFrame >= HoldFrames) _finished = true;
            }

            public void Reset() {
                _arrivedFrame = -1;
                _finished = false;
            }
        }

        private readonly List<Hint> _hints = new();

        public IReadOnlyList<Hint> Hints => _hints;

        public Hint Active(PlaytestWorld world, PlayerController player) {
            foreach (Hint hint in _hints) {
                bool applies = hint.When(world, player);
                if (!applies) {
                    hint.Reset();
                    continue;
                }
                if (!hint.Finished(world)) return hint;
            }
            return null;
        }

        private PlaytestRouteHints Add(Hint hint) {
            _hints.Add(hint);
            return this;
        }

        public static PlaytestRouteHints For(CampaignLevel level) {
            var table = new PlaytestRouteHints();
            switch (level) {
                default:
                    break;
            }
            return table;
        }

        // --- small helpers for hint conditions ---

        internal static T Find<T>(PlaytestWorld world, string name) where T : Node {
            Node scene = world.Tree.CurrentScene;
            return scene?.FindChild(name, true, false) as T;
        }
    }
}
