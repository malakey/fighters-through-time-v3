using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Environment;
using Godot;

namespace FTT.UI {

    /// <summary>The character-select flow states (audit M-28).</summary>
    public enum SelectScreenPhase {
        /// <summary>Players move selection tokens on the roster grid.</summary>
        Selection,
        /// <summary>All tokens locked; the designed 3.0 s cancelable countdown runs.</summary>
        Countdown,
        /// <summary>Stage catalog + match rules (Local Versus) or the CPU configuration panel (Versus CPU).</summary>
        StageSelect
    }

    /// <summary>
    /// Package 8 B4 authored screen, reworked for audit M-28: the designed
    /// select-screen mechanics from design-godot.md:2586-2599.
    ///
    /// <para><b>Two flows (Package 12 W5, H05 per adopted D4(a)).</b> The screen
    /// reads <see cref="SessionData.FighterMatchOrigin"/>:
    /// <list type="bullet">
    /// <item><b>Local Versus</b> — two human tokens, READY, the 3.0 s countdown,
    /// then the stage-and-rules phase. The old CPU toggle is gone: Versus CPU is
    /// its own entry on the Fighter menu.</item>
    /// <item><b>Versus CPU</b> — a <b>P1-only</b> select. Locking Player 1's token
    /// opens <see cref="HolodeckConsolePanel"/> as the front-end CPU configuration
    /// screen (CPU difficulty, CPU character, stage, rules), which launches the
    /// match; backing out of it unlocks Player 1.</item>
    /// </list></para>
    ///
    /// <para><b>Two selection tokens, per-device.</b> Each human player's token is
    /// driven only by that player's assigned device, polled through
    /// <see cref="InputManager.GetFrame(int)"/> — never through global focus
    /// navigation, which any device can steer. Movement uses the gameplay move
    /// axis plus Jump/Down; BasicAttack (or Interact) confirms; Block (or Roll)
    /// cancels. The roster tiles stay clickable buttons for the mouse (clicks act
    /// as Player 1: first click hovers, second confirms) but are deliberately
    /// excluded from the focus chain so a pad in Player 2's hands cannot move
    /// Player 1's token.</para>
    ///
    /// <para><b>Reserved/Occupied.</b> A hovered tile is painted with the hovering
    /// token's slot colour (Reserved); a confirmed tile is locked with a thick
    /// border and glow (Occupied). V7.3 ruling #17: mirror matches are allowed.
    /// G12: each tile also carries the tokens' slot <b>shapes</b> (P1 ▲ / P2 ●),
    /// so a cursor never reads by colour alone, and the colours come from the
    /// local player-slot palette.</para>
    ///
    /// <para><b>Random (G15a).</b> The grid ends in a Random tile with a silhouette
    /// placeholder, and the stage list ends in a Random stage. Both resolve from
    /// the per-match seed (<see cref="SessionData.PendingMatchSeed"/>), which this
    /// screen rolls once and the match then runs on.</para>
    /// </summary>
    public partial class CharacterSelectScreen : Control {

        private const string SelectRoot = "SelectPhase/Root/";
        private const string StageRoot = "StagePhase/Root/";
        private const int GridColumns = 3;
        private const float CountdownSeconds = 3.0f;
        private const float AxisThreshold = 0.5f;

        /// <summary>OptionButton id of the Random stage entry.</summary>
        public const int RandomStageItemID = 1000;

        private sealed class SelectionToken {
            public int Cursor;
            public int LockedIndex = -1;
            public bool Ready => LockedIndex >= 0;
        }

        /// <summary>
        /// The Fighter select tiles, in manifest order. Package 11 A6b: read
        /// from <see cref="FTT.Core.CharacterRoster"/> rather than a literal
        /// nine-element array, so a new manifest row appears here with no code
        /// change (design §2's standing "the roster will grow" mandate).
        /// </summary>
        private readonly string[] _characterIDs = FTT.Core.CharacterRoster.ToArray();

        /// <summary>Token 0 = Player 1; token 1 = Player 2 (Local Versus only).</summary>
        private readonly SelectionToken[] _tokens = { new(), new() { Cursor = 1 } };
        private readonly float[] _previousHorizontal = new float[2];

        private SelectScreenPhase _phase = SelectScreenPhase.Selection;
        private float _countdownRemaining;
        private FighterMatchOrigin _origin = FighterMatchOrigin.LocalVersus;

        private Button[] _characterButtons;
        private Label[] _tileBadges;
        private Control _selectPhase;
        private Control _stagePhase;
        private Label _p1Name;
        private Label _p1Ready;
        private Label _p1Stats;
        private Label _p2Name;
        private Label _p2Ready;
        private Label _p2Stats;
        private Label _countdownLabel;
        private SpinBox _stockCount;
        private SpinBox _timeLimit;
        private OptionButton _itemFrequency;
        private CheckButton _meterPickups;
        private CheckButton _stageHazards;
        private OptionButton _matchMode;
        private OptionButton _stageSelect;
        private TextureRect _stagePreview;
        private FighterStageCatalog _stageCatalog;
        private readonly List<string> _stageIDs = new();
        private MoveListScreen _moveList;
        private SystemsCardScreen _systemsCard;
        private HolodeckConsolePanel _cpuConfigPanel;

        /// <summary>The current flow state.</summary>
        public SelectScreenPhase Phase => _phase;

        /// <summary>Seconds left on the ready countdown (valid in Countdown phase).</summary>
        public float CountdownRemaining => _countdownRemaining;

        /// <summary>The tile index currently chosen for player one (lock wins over hover).</summary>
        public int SelectedIndex => EffectiveIndex(_tokens[0]);

        /// <summary>The tile index currently chosen for the opponent (lock wins over hover).</summary>
        public int OpponentIndex => EffectiveIndex(_tokens[1]);

        /// <summary>Whether the given token (0 = P1, 1 = P2) has confirmed its pick.</summary>
        public bool IsSlotReady(int token) => _tokens[token].Ready;

        /// <summary>The given token's hover cursor.</summary>
        public int GetCursor(int token) => _tokens[token].Cursor;

