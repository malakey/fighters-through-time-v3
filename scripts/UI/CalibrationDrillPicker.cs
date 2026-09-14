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

        /// <summary>The locked nine-character roster, in authored grid order.</summary>
        private static readonly string[] RosterIDs = {
            "einstein", "joan", "leonardo", "lincoln", "cleopatra",
            "tesla", "shakespeare", "mozart", "pocahontas"
        };

        private const string Root = "Select/Root/";

        private Label _nameLabel;
        private Label _statsLabel;
        private MoveListScreen _moveList;
        private SystemsCardScreen _systemsCard;
        private readonly Button[] _tiles = new Button[RosterIDs.Length];
        private int _highlighted;

        /// <summary>The character the preview panel is describing. Test surface.</summary>
        public string HighlightedCharacterID => RosterIDs[_highlighted];

        /// <summary>The nine roster IDs this screen offers. Test surface.</summary>
        public static System.Collections.Generic.IReadOnlyList<string> Roster => RosterIDs;

        public override void _Ready() {
            UIPalette.ApplyTheme(this);
            _nameLabel = GetNode<Label>(Root + "Preview/PreviewName");
            _statsLabel = GetNode<Label>(Root + "Preview/PreviewStats");

            var grid = GetNode<GridContainer>(Root + "Grid");
            for (int index = 0; index < RosterIDs.Length; index++) {
                int captured = index;
                var tile = grid.GetNode<Button>($"CharacterButton{index}");
                tile.Text = string.Empty;
                AddPortraitLayer(tile, PortraitFor(RosterIDs[index]), NameFor(RosterIDs[index]));
                // Unlike the Fighter select's cursor-driven tiles, these are plain
                // focusable buttons: one player, one token, so focus *is* the cursor.
                tile.FocusEntered += () => Highlight(captured);
                tile.MouseEntered += () => Highlight(captured);
                tile.Pressed += () => Choose(captured);
                _tiles[index] = tile;
            }

            GetNode<Button>(Root + "ButtonRow/MoveListButton").Pressed += OpenMoveList;
            GetNode<Button>(Root + "ButtonRow/SystemsCardButton").Pressed += OpenSystemsCard;
            GetNode<Button>(Root + "ButtonRow/BackButton").Pressed += Back;

            Highlight(StartingIndex());
            FocusChainBuilder.Apply(GetNode<Control>("Select"));
            _tiles[_highlighted].GrabFocus();
        }

        /// <summary>
        /// Opens on the session's character when there is one, so re-entering the
        /// route lands where the player left it. Never reads a save slot.
        /// </summary>
        private static int StartingIndex() {
            string current = GameManager.Instance?.CurrentSession.SelectedCharacterID;
            int index = System.Array.IndexOf(RosterIDs, current ?? "");
            return index < 0 ? 0 : index;
        }

        private void Highlight(int index) {
            _highlighted = index;
            string characterID = RosterIDs[index];
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
            session.SelectedCharacterID = RosterIDs[index];
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
        /// The Fighter select's tile treatment: the portrait gets its own layer
        /// above the name so a long name cannot squeeze the icon to a sliver.
        /// </summary>
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
                OffsetTop = -40f
            };
            nameLabel.ThemeTypeVariation = UIPalette.SmallLabelVariation;
            button.AddChild(nameLabel);
        }
    }
}
