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
    /// first airborne hitstun (a launch), the meter first reaching the Defy cost, the
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

        /// <summary>
        /// UI_SPEC_GLASS 9.2: the plate hangs top-centre at y 140, at most 900 wide, its height the text's. TG-CLOSE: like the
        /// checkpoint toast it keeps to the Story HUD's free band (<see cref="StoryHUD.FreeBand"/>), leaving the centre only
        /// as far as a cluster needs and wrapping at the band's width when the band is narrower: centred, eight of the nine
        /// lines covered the Vitals panel at 1.0 and all nine at 1.4 (the band is then about 508 px wide).
        /// </summary>
        public const float PlateTop = 140f, PlateMaxWidth = 900f;

        /// <summary>
        /// The plate's padding: 12 after the tag's 2 px left bar, 14 plus the 8 px slant on the right (3.0 G2, 3.3 the tag),
        /// and 10 above and below the line.
        /// </summary>
        public const float PadLeft = 14f, PadRight = 22f, PadVertical = 10f;

        /// <summary>The G2 slant: the right end runs 8 px back towards the bottom.</summary>
        public const float SlantRun = 8f;

        /// <summary>The plate's tag wash and its dark base. Test surface.</summary>
        internal PanelContainer Wash => _wash;
        internal Control PlateBase => _base;

        private PanelContainer _wash;
        private StyleBox _washBox;
        private TooltipPlateBase _base;

        public override void _Ready() {
            Layer = 40;
            ProcessMode = ProcessModeEnum.Pausable;
            // TG-PAUSE (UI_SPEC_GLASS 9.2): a glass tag-style plate, the G2 body (the 2 px edge-hi bar, the 90 degree gold
            // wash, the right end slanted 8 px), Outfit 400 22 in ink, fading and rising in. Under the wash lies the glass
            // slab's dark fill in the same shape, so the line stays legible over a bright level (the spec draws the G2 body
            // alone, which is a tag meant to straddle a dark panel).
            _panel = new PanelContainer {
                Name = "TooltipPanel",
                Visible = false,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            // FU-MENU: top-centre anchors growing both ways, not a fixed (560, 150).
            _panel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
            _panel.OffsetLeft = 0f;
            _panel.OffsetRight = 0f;
            _panel.OffsetTop = PlateTop;
            _panel.OffsetBottom = PlateTop;
            _panel.GrowHorizontal = Control.GrowDirection.Both;
            _panel.GrowVertical = Control.GrowDirection.End;
            UIPalette.ApplyTheme(_panel);
            _panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
            _base = new TooltipPlateBase { Name = "TooltipBase", MouseFilter = Control.MouseFilterEnum.Ignore };
            _panel.AddChild(_base);
            _wash = new PanelContainer { Name = "TooltipWash", MouseFilter = Control.MouseFilterEnum.Ignore };
            _washBox = WashBox();
            if (_washBox != null) _wash.AddThemeStyleboxOverride("panel", _washBox);
            _panel.AddChild(_wash);
            _label = new Label {
                Name = "TooltipText",
                ThemeTypeVariation = UIPalette.LabelRoles.BodySmallLabel,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _wash.AddChild(_label);
            AddChild(_panel);

            if (EventBus.Instance != null) {
                EventBus.Instance.OnPlayerHPChanged += OnPlayerHPChanged;
                EventBus.Instance.OnBlockBroken += OnBlockBroken;
                EventBus.Instance.OnBlockAbsorbed += OnBlockAbsorbed;
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
            Present(FirstUseTooltips.KeyFor(tooltipID));
        }

        /// <summary>
        /// Shows <paramref name="text"/> (a key, or a line) through the plate's real fit and appear path, outside the slot's
        /// ledger and timer. Capture and test seam.
        /// </summary>
        internal void PresentForPreview(string text) => Present(text);

        private void Present(string text) {
            if (_label == null || _panel == null) return;
            _label.Text = text;
            FitPlate();
            _panel.Visible = true;
            // MOT-A row 4: fade and rise in; under Reduced Temporal Effects a 150 ms fade (PulpMotion reads the comfort setting).
            _appear?.Kill();
            _appear = _panel.IsInsideTree() ? FTT.UI.Motion.PulpMotion.PopIn(_panel) : null;
        }

        private Tween _appear;

        // What the last FitPlate read; a change while a tooltip shows (a Settings change over the pause, a resize, a locale
        // switch) refits the plate in _Process.
        private float _fitScale, _fitHud, _fitView;
        private bool _fitStale;

        public override void _Notification(int what) {
            if ((long)what == NotificationTranslationChanged) _fitStale = true;
        }

        private void RefitIfStale() {
            if (_panel == null || !_panel.Visible) return;
            float view = GetViewport()?.GetVisibleRect().Size.X ?? 1920f;
            if (view <= 1f) view = 1920f;
            if (!_fitStale
                && Mathf.IsEqualApprox(_fitScale, Pulp.PulpButtonDecor.UiScaleFactor)
                && Mathf.IsEqualApprox(_fitHud, Mathf.Max(0.01f, UiScaleBinder.CurrentSetting))
                && Mathf.IsEqualApprox(_fitView, view)) return;
            // Leave the plate at its final pose first, so the rise cannot write stale offsets after the refit.
            _appear?.Kill();
            _appear = null;
            FTT.UI.Motion.PulpMotion.Settle(_panel);
            _panel.Modulate = new Color(_panel.Modulate, 1f);
            FitPlate();
        }

        /// <summary>
        /// Sizes the plate to its line: the line on one row while it fits inside <see cref="PlateMaxWidth"/> (or the HUD's free
        /// band, when narrower) less the padding, else wrapped at that width (9.2: "height to content, max 900 wide"). Every
        /// metric grows with the UI scale. The plate is placed on its anchor each time, because the rise of the previous
        /// showing wrote its offsets: centred on the screen, moved into the free band only as far as needed.
        /// </summary>
        private void FitPlate() {
            float s = Pulp.PulpButtonDecor.UiScaleFactor;
            float view = GetViewport()?.GetVisibleRect().Size.X ?? 1920f;
            if (view <= 1f) view = 1920f;
            // The band in the HUD's canvas (the HUD is magnified by its own scale setting), on screen.
            float hud = Mathf.Max(0.01f, UiScaleBinder.CurrentSetting);
            _fitScale = s;
            _fitHud = hud;
            _fitView = view;
            _fitStale = false;
            (float bandLeft, float bandRight) = StoryHUD.FreeBand(view / hud);
            bandLeft *= hud;
            bandRight *= hud;
            float maxWidth = Mathf.Min(PlateMaxWidth * s, Mathf.Max(0f, bandRight - bandLeft));
            if (_washBox != null) {
                _washBox.ContentMarginLeft = PadLeft * s;
                _washBox.ContentMarginRight = PadRight * s;
                _washBox.ContentMarginTop = PadVertical * s;
                _washBox.ContentMarginBottom = PadVertical * s;
            }
            float room = Mathf.Max(1f, maxWidth - (PadLeft + PadRight) * s);
            _label.AutowrapMode = TextServer.AutowrapMode.Off;
            _label.CustomMinimumSize = Vector2.Zero;
            if (_label.GetCombinedMinimumSize().X > room) {
                _label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                _label.CustomMinimumSize = new Vector2(room, 0f);
                // Wrap at the final width now, so the plate's height is right before the rise records its position.
                _label.Size = new Vector2(room, 0f);
            }
            float width = _panel.GetCombinedMinimumSize().X;
            float left = Mathf.Max(bandLeft, Mathf.Min(view / 2f - width / 2f, bandRight - width));
            // The plate is anchored at the screen's centre line (anchors 0.5), so its offsets are measured from it.
            _panel.OffsetLeft = left - view / 2f;
            _panel.OffsetRight = left - view / 2f + width;
            _panel.OffsetTop = PlateTop * s;
            _panel.OffsetBottom = PlateTop * s;
        }

        /// <summary>The glass tag's own nine-slice (the theme's <c>PanelTagPlate</c>), a copy the plate pads for itself.</summary>
        private static StyleBox WashBox() {
            Theme theme = UIPalette.LoadTheme();
            if (theme == null || !theme.HasStylebox("panel", UIPalette.Variations.PanelTagPlate)) return null;
            return theme.GetStylebox("panel", UIPalette.Variations.PanelTagPlate).Duplicate() as StyleBox;
        }

        /// <summary>
        /// The dark base under the tag wash: the glass slab's fill in the G2 shape (the right end slanted <see cref="SlantRun"/>
        /// px back towards the bottom). The four corners are rewritten only when the plate is resized; the draw allocates nothing.
        /// </summary>
        private sealed partial class TooltipPlateBase : Control {
            private readonly Vector2[] _corners = new Vector2[4];

            public override void _Notification(int what) {
                if ((long)what != NotificationResized) return;
                Vector2 size = Size;
                _corners[0] = Vector2.Zero;
                _corners[1] = new Vector2(size.X, 0f);
                _corners[2] = new Vector2(Mathf.Max(0f, size.X - SlantRun), size.Y);
                _corners[3] = new Vector2(0f, size.Y);
                QueueRedraw();
            }

            public override void _Draw() {
                if (Size.X <= SlantRun || Size.Y <= 0f) return;
                DrawColoredPolygon(_corners, UIPalette.GlassFillMid);
            }
        }

        public override void _Process(double delta) {
            RefitIfStale();   // only while the plate is visible (a showing tooltip, or a preview)
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
            if (FirstUseTooltips.DefyButtonDue(player.CurrentUltimateMeter)) Offer(FirstUseTooltips.DefyButton);
            // GR-SCHED: Special 1 is restored by Level 0 and first played on Level 1.
            if (FirstUseTooltips.SpecialMomentumDue(
                    (int)(StoryManager.Instance?.CurrentLevel ?? CampaignLevel.Tutorial),
                    player.IsAbilityUnlocked(AbilitySlot.Special1))) {
                Offer(FirstUseTooltips.SpecialMomentum);
            }
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

        private void OnRewindTriggered(Vector2 _) => Offer(FirstUseTooltips.DeathRewind);

        private static StorySaveData ActiveSave() {
            if (GameManager.Instance == null || SaveManager.Instance == null) return null;
            int slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            return slot >= 0 && slot < SaveManager.Instance.SaveSlots.Length ? SaveManager.Instance.SaveSlots[slot] : null;
        }
    }
}