        /// <summary>True in the Versus CPU flow: a P1-only select feeding the CPU configuration panel.</summary>
        public bool IsVersusCpuFlow => _origin == FighterMatchOrigin.VersusCpu;

        /// <summary>The grid index of the Random tile (one past the roster).</summary>
        public int RandomTileIndex => _characterIDs.Length;

        /// <summary>The Versus CPU configuration panel while it is open, else null. Test seam.</summary>
        public HolodeckConsolePanel CpuConfigPanel =>
            _cpuConfigPanel != null && IsInstanceValid(_cpuConfigPanel) ? _cpuConfigPanel : null;

        /// <summary>Test seam: a fixed per-match seed instead of a freshly rolled one.</summary>
        internal int? SeedOverride { get; set; }

        private int TileCount => _characterIDs.Length + 1;

        public override void _Ready() {
            UIPalette.ApplyTheme(this);
            // V7 "Match Settings Persist": the last-used rules pre-load from the
            // global save before the rule controls bind their initial values.
            GameManager.Instance?.EnsureMatchSettingsLoaded();
            _origin = GameManager.Instance?.CurrentSession.FighterMatchOrigin ?? FighterMatchOrigin.LocalVersus;

            _selectPhase = GetNode<Control>("SelectPhase");
            _stagePhase = GetNode<Control>("StagePhase");
            _p1Name = GetNode<Label>(SelectRoot + "PlayersRow/P1Panel/P1Name");
            _p1Ready = GetNode<Label>(SelectRoot + "PlayersRow/P1Panel/P1Ready");
            _p1Stats = GetNode<Label>(SelectRoot + "PlayersRow/P1Panel/P1Stats");
            _p2Name = GetNode<Label>(SelectRoot + "PlayersRow/P2Panel/P2Name");
            _p2Ready = GetNode<Label>(SelectRoot + "PlayersRow/P2Panel/P2Ready");
            _p2Stats = GetNode<Label>(SelectRoot + "PlayersRow/P2Panel/P2Stats");
            _countdownLabel = GetNode<Label>(SelectRoot + "CountdownLabel");
            _stageSelect = GetNode<OptionButton>(StageRoot + "StageRow/StageSelect");
            _stagePreview = GetNode<TextureRect>(StageRoot + "StageRow/StagePreview");
            _matchMode = GetNode<OptionButton>(StageRoot + "RulesRow/MatchMode");
            _stockCount = GetNode<SpinBox>(StageRoot + "RulesRow/StockCount");
            _timeLimit = GetNode<SpinBox>(StageRoot + "RulesRow/TimeLimit");
            _itemFrequency = GetNode<OptionButton>(StageRoot + "RulesRow/ItemFrequency");
            _meterPickups = GetNode<CheckButton>(StageRoot + "RulesRow/MeterPickups");
            _stageHazards = GetNode<CheckButton>(StageRoot + "RulesRow/StageHazards");

            BindCharacterGrid();
            BindRules();
            ApplySlotColours();

            GetNode<Button>(SelectRoot + "ButtonRow/BackButton").Pressed += OnBack;
            GetNode<Button>(StageRoot + "StageButtonRow/StageBackButton").Pressed += ReturnToSelection;
            GetNode<Button>(StageRoot + "StageButtonRow/FightButton").Pressed += OnFight;

            // V7.3 Fighter Onboarding: the footer's Move List opens for the tile
            // Player 1's token currently points at (lock wins over hover — the
            // roster tiles are cursor-driven, not focus-driven, so the footer
            // button is the tile's "focused" Move List action); the Systems Card
            // is the universal reference page.
            GetNode<Button>(SelectRoot + "ButtonRow/MoveListButton").Pressed += OpenMoveList;
            GetNode<Button>(SelectRoot + "ButtonRow/SystemsCardButton").Pressed += OpenSystemsCard;

            RefreshPanels();
            RepaintTiles();
            BuildFocusChain();

            // Post-match "New Stage" (V7): same characters, straight back to the
            // stage phase — which for Versus CPU is the CPU configuration panel.
            // The flag is consumed on read so Back still works.
            if (GameManager.Instance != null && GameManager.Instance.CurrentSession.ResumeAtStageSelect) {
                SessionData session = GameManager.Instance.CurrentSession;
                session.ResumeAtStageSelect = false;
                GameManager.Instance.CurrentSession = session;
                int p1 = Array.IndexOf(_characterIDs, session.SelectedCharacterID);
                int p2 = Array.IndexOf(_characterIDs, session.OpponentCharacterID);
                if (p1 >= 0 && (IsVersusCpuFlow || p2 >= 0)) {
                    _tokens[0].LockedIndex = p1;
                    if (!IsVersusCpuFlow) _tokens[1].LockedIndex = p2;
                    RefreshPanels();
                    RepaintTiles();
                    EnterStagePhase();
                }
            }
        }

        // === Flow: polled per-player input ===================================

        /// <summary>
        /// Per-player token input, polled at 60 Hz. Each player's frame comes from
        /// that player's assigned device only (<see cref="InputManager"/> owns the
        /// device→player mapping), which is what keeps Player 2's token off
        /// Player 1's keyboard and vice versa. The stage phase is focus-driven
        /// authored UI, so token polling stops there.
        /// </summary>
        public override void _PhysicsProcess(double delta) {
            if (InputManager.Instance == null) return;
            if (_phase == SelectScreenPhase.StageSelect) return;
            // An open onboarding overlay owns the player; the polled cursors
            // must not keep steering tokens underneath it.
            if (_moveList?.Visible == true || _systemsCard?.Visible == true) return;
            int players = Math.Min(2, InputManager.Instance.MaxPlayers);
            for (int playerIndex = 0; playerIndex < players; playerIndex++) {
                PlayerInputFrame frame = InputManager.Instance.GetFrame(playerIndex);
                HandleFrame(playerIndex, frame);
            }
        }

