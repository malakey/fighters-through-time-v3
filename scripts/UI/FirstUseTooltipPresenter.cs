using System.Collections.Generic;
using Godot;
using FTT.Characters;
using FTT.Core;

namespace FTT.UI {

    /// <summary>
    /// Package 13 W3 (S02): shows the skip path's first-use tooltips in a level.
    ///
    /// <para>Rules live in <see cref="FirstUseTooltips"/>; this node only watches
    /// for each mechanic becoming relevant and shows its one line. A tooltip
    /// <b>pauses nothing</b> (the layer never touches <c>SceneTree.Paused</c>),
    /// shows <see cref="DisplaySeconds"/> seconds, queues behind another, and is
    /// consumed once per save slot. A slot that ran the Full Calibration never
    /// arms the ledger, so for it this node is inert.</para>
    ///
    /// <para>Triggers (placeholder proxies where the engine has no single event):
    /// the first HP loss (a Rally echo forms), the first guard break, the first
    /// absorbed block (a grab opportunity — the guard is up at close range), the
    /// first airborne hitstun (a launch), the Defy seal going Ready or Spent, the
    /// first ledge hang, the first stand on a one-way platform, the first death
    /// rewind, and the first level with a Time Freeze controller.</para>
    /// </summary>
    public partial class FirstUseTooltipPresenter : CanvasLayer {
        public const string NodeName = "FirstUseTooltips";
        public const float DisplaySeconds = 5f;

        private readonly Queue<string> _pending = new();
        private Label _label;
        private PanelContainer _panel;
        private float _remaining;
        private int _pollFrames;
        private bool _bound;

        /// <summary>The tooltip currently on screen, or "". Test seam.</summary>
        public string ShowingTooltipID { get; private set; } = "";

        /// <summary>Tooltips queued behind the one showing. Test seam.</summary>
        public int PendingCount => _pending.Count;

        /// <summary>Set by the bootstrapper when the scene carries a Time Freeze controller.</summary>
        public bool SceneHasTimeFreeze { get; set; }

        public static FirstUseTooltipPresenter Attach(Node sceneRoot, bool sceneHasTimeFreeze) {
            if (sceneRoot == null) return null;
            var presenter = new FirstUseTooltipPresenter {
                Name = NodeName,
                SceneHasTimeFreeze = sceneHasTimeFreeze
            };
            sceneRoot.AddChild(presenter);
            return presenter;
        }

        public override void _Ready() {
            Layer = 40;
            ProcessMode = ProcessModeEnum.Pausable;
            _panel = new PanelContainer {
                Name = "TooltipPanel",
                Visible = false,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ThemeTypeVariation = "DialogueGlassPanel",
                Position = new Vector2(560, 150),
                CustomMinimumSize = new Vector2(800, 0)
            };
            UIPalette.ApplyTheme(_panel);
            _label = new Label {
                Name = "TooltipText",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                HorizontalAlignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(760, 0)
            };
            _panel.AddChild(_label);
            AddChild(_panel);

            if (EventBus.Instance != null) {
                EventBus.Instance.OnPlayerHPChanged += OnPlayerHPChanged;
                EventBus.Instance.OnBlockBroken += OnBlockBroken;
                EventBus.Instance.OnBlockAbsorbed += OnBlockAbsorbed;
                EventBus.Instance.OnDefySealChanged += OnDefySealChanged;
                EventBus.Instance.OnRewindTriggered += OnRewindTriggered;
                _bound = true;
            }
            if (SceneHasTimeFreeze) Offer(FirstUseTooltips.TimeFreeze);
        }

        public override void _ExitTree() {
            if (!_bound || EventBus.Instance == null) return;
            EventBus.Instance.OnPlayerHPChanged -= OnPlayerHPChanged;
            EventBus.Instance.OnBlockBroken -= OnBlockBroken;
            EventBus.Instance.OnBlockAbsorbed -= OnBlockAbsorbed;
            EventBus.Instance.OnDefySealChanged -= OnDefySealChanged;
            EventBus.Instance.OnRewindTriggered -= OnRewindTriggered;
            _bound = false;
        }

