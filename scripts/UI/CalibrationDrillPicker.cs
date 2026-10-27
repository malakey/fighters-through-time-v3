using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// The standalone Calibration Drills character picker (F18 Option B).
    ///
    /// <para>Deliberately <b>not</b> a reuse of <see cref="CharacterSelectScreen"/>:
    /// this is one player choosing one fighter. There is no second token, no Ready
    /// gate, no cancelable countdown and no stage phase — the drill scene owns its
    /// own Sealed stage. What is reused is the shape: the same nine-tile grid with
    /// portrait layers, the same stat summary, and the same Move List / Systems
    /// Card footer, so the onboarding surfaces read identically wherever a player
    /// meets them.</para>
    ///
    /// <para>Every tile is a full <b>normalized Fighter kit</b>. No Story save is
    /// consulted, created or written, and no campaign reward, fee or progression
    /// value is reachable from here.</para>
    /// </summary>
    public partial class CalibrationDrillPicker : Control {

        /// <summary>
        /// The runtime roster, in authored grid order. Package 13 W5: read from
        /// <see cref="CharacterRoster"/> (the content manifest's Character rows)
        /// instead of a literal list — the roster swap replaced a literal
        /// <c>"pocahontas"</c> here — and bound defensively onto the authored
        /// tiles, so a roster larger than the grid degrades instead of throwing.
        /// </summary>
        private static string[] RosterIDs => CharacterRoster.ToArray();

        private const string Root = "Select/Root/";

        private Label _nameLabel;
        private Label _statsLabel;
        private MoveListScreen _moveList;
        private SystemsCardScreen _systemsCard;
        private string[] _roster = System.Array.Empty<string>();
        private Button[] _tiles = System.Array.Empty<Button>();
        private int _highlighted;

        /// <summary>The character the preview panel is describing. Test surface.</summary>
        public string HighlightedCharacterID => _roster.Length > 0 ? _roster[_highlighted] : "";

        /// <summary>The roster IDs this screen offers (the manifest roster). Test surface.</summary>
        public static System.Collections.Generic.IReadOnlyList<string> Roster => CharacterRoster.IDs;

        /// <summary>The picker tile (UI_SPEC_GLASS 3.7, 9.2: the select tile, 164 x 164, no Random tile).</summary>
        public const float TileSize = 164f;
        /// <summary>The tile name's box (3.7: centred in 156 px, its bottom 11 px above the tile's bottom).</summary>
        public const float NameBoxWidth = 156f, NameBottomInset = 11f;

        private static readonly StyleBoxEmpty TileBox = new();
        private static readonly string[] TileBoxSlots = { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled" };

        public override void _Ready() {
            UIPalette.ApplyTheme(this);
            _nameLabel = GetNode<Label>(Root + "PreviewPanel/Preview/PreviewName");
            _statsLabel = GetNode<Label>(Root + "PreviewPanel/Preview/PreviewStats");

            var grid = GetNode<GridContainer>(Root + "Grid");
            string[] roster = RosterIDs;
            var tiles = new System.Collections.Generic.List<Button>(roster.Length);
            for (int index = 0; index < roster.Length; index++) {
                var tile = grid.GetNodeOrNull<Button>($"CharacterButton{index}");
                if (tile == null) break; // more roster than authored tiles: bind what fits
                tiles.Add(tile);
            }
            _tiles = tiles.ToArray();
            _roster = roster[.._tiles.Length];
            for (int index = 0; index < _tiles.Length; index++) {
                int captured = index;
                Button tile = _tiles[index];
                tile.Text = string.Empty;
                DressTile(tile, PortraitFor(_roster[index]), CardNameFor(_roster[index]));
                // Unlike the Fighter select's cursor-driven tiles, these are plain
                // focusable buttons: one player, one token, so focus *is* the cursor.
                // The glass tile lights its edge on focus (hover takes focus); its name turns white then, ink2 otherwise.
                Label tileName = tile.GetNodeOrNull<Label>("CharacterName");
                void Tint() {
                    bool lit = tile.HasFocus() || tile.IsHovered();
                    tileName?.AddThemeColorOverride("font_color", lit ? UIPalette.TextFocus : UIPalette.Ink2);
                }
                tile.FocusEntered += Tint;
                tile.FocusExited += Tint;
                tile.MouseEntered += Tint;
                tile.MouseExited += Tint;
                Tint();
                tile.FocusEntered += () => Highlight(captured);
                tile.MouseEntered += () => Highlight(captured);
                tile.Pressed += () => Choose(captured);
            }

            GetNode<Button>(Root + "ButtonRow/MoveListButton").Pressed += OpenMoveList;
            GetNode<Button>(Root + "ButtonRow/SystemsCardButton").Pressed += OpenSystemsCard;
            GetNode<Button>(Root + "ButtonRow/BackButton").Pressed += Back;

            DressGlass();
            Highlight(StartingIndex());
            FocusChainBuilder.Apply(GetNode<Control>("Select"));
            if (_tiles.Length > 0) _tiles[_highlighted].GrabFocus();
        }

        /// <summary>
        /// TG-SCREENS (UI_SPEC_GLASS 9.1, 9.2: the Calibration picker): a full-screen menu. The sub-screen art, the heading
        /// <c>drill_picker_title</c> between flourishes top-centre, the hint as scene text, a 3 x 3 grid of glass select
        /// tiles (no Random), the hero's preview on a glass slab, the footer as compact chevrons at their D6 width with no
        /// prompt badge and the derived row gap, the prompt bar bottom-right and the screen streak. The block shrinks to fit
        /// under the heading at a large UI scale.
        /// </summary>
        private void DressGlass() {
            PulpScreenKit.AddMenuArt(this, index: 1);
            PulpScreenKit.AddScreenHeading(this, "drill_picker_title");
            if (GetNodeOrNull<PanelContainer>(Root + "PreviewPanel") is { } preview) PulpScreenKit.DressPanel(preview);
            var footer = GetNode<HBoxContainer>(Root + "ButtonRow");
            System.Collections.Generic.List<Button> buttons = PulpScreenKit.ButtonsOf(footer);
            foreach (Button button in buttons) FTT.UI.Pulp.PulpButtonDecor.Apply(button, compact: true).ShowPrompt = false;
            FTT.UI.Pulp.PulpButtonDecor.ApplyStackWidth(buttons, 290f, reservePrompt: false);
            PulpScreenKit.SeparateFocusRow(footer);
            MenuSafeAreaFit.Attach(GetNode<Control>(Root.TrimEnd('/')), PulpScreenKit.ScaledHeadingReserve);
            PulpScreenKit.AddPromptBar(this, ("ui_accept", "ui_prompt_select"), ("ui_cancel", "common_back"));
            PulpScreenKit.AddScreenStreak(this);
        }

        /// <summary>
        /// Opens on the session's character when there is one, so re-entering the
        /// route lands where the player left it. Never reads a save slot.
        /// </summary>
        private int StartingIndex() {
            string current = GameManager.Instance?.CurrentSession.SelectedCharacterID;
            int index = System.Array.IndexOf(_roster, current ?? "");
            return index < 0 ? 0 : index;
        }

        private void Highlight(int index) {
            if (index < 0 || index >= _roster.Length) return;
            _highlighted = index;
            string characterID = _roster[index];
            if (_nameLabel != null) _nameLabel.Text = NameFor(characterID);
            if (_statsLabel != null) _statsLabel.Text = StatsFor(characterID);
        }

        /// <summary>
        /// Commits the choice to the session and hands off to the shared drill list.
        /// Only <c>SelectedCharacterID</c> moves: the active save slot, difficulty
        /// and campaign state are left exactly as they were.
        /// </summary>
        public void Choose(int index) {
            string destination = ApplyChoiceToSession(index);
            if (destination != null) GameManager.Instance.LoadScene(destination);
        }

        /// <summary>
        /// Writes the choice and returns the scene to open, or null when there is
        /// no session. Split from <see cref="Choose"/> so the session contract is
        /// testable without a real scene change (the Holodeck console idiom).
        /// </summary>
        public string ApplyChoiceToSession(int index) {
            if (GameManager.Instance == null) return null;
            SessionData session = GameManager.Instance.CurrentSession;
            session.SelectedCharacterID = _roster[index];
            session.CalibrationDrillIndex = 0;
            if (string.IsNullOrWhiteSpace(session.CalibrationReturnScenePath)) {
                session.CalibrationReturnScenePath = CalibrationRoute.MainMenuScenePath;
            }
            GameManager.Instance.CurrentSession = session;
            return CalibrationRoute.DrillListScenePath;
        }

        /// <summary>Back from the picker leaves the route entirely (F18).</summary>
        public void Back() {
            string destination = ApplyBackToSession();
            if (destination != null) GameManager.Instance.LoadScene(destination);
        }

        /// <summary>Disarms the route and returns where Back goes.</summary>
        public string ApplyBackToSession() {
            if (GameManager.Instance == null) return null;
            string destination = CalibrationRoute.ExitDestination(
                GameManager.Instance.CurrentSession.CalibrationReturnScenePath);
            GameManager.Instance.CurrentSession =
                CalibrationRoute.Cleared(GameManager.Instance.CurrentSession);
            return destination;
        }

        public override void _UnhandledInput(InputEvent @event) {
            if (@event == null || !@event.IsActionPressed("ui_cancel")) return;
            if (_moveList is { Visible: true } || _systemsCard is { Visible: true }) return;
            GetViewport()?.SetInputAsHandled();
            Back();
        }

        // ---- shared onboarding surfaces --------------------------------------

        private void OpenMoveList() {
            if (_moveList == null || !IsInstanceValid(_moveList)) {
                _moveList = new MoveListScreen { Name = "MoveListScreen" };
                AddChild(_moveList);
                _moveList.Closed += () =>
                    GetNodeOrNull<Button>(Root + "ButtonRow/MoveListButton")?.GrabFocus();
            }
            // Always the full kit: a drill is Fighter-side, so no Legacy Unlock
            // Dormant badge ever applies here.
            _moveList.Open(HighlightedCharacterID);
        }

        private void OpenSystemsCard() {
            if (_systemsCard == null || !IsInstanceValid(_systemsCard)) {
                _systemsCard = new SystemsCardScreen { Name = "SystemsCardScreen" };
                AddChild(_systemsCard);
                _systemsCard.Closed += () =>
                    GetNodeOrNull<Button>(Root + "ButtonRow/SystemsCardButton")?.GrabFocus();
            }
            _systemsCard.Open();
        }

        // ---- roster data ------------------------------------------------------

        private static CharacterData DataFor(string characterID) =>
            AuthoredResources.Load<CharacterData>($"res://resources/Characters/{characterID}_data.tres");

        private string NameFor(string characterID) {
            CharacterData data = DataFor(characterID);
            return data == null || string.IsNullOrWhiteSpace(data.DisplayNameKey)
                ? Tr("common_unknown")
                : Tr(data.DisplayNameKey);
        }

        private string StatsFor(string characterID) {
            CharacterData data = DataFor(characterID);
            if (data == null) return Tr("fighter_stats_unavailable");
            return string.Format(
                Tr("fighter_stats_summary"),
                data.MaxHP, data.MaxMoveSpeed, data.BasicAttackDamage, data.BasicAttackKnockback,
                data.Weight, data.MaxBlockCharges, data.Style, data.MaxJumpForce, data.MaxJumpCount);
        }

        private static Texture2D PortraitFor(string characterID) => DataFor(characterID)?.CharacterPortrait;

        /// <summary>
        /// The tile's short name (UI_SPEC_GLASS 3.7: <c>character_&lt;id&gt;_card_name</c>, as the Fighter select's tiles), else
        /// the display name.
        /// </summary>
        private string CardNameFor(string characterID) {
            string key = $"character_{characterID}_card_name";
            string card = Tr(key);
            return card != key && !string.IsNullOrWhiteSpace(card) ? card : NameFor(characterID);
        }

        /// <summary>
        /// The select tile treatment (UI_SPEC_GLASS 3.7) on a picker button: the button draws nothing of its own; a
        /// <see cref="FTT.UI.Pulp.GlassTileDecor"/> draws the 164 px glass tile, its portrait window (the art at 150 %, a
        /// face crop) and the focus edge and glow, and the short name (<c>CardName</c>, T35) sits centred 11 px above the
        /// tile's bottom, shrunk to its 156 px box at a large UI scale (the built <see cref="CardTextFit"/> rule).
        /// </summary>
        private static void DressTile(Button button, Texture2D portrait, string cardName) {
            button.CustomMinimumSize = new Vector2(TileSize, TileSize);
            foreach (string slot in TileBoxSlots) button.AddThemeStyleboxOverride(slot, TileBox);
            FTT.UI.Pulp.GlassTileDecor decor = FTT.UI.Pulp.GlassTileDecor.Attach(button, FTT.UI.Pulp.GlassTileDecor.SelectChamfer);
            decor.Portrait = portrait;

            var nameLabel = new Label {
                Name = "CharacterName",
                Text = cardName,
                Uppercase = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
                AutoTranslateMode = AutoTranslateModeEnum.Disabled,
                ThemeTypeVariation = UIPalette.LabelRoles.CardName
            };
            button.AddChild(nameLabel);
            void Fit() {
                if (!IsInstanceValid(nameLabel)) return;
                float height = nameLabel.GetMinimumSize().Y;
                nameLabel.Size = new Vector2(nameLabel.Size.X, height);
                nameLabel.Position = new Vector2(nameLabel.Position.X, TileSize - NameBottomInset - height);
                CardTextFit.Fit(nameLabel, (TileSize - NameBoxWidth) / 2f, NameBoxWidth, centred: true);
            }
            nameLabel.ThemeChanged += () => Callable.From(Fit).CallDeferred();
            Callable.From(Fit).CallDeferred();
        }
    }
}