        private void HandleFrame(int playerIndex, in PlayerInputFrame frame) {
            float previousHorizontal = _previousHorizontal[playerIndex];
            _previousHorizontal[playerIndex] = frame.Horizontal;

            if (frame.IsPressed(GameplayButtons.Block) || frame.IsPressed(GameplayButtons.Roll)) {
                TryCancel(playerIndex);
                return;
            }
            if (_phase != SelectScreenPhase.Selection) return;

            if (frame.IsPressed(GameplayButtons.BasicAttack) || frame.IsPressed(GameplayButtons.Interact)) {
                TryConfirm(playerIndex);
                return;
            }

            int dx = AxisStep(previousHorizontal, frame.Horizontal);
            int dy = 0;
            if (frame.IsPressed(GameplayButtons.Down)) dy = 1;
            else if (frame.IsPressed(GameplayButtons.Jump)) dy = -1;
            if (dx != 0 || dy != 0) TryMoveCursor(playerIndex, dx, dy);
        }

        /// <summary>One grid step when the axis crosses the threshold from neutral.</summary>
        private static int AxisStep(float previous, float current) {
            if (MathF.Abs(current) < AxisThreshold || MathF.Abs(previous) >= AxisThreshold) return 0;
            return current > 0f ? 1 : -1;
        }

        // === Flow: token mechanics ===========================================

        /// <summary>
        /// Which token the given player drives: in Local Versus each player drives
        /// their own; in Versus CPU only Player 1 drives a token. -1 when the
        /// player drives none.
        /// </summary>
        private int TokenDrivenBy(int playerIndex) {
            if (IsVersusCpuFlow) return playerIndex == 0 ? 0 : -1;
            return playerIndex is 0 or 1 ? playerIndex : -1;
        }

        /// <summary>Which token this player's cancel unlocks, or -1.</summary>
        private int TokenToUnlockFor(int playerIndex) {
            int token = TokenDrivenBy(playerIndex);
            if (token < 0) return -1;
            return _tokens[token].Ready ? token : -1;
        }

        /// <summary>
        /// Moves the token the player drives by one grid step with wraparound.
        /// Locked tokens do not move — cancel first. The Random tile is the last
        /// grid cell.
        /// </summary>
        public bool TryMoveCursor(int playerIndex, int dx, int dy) {
            if (_phase != SelectScreenPhase.Selection) return false;
            int tokenIndex = TokenDrivenBy(playerIndex);
            if (tokenIndex < 0) return false;
            SelectionToken token = _tokens[tokenIndex];
            if (token.Ready) return false;

            int rows = (TileCount + GridColumns - 1) / GridColumns;
            int column = ((token.Cursor % GridColumns) + dx + GridColumns) % GridColumns;
            int row = ((token.Cursor / GridColumns) + dy + rows) % rows;
            token.Cursor = Math.Min(row * GridColumns + column, TileCount - 1);
            RefreshPanels();
            RepaintTiles();
            return true;
        }

        /// <summary>
        /// Confirms the driven token on its hovered tile. V7.3 ruling #17: mirror
        /// matches are allowed. Local Versus: locking the last token starts the
        /// countdown. Versus CPU: locking Player 1 opens the CPU configuration panel.
        /// </summary>
        public bool TryConfirm(int playerIndex) {
            if (_phase != SelectScreenPhase.Selection) return false;
            int tokenIndex = TokenDrivenBy(playerIndex);
            if (tokenIndex < 0) return false;
            SelectionToken token = _tokens[tokenIndex];
            if (token.Ready) return false;

            token.LockedIndex = token.Cursor;
            // Selection-confirmed cue (character vocal SFX is Package 10 content;
            // the placeholder UI cue is the wiring).
            AudioManager.Instance?.PlayUISound();
            RefreshPanels();
            RepaintTiles();
            if (IsVersusCpuFlow) {
                EnterStagePhase();
            } else {
                TryStartCountdown();
            }
            return true;
        }

        /// <summary>
        /// Cancels for the given player: during the countdown any ready player's
        /// cancel aborts the timer and unlocks their token, returning to selection;
        /// during selection it unlocks that player's lock. Returns false when
        /// there was nothing to unwind (the caller may then leave the screen).
        /// </summary>
        public bool TryCancel(int playerIndex) {
            if (_phase == SelectScreenPhase.Countdown) {
                int abortToken = TokenToUnlockFor(playerIndex);
                if (abortToken < 0) return false;
                _tokens[abortToken].LockedIndex = -1;
                _phase = SelectScreenPhase.Selection;
                _countdownLabel.Visible = false;
                RefreshPanels();
                RepaintTiles();
                return true;
            }
            if (_phase != SelectScreenPhase.Selection) return false;
            int unlockToken = TokenToUnlockFor(playerIndex);
            if (unlockToken < 0) return false;
            _tokens[unlockToken].LockedIndex = -1;
            RefreshPanels();
            RepaintTiles();
            return true;
        }

        private void TryStartCountdown() {
            if (!_tokens[0].Ready || !_tokens[1].Ready) return;
            _phase = SelectScreenPhase.Countdown;
            _countdownRemaining = CountdownSeconds;
            _countdownLabel.Text = string.Format(Tr("fighter_select_countdown"), _countdownRemaining);
            _countdownLabel.Visible = true;
        }

        /// <summary>
        /// Ticks the ready countdown. Called from <c>_Process</c>; public so tests
        /// can drive the 3.0 s window deterministically.
        /// </summary>
        public void AdvanceCountdown(float seconds) {
            if (_phase != SelectScreenPhase.Countdown) return;
            int previousWhole = Mathf.CeilToInt(_countdownRemaining);
            _countdownRemaining -= seconds;
            if (_countdownRemaining <= 0f) {
                _countdownRemaining = 0f;
                EnterStagePhase();
                return;
            }
            if (Mathf.CeilToInt(_countdownRemaining) < previousWhole) {
                AudioManager.Instance?.PlayCountdownBlip();
            }
            _countdownLabel.Text = string.Format(Tr("fighter_select_countdown"), _countdownRemaining);
        }

        public override void _Process(double delta) {
            if (_phase == SelectScreenPhase.Countdown) AdvanceCountdown((float)delta);
        }

        // === Flow: stage phase ===============================================