        /// <summary>
        /// Offers a tooltip: consumed and queued exactly once per slot, ignored
        /// otherwise. Returns true when it was accepted. Public so tests and
        /// scripted beats can raise one directly.
        /// </summary>
        public bool Offer(string tooltipID) {
            StorySaveData save = ActiveSave();
            if (!FirstUseTooltips.TryConsume(save, tooltipID)) return false;
            if (string.IsNullOrEmpty(ShowingTooltipID)) Show(tooltipID);
            else _pending.Enqueue(tooltipID);
            return true;
        }

        private void Show(string tooltipID) {
            ShowingTooltipID = tooltipID;
            _remaining = DisplaySeconds;
            if (_label != null) _label.Text = FirstUseTooltips.KeyFor(tooltipID);
            if (_panel != null) _panel.Visible = true;
        }

        public override void _Process(double delta) {
            if (string.IsNullOrEmpty(ShowingTooltipID)) return;
            _remaining -= (float)delta;
            if (_remaining > 0f) return;
            ShowingTooltipID = "";
            if (_panel != null) _panel.Visible = false;
            if (_pending.Count > 0) Show(_pending.Dequeue());
        }

        public override void _PhysicsProcess(double delta) {
            // The polled triggers only matter on a skipped-calibration slot.
            if (++_pollFrames % 6 != 0) return;
            StorySaveData save = ActiveSave();
            if (save == null || !save.SkippedCalibration) return;
            PlayerController player = FindPlayer();
            if (player == null) return;
            if (player.CurrentState == CharacterState.LedgeHanging) Offer(FirstUseTooltips.Ledge);
            if (player.CurrentState == CharacterState.Stunned && !player.IsOnFloor()) Offer(FirstUseTooltips.Launch);
            if (FirstUseTooltips.ShouldShow(save, FirstUseTooltips.OneWayPlatform) && StandsOnOneWay(player)) {
                Offer(FirstUseTooltips.OneWayPlatform);
            }
        }

        private static bool StandsOnOneWay(PlayerController player) {
            if (!player.IsOnFloor()) return false;
            KinematicCollision2D collision = player.GetLastSlideCollision();
            return collision?.GetCollider() is CollisionObject2D body
                && (body.CollisionLayer & CollisionLayers.OneWayPlatform) != 0;
        }

        private PlayerController FindPlayer() {
            Godot.Collections.Array<Node> players = GetTree()?.GetNodesInGroup("StoryPlayer");
            if (players == null) return null;
            using var lifetime = players.AsDisposable();
            foreach (Node node in players) {
                if (node is PlayerController player && player.PlayerIndex == 0) return player;
            }
            return null;
        }

        private void OnPlayerHPChanged(PlayerHPPayload payload) {
            if (payload.PlayerIndex == 0 && payload.DamageAmount > 0) Offer(FirstUseTooltips.RallyEcho);
        }

        private void OnBlockBroken(int playerIndex) {
            if (playerIndex == 0) Offer(FirstUseTooltips.ShieldBreak);
        }

        private void OnBlockAbsorbed(int playerIndex, int remainingCharges) {
            if (playerIndex == 0) Offer(FirstUseTooltips.Grab);
        }

        private void OnDefySealChanged(DefySealPayload payload) {
            if (payload.PlayerIndex == 0
                && (payload.State == DefySealState.Ready || payload.State == DefySealState.Spent)) {
                Offer(FirstUseTooltips.Defy);
            }
        }

        private void OnRewindTriggered(Vector2 _) => Offer(FirstUseTooltips.DeathRewind);

        private static StorySaveData ActiveSave() {
            if (GameManager.Instance == null || SaveManager.Instance == null) return null;
            int slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            return slot >= 0 && slot < SaveManager.Instance.SaveSlots.Length ? SaveManager.Instance.SaveSlots[slot] : null;
        }
    }
}
