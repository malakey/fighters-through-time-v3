using Godot;
using FTT.Characters;
using FTT.Core;
using FTT.UI;

namespace FTT.Environment {

    /// <summary>
    /// V7.6 Act III — the Warden Beacon (Package 11 A3b).
    ///
    /// <para>Sarah hands the hero a Beacon at Level 13's entry: <i>data crosses
    /// the field, matter doesn't</i>. Interacting with it at <b>any activated Act
    /// III checkpoint</b> opens the <b>existing</b> Repository screen
    /// (<see cref="ResonanceGridPanel"/>) for grid purchases and the free respec.
    /// There is deliberately <b>no new UI</b> — the design's whole point is that
    /// the gauntlet adds rules, not screens.</para>
    ///
    /// <para><b>F02 resolution (Option A).</b> The Beacon cannot deposit or spend
    /// the active level's <c>levelChronalDust</c>: it opens the panel
    /// <i>without</i> the hub's deposit step, so the spendable balance is exactly
    /// <see cref="ResonanceProgression.SpendableBalance"/> — previously deposited
    /// dust only — and the checkpoint UI offers no Deposit action. Current-level
    /// dust banks once at level completion, "sealed through the Beacon", which is
    /// what makes Level 13's earnings spendable at Level 14's Beacon. The free
    /// respec therefore refunds only previously spent deposited dust.</para>
    ///
    /// <para>It also carries the level's <b>anchor charges</b>; the charges
    /// themselves live on <see cref="StoryManager"/> (they must survive the scene
    /// this node belongs to) and A8 renders them as gold pips on the Beacon icon
    /// beside the rewind counter.</para>
    /// </summary>
    public partial class WardenBeacon : Node2D, IInteractable {

        /// <summary>Every live Beacon joins this group.</summary>
        public const string GroupName = "warden_beacon";

        /// <summary>The interaction prompt. Raw key; the interaction area translates it.</summary>
        public const string PromptTranslationKey = "beacon_interaction_open_repository";

        /// <summary>The stable ID of the checkpoint this Beacon stands at.</summary>
        [Export] public string CheckpointID = "";

        /// <summary>Beacon body colour — Warden gold against the Unbound cold.</summary>
        public static readonly Color BeaconColor = new(0.96f, 0.78f, 0.32f, 1f);

        /// <summary>The panel currently open from this Beacon, if any. Test seam.</summary>
        public ResonanceGridPanel OpenPanel { get; private set; }

        /// <summary>Number of times this Beacon has opened the Repository. Test seam.</summary>
        public int OpenCount { get; private set; }

        private CanvasLayer _layer;
        private PlayerController _suspendedPlayer;

        public string InteractionID => $"warden_beacon_{CheckpointID}";
        public string PromptKey => PromptTranslationKey;

        public override void _Ready() {
            AddToGroup(GroupName);
            if (GetNodeOrNull<InteractionArea>("InteractionArea") == null) BuildInteraction();
        }

        /// <summary>
        /// The Beacon is live only at an <b>activated</b> Act III checkpoint —
        /// the design's exact wording. An anchor the player walked past without
        /// striking is not a Repository terminal, and a Beacon authored outside
        /// Act III is inert rather than a second hub.
        /// </summary>
        public bool CanInteract(PlayerController player) {
            if (player == null) return false;
            StoryManager story = StoryManager.Instance;
            if (story == null) return false;
            if (!StoryManager.IsActIIILevel(story.CurrentLevel)) return false;
            return story.IsCheckpointActivated(CheckpointID);
        }

        public void Interact(PlayerController player) {
            if (!CanInteract(player) || OpenPanel != null) return;
            OpenCount++;
            // NOTE the absence: the hub's Repository calls
            // DepositDustToActiveSave() immediately before opening this same
            // panel. The Beacon must NOT — that call is the deposit action F02
            // forbids at a checkpoint.
            OpenRepository(player);
        }

        private void OpenRepository(PlayerController player) {
            _layer = new CanvasLayer { Name = "BeaconRepositoryLayer", Layer = 90 };
            AddChild(_layer);
            OpenPanel = new ResonanceGridPanel { Name = "ResonanceGridPanel" };
            OpenPanel.Closed += CloseRepository;
            _suspendedPlayer = player;
            if (player != null && IsInstanceValid(player)) player.ProcessMode = ProcessModeEnum.Disabled;
            _layer.AddChild(OpenPanel);
        }

        private void CloseRepository() {
            OpenPanel = null;
            if (_suspendedPlayer != null && IsInstanceValid(_suspendedPlayer)) {
                _suspendedPlayer.ProcessMode = ProcessModeEnum.Inherit;
            }
            _suspendedPlayer = null;
            if (_layer != null && IsInstanceValid(_layer)) _layer.QueueFree();
            _layer = null;
        }

        public override void _ExitTree() {
            // Whoever suspends the player hands it back, including on teardown.
            if (_suspendedPlayer != null && IsInstanceValid(_suspendedPlayer)) {
                _suspendedPlayer.ProcessMode = ProcessModeEnum.Inherit;
            }
            _suspendedPlayer = null;
        }

        /// <summary>
        /// The placeholder Beacon body plus its interaction area. Built in code
        /// like the rest of the Story graybox layer; production art is Package 10.
        /// </summary>
        private void BuildInteraction() {
            AddChild(new Polygon2D {
                Name = "BeaconVisual",
                Polygon = new[] {
                    new Vector2(-14f, 0f), new Vector2(14f, 0f),
                    new Vector2(10f, -66f), new Vector2(-10f, -66f)
                },
                Color = BeaconColor
            });
            var area = new InteractionArea {
                Name = "InteractionArea",
                TargetPath = "..",
                PromptLabelPath = "Prompt"
            };
            area.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(110f, 150f) },
                Position = new Vector2(0f, -70f)
            });
            var prompt = new Label {
                Name = "Prompt",
                Text = "",
                Position = new Vector2(-110f, -122f),
                CustomMinimumSize = new Vector2(220f, 26f),
                HorizontalAlignment = HorizontalAlignment.Center,
                Visible = false
            };
            area.AddChild(prompt);
            AddChild(area);
        }

        /// <summary>
        /// Places a Beacon at an activated-capable Act III anchor. Used by
        /// <c>StoryLevelControllerBase.BuildCheckpoint</c>; exposed so a test can
        /// build one without a whole level.
        /// </summary>
        public static WardenBeacon Create(string checkpointID, Vector2 position) => new() {
            Name = $"WardenBeacon_{checkpointID}",
            CheckpointID = checkpointID ?? "",
            Position = position
        };
    }
}