        private void EnterStagePhase() {
            _phase = SelectScreenPhase.StageSelect;
            _countdownLabel.Visible = false;
            CommitCharactersToSession();
            if (IsVersusCpuFlow) {
                OpenCpuConfigPanel();
                return;
            }
            _selectPhase.Visible = false;
            _stagePhase.Visible = true;
            UpdateStagePreview();
            BuildFocusChain();
        }

        /// <summary>
        /// Versus CPU's "stage phase": the Holodeck console panel as a front-end
        /// screen. Its Back unlocks Player 1; its Launch starts the match.
        /// </summary>
        private void OpenCpuConfigPanel() {
            _cpuConfigPanel = new HolodeckConsolePanel {
                Name = "CpuConfigPanel",
                FrontEnd = true,
                SeedOverride = SeedOverride
            };
            _cpuConfigPanel.Closed += ReturnToSelection;
            AddChild(_cpuConfigPanel);
        }

        /// <summary>
        /// Backs out of stage select (or the CPU configuration panel). Everyone
        /// returns to selection unreadied (cursors keep their picks) so a re-confirm
        /// restarts the countdown rather than instantly re-entering the stage phase.
        /// </summary>
        public void ReturnToSelection() {
            if (_phase != SelectScreenPhase.StageSelect) return;
            foreach (SelectionToken token in _tokens) token.LockedIndex = -1;
            _phase = SelectScreenPhase.Selection;
            if (_cpuConfigPanel != null && IsInstanceValid(_cpuConfigPanel)) _cpuConfigPanel.QueueFree();
            _cpuConfigPanel = null;
            _stagePhase.Visible = false;
            _selectPhase.Visible = true;
            RefreshPanels();
            RepaintTiles();
            BuildFocusChain();
        }

        // === Presentation ====================================================

        /// <summary>
        /// The authored focus order for the active phase, exposed so a test can
        /// walk it. The roster tiles are deliberately absent: they are driven by
        /// the per-player polled cursors (and the mouse), never by focus, so one
        /// player's pad cannot steer the other player's token.
        /// </summary>
        public IReadOnlyList<Control> FocusChain => _phase == SelectScreenPhase.StageSelect
            ? new List<Control> {
                _stageSelect,
                _matchMode,
                _stockCount.GetLineEdit(),
                _timeLimit.GetLineEdit(),
                _itemFrequency,
                _meterPickups,
                _stageHazards,
                GetNode<Button>(StageRoot + "StageButtonRow/StageBackButton"),
                GetNode<Button>(StageRoot + "StageButtonRow/FightButton")
            }
            : new List<Control> {
                // M01 (Package 11): the Steam Remote Play Together notice is the
                // launch netplay message. It is a Label rather than a button, but
                // it is focusable and in the chain so a controller-only player can
                // reach and read it without a mouse.
                GetNode<Label>(SelectRoot + "RemotePlayNotice"),
                GetNode<Button>(SelectRoot + "ButtonRow/MoveListButton"),
                GetNode<Button>(SelectRoot + "ButtonRow/SystemsCardButton"),
                GetNode<Button>(SelectRoot + "ButtonRow/BackButton")
            };

        /// <summary>Rebuilt on every phase change, per the focus-authoring convention.</summary>
        private void BuildFocusChain() {
            IReadOnlyList<Control> chain = FocusChain;
            FocusChainBuilder.Chain(chain);
            chain[0]?.GrabFocus();
        }

        private void BindCharacterGrid() {
            var grid = GetNode<GridContainer>(SelectRoot + "PlayersRow/Grid");
            _characterButtons = new Button[TileCount];
            _tileBadges = new Label[TileCount];
            for (int index = 0; index < TileCount; index++) {
                int captured = index;
                bool random = index == RandomTileIndex;
                // Package 11 A6b: the roster is manifest-driven while the grid's
                // tiles are authored, so bind what the scene actually carries and
                // let a larger roster degrade to the authored tile count rather
                // than throwing out of _Ready. Adding tiles is scene work.
                var button = grid.GetNodeOrNull<Button>(random ? "RandomTile" : $"CharacterButton{index}");
                if (button == null) continue;
                // Button icons share a horizontal row with their text, so long
                // names such as "Wolfgang Amadeus Mozart" can squeeze the icon
                // down to a sliver. Keep the full name and give every portrait a
                // dedicated layer above it instead.
                button.Text = string.Empty;
                if (random) {
                    AddRandomSilhouette(button);
                    AddPortraitLayer(button, null, Tr("fighter_random_character"));
                } else {
                    AddPortraitLayer(button, GetCharacterPortrait(index), GetCharacterName(index));
                }
                _tileBadges[index] = AddTokenBadge(button);
                // Cursor-driven, not focus-driven: see FocusChain.
                button.FocusMode = FocusModeEnum.None;
                button.AddToGroup(FocusChainBuilder.SkipGroup);
                button.Pressed += () => OnTilePressed(captured);
                _characterButtons[index] = button;
            }
        }

        /// <summary>
        /// G15a's placeholder silhouette: a dark figure-shaped block with a "?"
        /// over it, until Package 10 supplies Random tile art.
        /// </summary>
        private static void AddRandomSilhouette(Button button) {
            var silhouette = new ColorRect {
                Name = "Silhouette",
                Color = new Color(0.05f, 0.06f, 0.12f, 0.9f),
                MouseFilter = MouseFilterEnum.Ignore,
                AnchorLeft = 0.5f,
                AnchorRight = 0.5f,
                OffsetLeft = -24f,
                OffsetTop = 8f,
                OffsetRight = 24f,
                OffsetBottom = 74f
            };
            button.AddChild(silhouette);
            var mark = new Label {
                Name = "RandomMark",
                Text = "?",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore
            };
            mark.SetAnchorsPreset(LayoutPreset.FullRect);
            mark.ThemeTypeVariation = UIPalette.TitleLabelVariation;
            silhouette.AddChild(mark);
        }

