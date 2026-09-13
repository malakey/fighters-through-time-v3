using System;
using System.Collections.Generic;
using FTT.Characters;
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
        /// <summary>Stage catalog + match rules; Fight routes the match.</summary>
        StageSelect
    }

    /// <summary>
    /// Package 8 B4 authored screen, reworked for audit M-28: the designed
    /// select-screen mechanics from design-godot.md:2586-2599.
    ///
    /// <para><b>Two selection tokens, per-device.</b> Token 0 is Player 1's pick;
    /// token 1 is the opponent (Player 2 in local-human mode, the CPU pick in CPU
    /// mode). Each human player's token is driven only by that player's assigned
    /// device, polled through <see cref="InputManager.GetFrame(int)"/> — never
    /// through global focus navigation, which any device can steer. Movement uses
    /// the gameplay move axis plus Jump/Down; BasicAttack (or Interact) confirms;
    /// Block (or Roll) cancels. The roster tiles stay clickable buttons for the
    /// mouse (clicks act as Player 1: first click hovers, second confirms) but are
    /// deliberately excluded from the focus chain so a pad in Player 2's hands
    /// cannot move Player 1's token.</para>
    ///
    /// <para><b>Reserved/Occupied.</b> A hovered tile is painted with the
    /// hovering token's colour (Reserved); a confirmed tile is locked with a
    /// thick border and glow (Occupied). V7.3 ruling #17: mirror matches are
    /// allowed — both tokens may lock the same tile (the border blends the two
    /// colours), and the old duplicate-pick refusal is gone.</para>
    ///
    /// <para><b>Ready → countdown → stage.</b> Confirm toggles READY (banner per
    /// player); once every required token is locked, a 3.0-second countdown runs
    /// that any ready player's cancel aborts back to selection. At zero the screen
    /// swaps to a distinct full-screen stage-select state reusing the existing
    /// catalog population, ProductionReady gating, and preview-plate rendering,
    /// with the match rules alongside. All session writes are unchanged from the
    /// pre-rework behaviour (<see cref="ApplySelectionToSession"/>).</para>
    /// </summary>
    public partial class CharacterSelectScreen : Control {

        private const string SelectRoot = "SelectPhase/Root/";
        private const string StageRoot = "StagePhase/Root/";
        private const int GridColumns = 3;
        private const float CountdownSeconds = 3.0f;
        private const float AxisThreshold = 0.5f;

        private sealed class SelectionToken {
            public int Cursor;
            public int LockedIndex = -1;
            public bool Ready => LockedIndex >= 0;
        }

        private readonly string[] _characterIDs = {
            "einstein", "joan", "leonardo", "lincoln", "cleopatra",
            "tesla", "shakespeare", "mozart", "pocahontas"
        };

        /// <summary>Token 0 = Player 1; token 1 = Player 2 or the CPU pick.</summary>
        private readonly SelectionToken[] _tokens = { new(), new() { Cursor = 1 } };
        private readonly float[] _previousHorizontal = new float[2];

        private SelectScreenPhase _phase = SelectScreenPhase.Selection;
        private float _countdownRemaining;

        private Button[] _characterButtons;
        private Control _selectPhase;
        private Control _stagePhase;
        private Label _p1Name;
        private Label _p1Ready;
        private Label _p1Stats;
        private Label _p2Name;
        private Label _p2Ready;
        private Label _p2Stats;
        private Label _countdownLabel;
        private CheckButton _localHumanToggle;
        private OptionButton _cpuDifficulty;
        private SpinBox _stockCount;
        private SpinBox _timeLimit;
        private OptionButton _itemFrequency;
        private OptionButton _hazardFrequency;
        private OptionButton _matchMode;
        private OptionButton _stageSelect;
        private TextureRect _stagePreview;
        private FighterStageCatalog _stageCatalog;
        private readonly List<string> _stageIDs = new();
        private MoveListScreen _moveList;
        private SystemsCardScreen _systemsCard;

        /// <summary>The current flow state.</summary>
        public SelectScreenPhase Phase => _phase;

        /// <summary>Seconds left on the ready countdown (valid in Countdown phase).</summary>
        public float CountdownRemaining => _countdownRemaining;

        /// <summary>The tile index currently chosen for player one (lock wins over hover).</summary>
        public int SelectedIndex => EffectiveIndex(_tokens[0]);

        /// <summary>The tile index currently chosen for the opponent (lock wins over hover).</summary>
        public int OpponentIndex => EffectiveIndex(_tokens[1]);

        /// <summary>Whether the given token (0 = P1, 1 = P2/CPU) has confirmed its pick.</summary>
        public bool IsSlotReady(int token) => _tokens[token].Ready;

        /// <summary>The given token's hover cursor.</summary>
        public int GetCursor(int token) => _tokens[token].Cursor;

        /// <summary>True while Player 1, already locked, is choosing the CPU's character.</summary>
        public bool IsPickingCpu =>
            _phase == SelectScreenPhase.Selection && !IsLocalHumanMode
            && _tokens[0].Ready && !_tokens[1].Ready;

        private bool IsLocalHumanMode => _localHumanToggle != null && _localHumanToggle.ButtonPressed;

        public override void _Ready() {
            UIPalette.ApplyTheme(this);
            // V7 "Match Settings Persist": the last-used rules pre-load from the
            // global save before the rule controls bind their initial values.
            GameManager.Instance?.EnsureMatchSettingsLoaded();

            _selectPhase = GetNode<Control>("SelectPhase");
            _stagePhase = GetNode<Control>("StagePhase");
            _p1Name = GetNode<Label>(SelectRoot + "PlayersRow/P1Panel/P1Name");
            _p1Ready = GetNode<Label>(SelectRoot + "PlayersRow/P1Panel/P1Ready");
            _p1Stats = GetNode<Label>(SelectRoot + "PlayersRow/P1Panel/P1Stats");
            _p2Name = GetNode<Label>(SelectRoot + "PlayersRow/P2Panel/P2Name");
            _p2Ready = GetNode<Label>(SelectRoot + "PlayersRow/P2Panel/P2Ready");
            _p2Stats = GetNode<Label>(SelectRoot + "PlayersRow/P2Panel/P2Stats");
            _countdownLabel = GetNode<Label>(SelectRoot + "CountdownLabel");
            _localHumanToggle = GetNode<CheckButton>(SelectRoot + "ModeRow/LocalHumanToggle");
            _cpuDifficulty = GetNode<OptionButton>(SelectRoot + "ModeRow/CpuDifficulty");
            _stageSelect = GetNode<OptionButton>(StageRoot + "StageRow/StageSelect");
            _stagePreview = GetNode<TextureRect>(StageRoot + "StageRow/StagePreview");
            _matchMode = GetNode<OptionButton>(StageRoot + "RulesRow/MatchMode");
            _stockCount = GetNode<SpinBox>(StageRoot + "RulesRow/StockCount");
            _timeLimit = GetNode<SpinBox>(StageRoot + "RulesRow/TimeLimit");
            _itemFrequency = GetNode<OptionButton>(StageRoot + "RulesRow/ItemFrequency");
            _hazardFrequency = GetNode<OptionButton>(StageRoot + "RulesRow/HazardFrequency");

            BindCharacterGrid();
            BindModeAndRules();

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

            // Returning here from a match ("Change Fighters") keeps the session's
            // opponent mode instead of silently resetting local-human to CPU.
            if (GameManager.Instance != null) {
                _localHumanToggle.ButtonPressed =
                    GameManager.Instance.CurrentSession.FighterOpponentType == FighterOpponentType.LocalHuman;
            }
            _cpuDifficulty.Disabled = IsLocalHumanMode;

            RefreshPanels();
            RepaintTiles();
            BuildFocusChain();

            // Post-match "New Stage" (V7): same characters, straight back to the
            // stage phase. The flag is consumed on read so Back still works.
            if (GameManager.Instance != null && GameManager.Instance.CurrentSession.ResumeAtStageSelect) {
                SessionData session = GameManager.Instance.CurrentSession;
                session.ResumeAtStageSelect = false;
                GameManager.Instance.CurrentSession = session;
                int p1 = System.Array.IndexOf(_characterIDs, session.SelectedCharacterID);
                int p2 = System.Array.IndexOf(_characterIDs, session.OpponentCharacterID);
                if (p1 >= 0 && p2 >= 0) {
                    _tokens[0].LockedIndex = p1;
                    _tokens[1].LockedIndex = p2;
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
        /// Which token the given player currently drives: in local-human mode each
        /// player drives their own; in CPU mode Player 1 drives their own token
        /// until it locks, then drives the CPU pick. -1 when the player drives none
        /// (Player 2 in CPU mode).
        /// </summary>
        private int TokenDrivenBy(int playerIndex) {
            if (IsLocalHumanMode) return playerIndex is 0 or 1 ? playerIndex : -1;
            if (playerIndex != 0) return -1;
            return _tokens[0].Ready ? 1 : 0;
        }

        /// <summary>Which token this player's cancel unlocks, or -1.</summary>
        private int TokenToUnlockFor(int playerIndex) {
            if (IsLocalHumanMode) {
                if (playerIndex is not (0 or 1)) return -1;
                return _tokens[playerIndex].Ready ? playerIndex : -1;
            }
            if (playerIndex != 0) return -1;
            if (_tokens[1].Ready) return 1;
            return _tokens[0].Ready ? 0 : -1;
        }

        /// <summary>
        /// Moves the token the player drives by one grid step with wraparound.
        /// Locked tokens do not move — cancel first.
        /// </summary>
        public bool TryMoveCursor(int playerIndex, int dx, int dy) {
            if (_phase != SelectScreenPhase.Selection) return false;
            int tokenIndex = TokenDrivenBy(playerIndex);
            if (tokenIndex < 0) return false;
            SelectionToken token = _tokens[tokenIndex];
            if (token.Ready) return false;

            int rows = (_characterIDs.Length + GridColumns - 1) / GridColumns;
            int column = ((token.Cursor % GridColumns) + dx + GridColumns) % GridColumns;
            int row = ((token.Cursor / GridColumns) + dy + rows) % rows;
            token.Cursor = Math.Min(row * GridColumns + column, _characterIDs.Length - 1);
            RefreshPanels();
            RepaintTiles();
            return true;
        }

        /// <summary>
        /// Confirms the driven token on its hovered tile. V7.3 ruling #17:
        /// mirror matches are allowed — a tile another token has locked is
        /// freely confirmable. Locking the last required token starts the
        /// countdown.
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
            TryStartCountdown();
            return true;
        }

        /// <summary>
        /// Cancels for the given player: during the countdown any ready player's
        /// cancel aborts the timer and unlocks their token, returning to selection;
        /// during selection it unlocks that player's most recent lock (in CPU mode,
        /// first the CPU pick, then Player 1's own). Returns false when there was
        /// nothing to unwind (the caller may then leave the screen).
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
            _selectPhase.Visible = false;
            _stagePhase.Visible = true;
            UpdateStagePreview();
            BuildFocusChain();
        }

        /// <summary>
        /// Backs out of stage select. Everyone returns to selection unreadied
        /// (cursors keep their picks) so a re-confirm restarts the countdown
        /// rather than instantly re-entering the stage phase.
        /// </summary>
        public void ReturnToSelection() {
            if (_phase != SelectScreenPhase.StageSelect) return;
            foreach (SelectionToken token in _tokens) token.LockedIndex = -1;
            _phase = SelectScreenPhase.Selection;
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
                _hazardFrequency,
                GetNode<Button>(StageRoot + "StageButtonRow/StageBackButton"),
                GetNode<Button>(StageRoot + "StageButtonRow/FightButton")
            }
            : new List<Control> {
                _localHumanToggle,
                _cpuDifficulty,
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
            _characterButtons = new Button[_characterIDs.Length];
            for (int index = 0; index < _characterIDs.Length; index++) {
                int captured = index;
                var button = grid.GetNode<Button>($"CharacterButton{index}");
                // Button icons share a horizontal row with their text, so long
                // names such as "Wolfgang Amadeus Mozart" can squeeze the icon
                // down to a sliver. Keep the full name and give every portrait a
                // dedicated layer above it instead.
                button.Text = string.Empty;
                AddPortraitLayer(button, GetCharacterPortrait(index), GetCharacterName(index));
                // Cursor-driven, not focus-driven: see FocusChain.
                button.FocusMode = FocusModeEnum.None;
                button.AddToGroup(FocusChainBuilder.SkipGroup);
                button.Pressed += () => OnTilePressed(captured);
                _characterButtons[index] = button;
            }
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

        /// <summary>
        /// Paints one tile from the token states. Occupied (locked) tiles carry a
        /// thick border and glow in the owning token's colour; Reserved (hovered)
        /// tiles a thinner border; overlapping hovers blend the two colours, and
        /// (V7.3, mirror matches) a tile BOTH tokens have locked blends the two
        /// lock colours the same way. The theme's focus stylebox is never
        /// overridden.
        /// </summary>
        private void StyleTile(int index) {
            Color color = CharacterFactory.GetCharacterColor(_characterIDs[index]);
            Color opponentAccent = IsLocalHumanMode ? UIPalette.TemporalViolet : UIPalette.Warning;

            Color borderColor = new(0.2f, 0.2f, 0.25f);
            int borderWidth = 2;
            bool occupied = false;

            if (_tokens[0].LockedIndex == index && _tokens[1].LockedIndex == index) {
                borderColor = UIPalette.Cyan.Lerp(opponentAccent, 0.5f);
                borderWidth = 5;
                occupied = true;
            } else if (_tokens[0].LockedIndex == index) {
                borderColor = UIPalette.Cyan;
                borderWidth = 5;
                occupied = true;
            } else if (_tokens[1].LockedIndex == index) {
                borderColor = opponentAccent;
                borderWidth = 5;
                occupied = true;
            } else {
                bool p1Hover = !_tokens[0].Ready && _tokens[0].Cursor == index;
                bool opponentHover = OpponentCursorActive && !_tokens[1].Ready && _tokens[1].Cursor == index;
                if (p1Hover && opponentHover) {
                    borderColor = UIPalette.Cyan.Lerp(opponentAccent, 0.5f);
                    borderWidth = 4;
                } else if (p1Hover) {
                    borderColor = UIPalette.Cyan;
                    borderWidth = 4;
                } else if (opponentHover) {
                    borderColor = opponentAccent;
                    borderWidth = 4;
                }
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

            Button button = _characterButtons[index];
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

        private bool OpponentCursorActive => IsLocalHumanMode || IsPickingCpu || _tokens[1].Ready;

        private void RefreshPanels() {
            if (_p1Name == null) return;

            int p1Index = EffectiveIndex(_tokens[0]);
            _p1Name.Text = string.Format(Tr("fighter_player_selection"), 1, GetCharacterName(p1Index));
            _p1Ready.Visible = _tokens[0].Ready;
            _p1Stats.Text = StatsText(_characterIDs[p1Index]);

            int p2Index = EffectiveIndex(_tokens[1]);
            string opponentName = GetCharacterName(p2Index);
            _p2Name.Text = IsLocalHumanMode
                ? string.Format(Tr("fighter_player_selection"), 2, opponentName)
                : string.Format(Tr("fighter_cpu_selection"), opponentName);
            _p2Ready.Visible = _tokens[1].Ready;
            _p2Stats.Text = IsPickingCpu
                ? $"{Tr("fighter_pick_cpu_prompt")}\n{StatsText(_characterIDs[p2Index])}"
                : StatsText(_characterIDs[p2Index]);
        }

        private void BindModeAndRules() {
            _localHumanToggle.Toggled += enabled => {
                _cpuDifficulty.Disabled = enabled;
                ResetSelection();
            };
            _cpuDifficulty.AddItem(Tr("difficulty_easy"), (int)CpuDifficulty.Easy);
            _cpuDifficulty.AddItem(Tr("difficulty_normal"), (int)CpuDifficulty.Normal);
            _cpuDifficulty.AddItem(Tr("difficulty_hard"), (int)CpuDifficulty.Hard);
            _cpuDifficulty.Select(1);

            _stageSelect.ItemSelected += _ => UpdateStagePreview();
            PopulateStages();
            UpdateStagePreview();

            _matchMode.AddItem(Tr("fighter_mode_stock"), (int)MatchMode.Stock);
            _matchMode.AddItem(Tr("fighter_mode_time"), (int)MatchMode.TimeLimit);
            _matchMode.AddItem(Tr("fighter_mode_hybrid"), (int)MatchMode.Hybrid);

            // V7 "Match Settings Persist": the rule controls initialize from the
            // session settings (which EnsureMatchSettingsLoaded pre-loaded from
            // the global save) instead of hardcoded defaults.
            MatchSettings settings = GameManager.Instance?.CurrentSession.MatchSettings
                ?? MatchSettings.GetDefault();
            _matchMode.Select((int)settings.Mode);
            if (_stockCount != null) _stockCount.Value = settings.StockCount;
            if (_timeLimit != null) _timeLimit.Value = settings.TimeLimit;

            // Off/Low/Medium/High, matching the deterministic spawn-interval bands
            // the simulation actually consumes rather than a binary on/off.
            FillFrequencySelect(_itemFrequency, (int)settings.ItemSpawnRate);
            FillFrequencySelect(_hazardFrequency, (int)settings.HazardRate);
        }

        /// <summary>Opponent-mode change invalidates every lock; back to a clean selection.</summary>
        private void ResetSelection() {
            foreach (SelectionToken token in _tokens) token.LockedIndex = -1;
            if (_phase != SelectScreenPhase.Selection) {
                _phase = SelectScreenPhase.Selection;
                _countdownLabel.Visible = false;
                _stagePhase.Visible = false;
                _selectPhase.Visible = true;
                BuildFocusChain();
            }
            RefreshPanels();
            RepaintTiles();
        }

        private static void FillFrequencySelect(OptionButton select, int selectedID) {
            select.Clear();
            select.AddItem(TranslationServer.Translate("fighter_frequency_off"), 0);
            select.AddItem(TranslationServer.Translate("fighter_frequency_low"), 1);
            select.AddItem(TranslationServer.Translate("fighter_frequency_medium"), 2);
            select.AddItem(TranslationServer.Translate("fighter_frequency_high"), 3);
            select.Select(selectedID);
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
        }

        /// <summary>
        /// Shows the selected stage's placeholder preview plate. Production art
        /// replaces the SVG behind <see cref="FighterStageData.PreviewTexturePath"/>
        /// with no change here.
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
        /// over hover), returning focus to the footer button on close.</summary>
        private void OpenMoveList() {
            if (_moveList == null || !IsInstanceValid(_moveList)) {
                _moveList = new MoveListScreen { Name = "MoveListScreen" };
                AddChild(_moveList);
                _moveList.Closed += () =>
                    GetNodeOrNull<Button>(SelectRoot + "ButtonRow/MoveListButton")?.GrabFocus();
            }
            _moveList.Open(_characterIDs[EffectiveIndex(_tokens[0])]);
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
        /// Writes every control's value into the session and returns the stage the
        /// match should route to, or null when the selection cannot resolve.
        ///
        /// <para>Split out of <c>OnFight</c> so the session round-trip is testable
        /// without triggering a real scene change: a test that pressed Fight would
        /// tear the GdUnit runner's own scene out from under it two seconds
        /// later.</para>
        /// </summary>
        public FighterStageData ApplySelectionToSession() {
            if (GameManager.Instance == null) return null;

            var session = GameManager.Instance.CurrentSession;
            session.SelectedCharacterID = _characterIDs[EffectiveIndex(_tokens[0])];
            session.OpponentCharacterID = _characterIDs[EffectiveIndex(_tokens[1])];
            int selectedStageIndex = (int)_stageSelect.GetSelectedId();
            if (selectedStageIndex < 0 || selectedStageIndex >= _stageIDs.Count) return null;
            session.SelectedStageID = _stageIDs[selectedStageIndex];
            // A LAN session set by the Network Select screen survives the
            // character select — the toggle only distinguishes the local modes.
            if (session.FighterOpponentType != FighterOpponentType.Lan) {
                session.FighterOpponentType = _localHumanToggle.ButtonPressed
                    ? FighterOpponentType.LocalHuman
                    : FighterOpponentType.Cpu;
            }
            session.CpuDifficulty = (CpuDifficulty)_cpuDifficulty.GetSelectedId();
            MatchSettings settings = session.MatchSettings;
            settings.Mode = (MatchMode)_matchMode.GetSelectedId();
            settings.StockCount = (int)_stockCount.Value;
            settings.TimeLimit = (float)_timeLimit.Value;
            var itemRate = (ChronalOrbFrequency)_itemFrequency.GetSelectedId();
            var hazardRate = (HazardTriggerFrequency)_hazardFrequency.GetSelectedId();
            settings.ItemSpawnRate = itemRate;
            settings.ItemsEnabled = itemRate != ChronalOrbFrequency.Off;
            settings.HazardRate = hazardRate;
            settings.StageHazardsEnabled = hazardRate != HazardTriggerFrequency.Off;
            session.MatchSettings = settings;
            GameManager.Instance.CurrentSession = session;
            // V7 "Match Settings Persist": set-and-forget for house rules.
            GameManager.Instance.PersistMatchSettings();
            return _stageCatalog?.Find(session.SelectedStageID);
        }

        /// <summary>
        /// Where Back routes: the hub for a Holodeck practice session, the main
        /// menu otherwise. Exposed so the Holodeck regression test can assert the
        /// route without a real scene change.
        /// </summary>
        public string ResolveBackScenePath() {
            bool holodeck = GameManager.Instance?.CurrentSession.ReturnToHubAfterFighterMatch == true;
            return holodeck
                ? "res://scenes/campaign/HubWorld.tscn"
                : "res://scenes/menus/MainMenu.tscn";
        }

        private void OnBack() {
            GameManager.Instance?.LoadScene(ResolveBackScenePath());
        }
    }
}