        /// <summary>
        /// G12: the corner badge that carries the hovering/locking tokens' slot
        /// shapes (▲ / ●), so a cursor never reads by colour alone.
        /// </summary>
        private static Label AddTokenBadge(Button button) {
            var badge = new Label {
                Name = "TokenBadge",
                Text = "",
                HorizontalAlignment = HorizontalAlignment.Right,
                MouseFilter = MouseFilterEnum.Ignore,
                AnchorLeft = 1f,
                AnchorRight = 1f,
                OffsetLeft = -52f,
                OffsetTop = 2f,
                OffsetRight = -6f,
                OffsetBottom = 26f
            };
            badge.ThemeTypeVariation = UIPalette.HeadingLabelVariation;
            badge.AddThemeConstantOverride("outline_size", 4);
            button.AddChild(badge);
            return badge;
        }

        private static void AddPortraitLayer(Button button, Texture2D portrait, string characterName) {
            var portraitRect = new TextureRect {
                Name = "Portrait",
                Texture = portrait,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore,
                AnchorLeft = 0.5f,
                AnchorRight = 0.5f,
                OffsetLeft = -36f,
                OffsetTop = 5f,
                OffsetRight = 36f,
                OffsetBottom = 77f
            };
            button.AddChild(portraitRect);

            var nameLabel = new Label {
                Name = "CharacterName",
                Text = characterName,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = MouseFilterEnum.Ignore,
                AnchorRight = 1f,
                AnchorTop = 1f,
                AnchorBottom = 1f,
                OffsetLeft = 4f,
                OffsetTop = -42f,
                OffsetRight = -4f,
                OffsetBottom = -3f
            };
            // V7.3 UI-scale pass: the theme's SmallLabel role replaces the old
            // 16 px override so the tile names follow the accessibility scale.
            nameLabel.ThemeTypeVariation = UIPalette.SmallLabelVariation;
            button.AddChild(nameLabel);
        }

        /// <summary>
        /// Mouse path, acting as Player 1: a click on a new tile moves the driven
        /// token there (hover/Reserved); a click on the hovered tile confirms it —
        /// the same hover-then-confirm sequencing the polled cursors use.
        /// </summary>
        private void OnTilePressed(int index) {
            if (_phase != SelectScreenPhase.Selection) return;
            int tokenIndex = TokenDrivenBy(0);
            if (tokenIndex < 0 || _tokens[tokenIndex].Ready) return;
            if (_tokens[tokenIndex].Cursor != index) {
                _tokens[tokenIndex].Cursor = index;
                RefreshPanels();
                RepaintTiles();
                return;
            }
            TryConfirm(0);
        }

        private void RepaintTiles() {
            if (_characterButtons == null) return;
            for (int index = 0; index < _characterButtons.Length; index++) StyleTile(index);
        }

        /// <summary>The badge glyphs on a tile: ▲ for P1, ● for P2, both when shared. Test seam.</summary>
        public string TileBadgeText(int index) =>
            _tileBadges != null && index >= 0 && index < _tileBadges.Length && _tileBadges[index] != null
                ? _tileBadges[index].Text
                : "";

        /// <summary>
        /// Paints one tile from the token states. Occupied (locked) tiles carry a
        /// thick border and glow in the owning token's slot colour; Reserved
        /// (hovered) tiles a thinner border; overlapping hovers blend the two
        /// colours, and (V7.3, mirror matches) a tile BOTH tokens have locked
        /// blends the two lock colours the same way. The theme's focus stylebox is
        /// never overridden.
        /// </summary>
        private void StyleTile(int index) {
            Button button = _characterButtons[index];
            // A manifest roster larger than the authored tile grid leaves the
            // tail unbound (see BindCharacterGrid); styling must skip it.
            if (button == null) return;

            Color color = index == RandomTileIndex
                ? UIPalette.Slate
                : CharacterFactory.GetCharacterColor(_characterIDs[index]);
            Color playerOne = PlayerSlotPalettes.ActiveSlotColor(0);
            Color playerTwo = PlayerSlotPalettes.ActiveSlotColor(1);

            Color borderColor = new(0.2f, 0.2f, 0.25f);
            int borderWidth = 2;
            bool occupied = false;
            bool p1Here = _tokens[0].LockedIndex == index || (!_tokens[0].Ready && _tokens[0].Cursor == index);
            bool p2Here = OpponentCursorActive
                && (_tokens[1].LockedIndex == index || (!_tokens[1].Ready && _tokens[1].Cursor == index));

            if (_tokens[0].LockedIndex == index && _tokens[1].LockedIndex == index) {
                borderColor = playerOne.Lerp(playerTwo, 0.5f);
                borderWidth = 5;
                occupied = true;
            } else if (_tokens[0].LockedIndex == index) {
                borderColor = playerOne;
                borderWidth = 5;
                occupied = true;
            } else if (_tokens[1].LockedIndex == index && OpponentCursorActive) {
                borderColor = playerTwo;
                borderWidth = 5;
                occupied = true;
            } else {
                bool p1Hover = !_tokens[0].Ready && _tokens[0].Cursor == index;
                bool opponentHover = OpponentCursorActive && !_tokens[1].Ready && _tokens[1].Cursor == index;
                if (p1Hover && opponentHover) {
                    borderColor = playerOne.Lerp(playerTwo, 0.5f);
                    borderWidth = 4;
                } else if (p1Hover) {
                    borderColor = playerOne;
                    borderWidth = 4;
                } else if (opponentHover) {
                    borderColor = playerTwo;
                    borderWidth = 4;
                }
            }

            Label badge = _tileBadges[index];
            if (badge != null) {
                string text = (p1Here ? PlayerSlotPalettes.PlayerOneGlyph : "")
                    + (p2Here ? PlayerSlotPalettes.PlayerTwoGlyph : "");
                badge.Text = text;
                Color badgeColor = p1Here && !p2Here ? playerOne : p2Here && !p1Here ? playerTwo : borderColor;
                badge.AddThemeColorOverride("font_color", badgeColor);
                badge.AddThemeColorOverride("font_outline_color",
                    PlayerSlotPalettes.ActiveSlotEdgeColor(p2Here && !p1Here ? 1 : 0));
            }

            Color cardColor = color.Lerp(UIPalette.Navy, 0.62f);
            var style = new StyleBoxFlat {
                // Keep each fighter's identity colour, but sink it into the
                // midnight glass palette so portraits read as layered time cards
                // instead of opaque placeholder blocks.
                BgColor = new Color(cardColor, 0.86f),
                BorderColor = borderColor,
                ShadowSize = occupied ? 8 : 0,
                ShadowColor = occupied ? new Color(borderColor, 0.5f) : Colors.Transparent
            };
            style.SetBorderWidthAll(borderWidth);
            style.SetCornerRadiusAll(6);
            style.SetContentMarginAll(8);

            button.AddThemeStyleboxOverride("normal", style);
            button.AddThemeStyleboxOverride("hover", style);
            button.AddThemeStyleboxOverride("pressed", style);

            // Readable text on top of an arbitrary character colour.
            float luminance =
                0.299f * cardColor.R + 0.587f * cardColor.G + 0.114f * cardColor.B;
            Color textColor = luminance > 0.55f
                ? new Color(0.1f, 0.1f, 0.15f)
                : new Color(0.95f, 0.95f, 0.98f);
            button.AddThemeColorOverride("font_color", textColor);
            button.AddThemeColorOverride("font_hover_color", textColor);
            button.AddThemeColorOverride("font_pressed_color", textColor);
            button.AddThemeColorOverride("font_focus_color", textColor);
        }

        /// <summary>Player 2's token only exists in Local Versus.</summary>
        private bool OpponentCursorActive => !IsVersusCpuFlow;

        /// <summary>G12: the two player panels' name tags wear the local slot palette.</summary>
        private void ApplySlotColours() {
            _p1Name?.AddThemeColorOverride("font_color", PlayerSlotPalettes.ActiveSlotColor(0));
            _p1Name?.AddThemeColorOverride("font_outline_color", PlayerSlotPalettes.ActiveSlotEdgeColor(0));
            _p1Name?.AddThemeConstantOverride("outline_size", 3);
            _p2Name?.AddThemeColorOverride("font_color", PlayerSlotPalettes.ActiveSlotColor(1));
            _p2Name?.AddThemeColorOverride("font_outline_color", PlayerSlotPalettes.ActiveSlotEdgeColor(1));
            _p2Name?.AddThemeConstantOverride("outline_size", 3);
        }

        private void RefreshPanels() {
            if (_p1Name == null) return;

            int p1Index = EffectiveIndex(_tokens[0]);
            _p1Name.Text = $"{PlayerSlotPalettes.PlayerOneGlyph} "
                + string.Format(Tr("fighter_player_selection"), 1, TileName(p1Index));
            _p1Ready.Visible = _tokens[0].Ready;
            _p1Stats.Text = TileStats(p1Index);

            if (IsVersusCpuFlow) {
                // The CPU is configured on the next screen, not picked here.
                _p2Name.Text = $"{PlayerSlotPalettes.PlayerTwoGlyph} {Tr("fighter_versus_cpu_title")}";
                _p2Ready.Visible = false;
                _p2Stats.Text = Tr("fighter_cpu_configured_next");
                return;
            }
            int p2Index = EffectiveIndex(_tokens[1]);
            _p2Name.Text = $"{PlayerSlotPalettes.PlayerTwoGlyph} "
                + string.Format(Tr("fighter_player_selection"), 2, TileName(p2Index));
            _p2Ready.Visible = _tokens[1].Ready;
            _p2Stats.Text = TileStats(p2Index);
        }

        private string TileName(int index) =>
            index == RandomTileIndex ? Tr("fighter_random_character") : GetCharacterName(index);

        private string TileStats(int index) =>
            index == RandomTileIndex ? Tr("fighter_random_character_hint") : StatsText(_characterIDs[index]);

        private void BindRules() {
            _stageSelect.ItemSelected += _ => UpdateStagePreview();
            PopulateStages();
            UpdateStagePreview();

            // F21: two modes. The retired Stock + Time combination is gone from
            // both selectors; a legacy saved Hybrid has already normalized to
            // timed Stock by the time these settings are read.
            _matchMode.AddItem(Tr("fighter_mode_stock"), (int)MatchMode.Stock);
            _matchMode.AddItem(Tr("fighter_mode_time"), (int)MatchMode.TimeLimit);

            // V7 "Match Settings Persist": the rule controls initialize from the
            // session settings (which EnsureMatchSettingsLoaded pre-loaded from
            // the global save) instead of hardcoded defaults.
            MatchSettings settings = GameManager.Instance?.CurrentSession.MatchSettings
                ?? MatchSettings.GetDefault();
            _matchMode.Select(SelectableModeIndex(settings.Mode));
            if (_stockCount != null) _stockCount.Value = settings.StockCount;
            if (_timeLimit != null) _timeLimit.Value = settings.TimeLimit;

            // Items keep their Off/Low/Medium/High frequency bands (the orb spawn
            // windows the simulation draws inside); hazards are a single On/Off
            // toggle (2026-09-26), and Items gains the M24 Meter pickups toggle.
            FillFrequencySelect(_itemFrequency, (int)settings.ItemSpawnRate);
            _meterPickups.ButtonPressed = settings.MeterPickupsEnabled;
            _stageHazards.ButtonPressed = settings.StageHazardsEnabled;
        }

        private static void FillFrequencySelect(OptionButton select, int selectedID) {
            select.Clear();
            select.AddItem(TranslationServer.Translate("fighter_frequency_off"), 0);
            select.AddItem(TranslationServer.Translate("fighter_frequency_low"), 1);
            select.AddItem(TranslationServer.Translate("fighter_frequency_medium"), 2);
            select.AddItem(TranslationServer.Translate("fighter_frequency_high"), 3);
            select.Select(Mathf.Clamp(selectedID, 0, 3));
        }

        private void PopulateStages() {
            _stageCatalog = FighterStageCatalog.LoadDefault();
            _stageIDs.Clear();
            _stageSelect.Clear();
            if (_stageCatalog == null) return;
            List<string> unlocked = SaveManager.Instance?.GlobalData?.UnlockedStages
                ?? new List<string>(GlobalSaveData.InitialStageIDs);
            foreach (FighterStageData stage in _stageCatalog.Stages) {
                if (stage == null || !stage.IsPlayable || !unlocked.Contains(stage.StageID)) continue;
                int itemID = _stageIDs.Count;
                _stageIDs.Add(stage.StageID);
                // Open/Sealed rides the item label itself (V7 "Floor Segments, Pits
                // & Ledges"): whether the main floor has a hole in it changes how a
                // match is played, so it belongs in front of the pick rather than
                // only in a tooltip.
                _stageSelect.AddItem(
                    $"{Tr(stage.DisplayNameKey)} - {Tr(FighterStageLayoutBadge.LabelKey(stage))}", itemID);
                int itemIndex = _stageSelect.ItemCount - 1;
                // A production-contract stage is no longer a prototype. The line
                // used to be appended unconditionally, which branded every stage —
                // including the shipped Florence Workshop — as placeholder routing.
                string tooltip =
                    $"{Tr(FighterStageLayoutBadge.TooltipKey(stage))}\n" +
                    $"{Tr(stage.LayoutDescriptionKey)}\n{Tr(stage.HazardNameKey)}: {Tr(stage.HazardDescriptionKey)}";
                if (!stage.ProductionReady) tooltip += $"\n{Tr("fighter_stage_prototype")}";
                _stageSelect.SetItemTooltip(itemIndex, tooltip);
            }
            _stageSelect.Disabled = _stageIDs.Count == 0;
            // G15a: the Random stage, resolved from the per-match seed at Fight
            // and revealed on the loading screen.
            if (_stageIDs.Count > 0) _stageSelect.AddItem(Tr("fighter_random_stage"), RandomStageItemID);
        }

        /// <summary>
        /// Shows the selected stage's placeholder preview plate. Production art
        /// replaces the SVG behind <see cref="FighterStageData.PreviewTexturePath"/>
        /// with no change here. The Random stage shows no plate until it resolves.
        /// </summary>
        private void UpdateStagePreview() {
            if (_stagePreview == null) return;
            _stagePreview.Texture = null;
            _stagePreview.Visible = false;

            int index = (int)_stageSelect.GetSelectedId();
            if (index < 0 || index >= _stageIDs.Count) return;

            FighterStageData stage = _stageCatalog?.Find(_stageIDs[index]);
            if (stage == null || string.IsNullOrWhiteSpace(stage.PreviewTexturePath)) return;
            if (!ResourceLoader.Exists(stage.PreviewTexturePath)) return;

            // Preview plates are streamable art, not authored tuning data, so they
            // deliberately do NOT go through AuthoredResources' pinning cache.
            var texture = ResourceLoader.Load<Texture2D>(stage.PreviewTexturePath);
            if (texture == null) return;
            _stagePreview.Texture = texture;
            _stagePreview.TooltipText = Tr(stage.DisplayNameKey);
            _stagePreview.Visible = true;
        }

        /// <summary>Opens the Move List for Player 1's current tile (lock wins
        /// over hover), returning focus to the footer button on close. The Random
        /// tile has no single kit, so it opens the first roster entry's.</summary>
        private void OpenMoveList() {
            if (_moveList == null || !IsInstanceValid(_moveList)) {
                _moveList = new MoveListScreen { Name = "MoveListScreen" };
                AddChild(_moveList);
                _moveList.Closed += () =>
                    GetNodeOrNull<Button>(SelectRoot + "ButtonRow/MoveListButton")?.GrabFocus();
            }
            int index = EffectiveIndex(_tokens[0]);
            _moveList.Open(_characterIDs[index == RandomTileIndex ? 0 : index]);
        }

        private void OpenSystemsCard() {
            if (_systemsCard == null || !IsInstanceValid(_systemsCard)) {
                _systemsCard = new SystemsCardScreen { Name = "SystemsCardScreen" };
                AddChild(_systemsCard);
                _systemsCard.Closed += () =>
                    GetNodeOrNull<Button>(SelectRoot + "ButtonRow/SystemsCardButton")?.GrabFocus();
            }
            _systemsCard.Open();
        }

        private Texture2D GetCharacterPortrait(int index) {
            CharacterData data = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{_characterIDs[index]}_data.tres");
            return data?.CharacterPortrait;
        }

        private string GetCharacterName(int index) {
            CharacterData data = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{_characterIDs[index]}_data.tres");
            return data == null || string.IsNullOrWhiteSpace(data.DisplayNameKey)
                ? Tr("common_unknown")
                : Tr(data.DisplayNameKey);
        }

        private string StatsText(string characterID) {
            var data = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{characterID}_data.tres");
            if (data == null) return Tr("fighter_stats_unavailable");
            return string.Format(
                Tr("fighter_stats_summary"),
                data.MaxHP, data.MaxMoveSpeed, data.BasicAttackDamage, data.BasicAttackKnockback,
                data.Weight, data.MaxBlockCharges, data.Style, data.MaxJumpForce, data.MaxJumpCount);
        }

        private static int EffectiveIndex(SelectionToken token) =>
            token.LockedIndex >= 0 ? token.LockedIndex : token.Cursor;

        /// <summary>
        /// Uniform cancel, one step at a time: stage select backs to selection, a
        /// running countdown aborts, a lock unwinds; only with nothing left to
        /// unwind does cancel leave the screen (exactly as the Back button does).
        /// </summary>
        public override void _UnhandledInput(InputEvent @event) {
            if (@event == null || !@event.IsActionPressed("ui_cancel")) return;
            GetViewport()?.SetInputAsHandled();
            if (_phase == SelectScreenPhase.StageSelect) {
                ReturnToSelection();
                return;
            }
            if (TryCancel(0)) return;
            OnBack();
        }

        private void OnFight() {
            FighterStageData stage = ApplySelectionToSession();
            if (stage == null || !ResourceLoader.Exists(stage.ScenePath)) return;
            GameManager.Instance.LoadScene(stage.ScenePath);
        }

        /// <summary>
        /// G15a: this match's seed — rolled once, kept on the session so the Random
        /// picks and the match itself share it (the driver consumes it).
        /// </summary>
        private int EnsureMatchSeed(ref SessionData session) {
            if (!session.HasPendingMatchSeed) {
                session.PendingMatchSeed = SeedOverride ?? unchecked((int)GD.Randi());
                session.HasPendingMatchSeed = true;
            }
            return session.PendingMatchSeed;
        }

        /// <summary>A tile index resolved to a roster ID, drawing the Random tile from the seed.</summary>
        private string ResolveCharacter(int tileIndex, int seed, int salt) {
            if (tileIndex != RandomTileIndex) return _characterIDs[Math.Clamp(tileIndex, 0, _characterIDs.Length - 1)];
            return _characterIDs[FighterRandomPick.Index(seed, salt, _characterIDs.Length)];
        }

        /// <summary>
        /// Writes the locked characters (Random resolved from the per-match seed)
        /// into the session. Idempotent for a given seed.
        /// </summary>
        private void CommitCharactersToSession() {
            if (GameManager.Instance == null) return;
            SessionData session = GameManager.Instance.CurrentSession;
            int seed = EnsureMatchSeed(ref session);
            session.SelectedCharacterID = ResolveCharacter(
                EffectiveIndex(_tokens[0]), seed, FighterRandomPick.PlayerOneCharacterSalt);
            if (!IsVersusCpuFlow) {
                session.OpponentCharacterID = ResolveCharacter(
                    EffectiveIndex(_tokens[1]), seed, FighterRandomPick.PlayerTwoCharacterSalt);
            }
            GameManager.Instance.CurrentSession = session;
        }

        /// <summary>
        /// Writes every control's value into the session and returns the stage the
        /// match should route to, or null when the selection cannot resolve. Local
        /// Versus only — Versus CPU launches from its configuration panel.
        ///
        /// <para>Split out of <c>OnFight</c> so the session round-trip is testable
        /// without triggering a real scene change: a test that pressed Fight would
        /// tear the GdUnit runner's own scene out from under it two seconds
        /// later.</para>
        /// </summary>
        public FighterStageData ApplySelectionToSession() {
            if (GameManager.Instance == null) return null;
            int selectedStageItem = (int)_stageSelect.GetSelectedId();
            if (_stageIDs.Count == 0) return null;
            if (selectedStageItem != RandomStageItemID
                && (selectedStageItem < 0 || selectedStageItem >= _stageIDs.Count)) return null;

            CommitCharactersToSession();
            var session = GameManager.Instance.CurrentSession;
            int seed = EnsureMatchSeed(ref session);
            int stageIndex = selectedStageItem == RandomStageItemID
                ? FighterRandomPick.Index(seed, FighterRandomPick.StageSalt, _stageIDs.Count)
                : selectedStageItem;
            session.SelectedStageID = _stageIDs[stageIndex];
            // A LAN session set by the Network Select screen survives the
            // character select; otherwise this is the two-human local flow.
            if (session.FighterOpponentType != FighterOpponentType.Lan) {
                session.FighterOpponentType = FighterOpponentType.LocalHuman;
            }
            session.FighterMatchOrigin = FighterMatchOrigin.LocalVersus;
            MatchSettings settings = session.MatchSettings;
            settings.Mode = (MatchMode)_matchMode.GetSelectedId();
            settings.StockCount = (int)_stockCount.Value;
            settings.TimeLimit = ResolveTimeLimitForMode(settings.Mode, (float)_timeLimit.Value);
            var itemRate = (ChronalOrbFrequency)_itemFrequency.GetSelectedId();
            settings.ItemSpawnRate = itemRate;
            settings.ItemsEnabled = itemRate != ChronalOrbFrequency.Off;
            settings.MeterPickupsEnabled = _meterPickups.ButtonPressed;
            settings.StageHazardsEnabled = _stageHazards.ButtonPressed;
            session.MatchSettings = settings;
            GameManager.Instance.CurrentSession = session;
            // V7 "Match Settings Persist": set-and-forget for house rules.
            GameManager.Instance.PersistMatchSettings();
            return _stageCatalog?.Find(session.SelectedStageID);
        }

        /// <summary>
        /// Where Back routes: the hub for a Holodeck session, the Fighter menu for
        /// both front-end flows. Exposed so a test can assert the route without a
        /// real scene change.
        /// </summary>
        public string ResolveBackScenePath() =>
            FighterFlowRoutes.SelectBack(GameManager.Instance?.CurrentSession.FighterMatchOrigin
                ?? FighterMatchOrigin.LocalVersus);

        private void OnBack() {
            GameManager.Instance?.LoadScene(ResolveBackScenePath());
        }

        /// <summary>
        /// F21: the option list carries only the two selectable modes, so the index
        /// a saved mode maps to is its ordinal — clamped, because an out-of-range
        /// stored value (the retired Hybrid, or a corrupt payload that reached the
        /// menu before <c>SavedMatchSettings.Normalize</c> could run) would
        /// otherwise index past the list.
        /// </summary>
        private static int SelectableModeIndex(MatchMode mode) =>
            mode == MatchMode.TimeLimit ? 1 : 0;

        /// <summary>
        /// F21: <b>Time requires a positive timer and Off is unavailable there</b>,
        /// so switching from untimed Stock to Time selects the default 480 s. The
        /// spin box is written before launch, which is what "shows it before launch"
        /// asks for — the player sees the repaired value in the rules row. Stock
        /// keeps its own timer, Off included, and characters, stage, items and
        /// hazards are untouched by the switch.
        /// </summary>
        private float ResolveTimeLimitForMode(MatchMode mode, float requested) {
            if (mode != MatchMode.TimeLimit) return requested;
            if (requested > 0f && !float.IsNaN(requested) && !float.IsInfinity(requested)) return requested;
            float repaired = SavedMatchSettings.DefaultTimeLimitSeconds;
            if (_timeLimit != null) _timeLimit.Value = repaired;
            return repaired;
        }

    }
}
