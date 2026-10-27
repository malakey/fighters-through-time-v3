using Godot;
using FTT.Core;
using FTT.Environment;
using FTT.FighterSim;
using FTT.UI.Pulp;

namespace FTT.UI {

    /// <summary>
    /// Post-match win/loss screen (design-godot.md Section 11 "Post-Match
    /// Results" / "Return and Lobby Flow"). Replaces the panel the Test Arena
    /// HUD used to build in code, and — unlike that panel — rematch resolves the
    /// destination through the stage catalog, so the selected stage survives a
    /// rematch instead of dumping every match back into the Test Arena.
    /// </summary>
    public partial class MatchResults : CanvasLayer {
        private Control _root;
        private Control _panel;
        private Label _stamp;
        private Label _outcome;
        private Label _stocksLostOne;
        private Label _stocksLostTwo;
        private Label _suddenDeathNote;
        private readonly PulpPortraitRoundel[] _roundels = new PulpPortraitRoundel[2];
        private readonly Label[] _fighterNames = new Label[2];
        private readonly Control[] _fighterColumns = new Control[2];
        private readonly PulpIcon[] _winnerStars = new PulpIcon[2];
        private Button _rematchButton;
        private bool _shown;
        // V7 "Rematch requires both": in local-human matches the Rematch press
        // arms a vote and each player confirms with their own Attack press.
        private bool _rematchPending;
        private readonly bool[] _rematchReady = new bool[2];

        /// <summary>True while the both-players Rematch vote is armed. Test seam.</summary>
        public bool RematchVotePending => _rematchPending;

        /// <summary>True once a result has been presented. Test seam.</summary>
        public bool IsShowing => _shown;

        /// <summary>Localized outcome line currently displayed. Test seam.</summary>
        public string OutcomeText => _outcome?.Text ?? "";

        /// <summary>Stamp colour currently in force. Test seam.</summary>
        public Color StampColor => _stamp != null ? _stamp.GetThemeColor("font_color") : default;

        /// <summary>The two F21 stocks-lost lines, in player order. Test seam.</summary>
        public string StocksLostTextOne => _stocksLostOne?.Text ?? "";

        /// <inheritdoc cref="StocksLostTextOne"/>
        public string StocksLostTextTwo => _stocksLostTwo?.Text ?? "";

        /// <summary>Whether the stocks-lost totals are currently displayed. Test seam.</summary>
        public bool StocksLostVisible => _stocksLostOne?.Visible == true;

        public override void _Ready() {
            Layer = 90;
            ProcessMode = ProcessModeEnum.Always;
            BuildUI();
        }

        public void ShowResult(FighterMatchResult result) {
            if (_shown) return;
            _shown = true;
            if (_root != null) _root.Visible = true;
            if (_stamp != null) {
                _stamp.Text = result.IsTrueTie ? Tr("match_draw") : Tr("match_ko");
                _stamp.AddThemeColorOverride(
                    "font_color",
                    result.IsTrueTie ? UIPalette.Slate : UIPalette.BossRed);
                // UI_SPEC_GLASS 9.2: a draw reads as one line, "Draw" in ink; the slate stamp would only repeat it.
                _stamp.Visible = !result.IsTrueTie;
            }
            if (_outcome != null) {
                _outcome.Text = result.IsTrueTie
                    ? Tr("fighter_results_draw")
                    : string.Format(Tr("fighter_results_winner"), result.WinnerPlayerID + 1);
                if (result.IsTrueTie) _outcome.AddThemeColorOverride("font_color", UIPalette.Ink);
                else _outcome.RemoveThemeColorOverride("font_color");
            }
            ApplyWinnerShape(result);
            ApplyFighters(result);
            ApplyStocksLostTotals(result);
            // Focus is authored only once the panel is on screen: a chain built
            // against a hidden subtree collects nothing (FocusChainBuilder skips
            // invisible branches), and a controller player would land on nothing.
            FocusChainBuilder.Apply(_panel);
            GlassScreenDressing.FadeIn(_panel);
        }

        /// <summary>The roundel portrait of player <paramref name="player"/> (0 or 1). Test seam.</summary>
        internal PulpPortraitRoundel RoundelFor(int player) => _roundels[player];

        /// <summary>The star on the winner's column (visible only on a decided match). Test seam.</summary>
        internal bool WinnerStarVisible(int player) => _winnerStars[player]?.Visible == true;

        /// <summary>
        /// UR-SCREENS: both fighters on portrait roundels with their slot shape and colour (the shape,
        /// not the colour alone, tells player one from player two), the winner marked with a star and the
        /// loser dimmed.
        /// </summary>
        private void ApplyFighters(FighterMatchResult result) {
            SessionData session = GameManager.Instance?.CurrentSession ?? default;
            string[] ids = { session.SelectedCharacterID, session.OpponentCharacterID };
            for (int player = 0; player < 2; player++) {
                FTT.Characters.CharacterData data = LoadCharacter(ids[player]);
                if (_roundels[player] != null) _roundels[player].Portrait = data?.CharacterPortrait;
                if (_fighterNames[player] != null) {
                    _fighterNames[player].Text = data == null || string.IsNullOrWhiteSpace(data.DisplayNameKey)
                        ? string.Format(Tr("fighter_results_player"), player + 1)
                        : Tr(data.DisplayNameKey);
                }
                bool won = !result.IsTrueTie && result.WinnerPlayerID == player;
                bool lost = !result.IsTrueTie && result.WinnerPlayerID >= 0 && result.WinnerPlayerID != player;
                if (_winnerStars[player] != null) _winnerStars[player].Visible = won;
                if (_fighterColumns[player] != null) {
                    _fighterColumns[player].Modulate = lost ? new Color(1f, 1f, 1f, 0.62f) : Colors.White;
                }
            }
        }

        private static FTT.Characters.CharacterData LoadCharacter(string characterID) {
            if (string.IsNullOrWhiteSpace(characterID)) return null;
            string path = $"res://resources/Characters/{characterID}_data.tres";
            return ResourceLoader.Exists(path) ? AuthoredResources.Load<FTT.Characters.CharacterData>(path) : null;
        }

        /// <summary>
        /// F21's results lines (Package 11 A1c). Time mode is decided by fewest
        /// stocks lost, so the totals that decided it are shown — the regulation
        /// totals, frozen at Sudden Death entry, never the decider's own life.
        ///
        /// <para>Stock mode hides them: its own elimination and HP-percentage rules
        /// decided the match, and a "stocks lost" line there would read as the
        /// deciding quantity when it is not. The contract forbids labelling this
        /// KOs scored, points or remaining lives — lower is better.</para>
        /// </summary>
        private void ApplyStocksLostTotals(FighterMatchResult result) {
            bool timeMode = FighterHudModel.UsesStocksLostDisplay(result.MatchMode);
            if (_stocksLostOne != null) {
                _stocksLostOne.Visible = timeMode;
                ResultRow.Attach(_stocksLostOne).SetText(Tr("fighter_results_stocks_lost"), 1, result.PlayerOneStocksLost);
            }
            if (_stocksLostTwo != null) {
                _stocksLostTwo.Visible = timeMode;
                ResultRow.Attach(_stocksLostTwo).SetText(Tr("fighter_results_stocks_lost"), 2, result.PlayerTwoStocksLost);
            }
            if (_suddenDeathNote != null) {
                _suddenDeathNote.Visible = result.DecidedInSuddenDeath;
                _suddenDeathNote.Text = Tr("fighter_results_sudden_death");
            }
        }

        /// <summary>
        /// Rematch destination: the scene the selected stage actually lives in.
        /// Falls back to the Test Arena only when no catalog entry resolves.
        /// </summary>
        public static string RematchScenePath() {
            string stageID = GameManager.Instance?.CurrentSession.SelectedStageID ?? "";
            FighterStageCatalog catalog = FighterStageCatalog.LoadDefault();
            FighterStageData stage = catalog?.Find(stageID) ?? catalog?.FirstPlayable();
            if (stage != null
                && !string.IsNullOrWhiteSpace(stage.ScenePath)
                && ResourceLoader.Exists(stage.ScenePath)) {
                return stage.ScenePath;
            }
            return "res://scenes/arenas/TestArena.tscn";
        }

        /// <summary>
        /// Where the results screen's exit button leads, by match origin (M26):
        /// the hub for the Holodeck, the Fighter menu for Versus CPU, the main menu
        /// for Local Versus. Read-only — it does not touch the session.
        /// </summary>
        public static string ExitScenePath() {
            SessionData session = GameManager.Instance?.CurrentSession ?? default;
            return FighterFlowRoutes.ResultsDestination(FighterResultsAction.Exit, ref session);
        }

        /// <summary>
        /// Resolves a results button (everything but Rematch) through
        /// <see cref="FighterFlowRoutes"/>, commits the session it prepared and
        /// returns the destination. Split from the handler so a test can assert a
        /// route without a real scene change.
        /// </summary>
        public static string ApplyResultsAction(FighterResultsAction action) {
            GameManager manager = GameManager.Instance;
            if (manager == null) return null;
            SessionData session = manager.CurrentSession;
            string destination = FighterFlowRoutes.ResultsDestination(action, ref session);
            manager.CurrentSession = session;
            return destination;
        }

        /// <summary>The results button names, in display order. Test seam.</summary>
        public System.Collections.Generic.IReadOnlyList<string> ActionButtonNames => _actionButtonNames;
        private readonly System.Collections.Generic.List<string> _actionButtonNames = new();

        /// <summary>The fighter slabs' width (UI_SPEC_GLASS 9.2: "two glass slabs 600 wide, left and right").</summary>
        public const float FighterSlabWidth = 600f;

        /// <summary>The portrait frame in each fighter slab (9.2: 110).</summary>
        public const float PortraitFrame = 110f;

        /// <summary>The winner's slot shape beside the outcome line, and its slot-colour glow (9.2: 14 px).</summary>
        public const float WinnerShapeSize = 30f;
        public const int WinnerGlowSize = 14;

        private Control _winnerShapeHost;
        private PulpIcon _winnerShape;
        private StyleBoxFlat _winnerGlow;

        /// <summary>
        /// TG-SCREENS (UI_SPEC_GLASS 9.2, match results): full-screen over the dimmed stage (the .70 dim, no streak: it sits
        /// over gameplay, plan 0.4 P3). The heading <c>fighter_results_title</c> in T03 between flourishes at y 40; the KO or
        /// draw stamp (kept: its role and colours are pinned); the outcome line with the winner's slot shape and its glow;
        /// two 600 px glass slabs, each a 110 px frame, the slot shape and the hero name (T09) and the per-player numbers as
        /// results rows; the buttons as compact chevrons, centred, at least 34 px apart (the derived row gap); the prompt bar
        /// bottom-centre.
        /// </summary>
        private void BuildUI() {
            var shade = new ColorRect {
                Name = "Shade",
                Color = UIPalette.PauseDim,
                MouseFilter = Control.MouseFilterEnum.Stop,
                Visible = false
            };
            shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            UIPalette.ApplyTheme(shade);
            AddChild(shade);
            _root = shade;

            GlassScreenDressing.AddScreenHeading(shade, "fighter_results_title");

            var center = new CenterContainer { Name = "Center", MouseFilter = Control.MouseFilterEnum.Ignore };
            center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            shade.AddChild(center);

            var layout = new VBoxContainer { Name = "Panel", Alignment = BoxContainer.AlignmentMode.Center };
            layout.AddThemeConstantOverride("separation", UIPalette.PanelSeparation);
            center.AddChild(layout);
            // The heading at y 40 above, the prompt bar below: the larger of the two reserves (the fit is symmetric).
            MenuSafeAreaFit.Attach(layout, GlassScreenDressing.ScaledHeadingReserve);
            _panel = layout;

            // V7.3 UI-scale pass: the stamp rides the TitleLabel role — its old
            // 56 px override froze it out of the accessibility scale. On the glass
            // results it keeps its role and its KO red / draw slate, upper-case, with
            // the scene halo (it stands on the dim, not on a slab).
            _stamp = new Label { Name = "Stamp", HorizontalAlignment = HorizontalAlignment.Center, Uppercase = true };
            _stamp.ThemeTypeVariation = UIPalette.TitleLabelVariation;
            GlassScreenDressing.ApplySceneHalo(_stamp, large: true);
            layout.AddChild(_stamp);

            // The outcome line, led by the winner's slot shape and its glow (hidden on a draw).
            var outcomeRow = new HBoxContainer { Name = "OutcomeRow", Alignment = BoxContainer.AlignmentMode.Center };
            outcomeRow.AddThemeConstantOverride("separation", 14);
            layout.AddChild(outcomeRow);
            _winnerShapeHost = new WinnerShapeHost(this) {
                Name = "WinnerShape", CustomMinimumSize = new Vector2(WinnerShapeSize, WinnerShapeSize),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _winnerGlow = new StyleBoxFlat {
                DrawCenter = false, BgColor = new Color(0f, 0f, 0f, 0f), ShadowSize = WinnerGlowSize, AntiAliasing = true
            };
            _winnerGlow.SetCornerRadiusAll(Mathf.RoundToInt(WinnerShapeSize / 2f));
            _winnerShape = PulpIcon.Create("slot_p1", UIPalette.OwnerOnDark(0), WinnerShapeSize);
            _winnerShape.Name = "Shape";
            _winnerShapeHost.AddChild(_winnerShape);
            outcomeRow.AddChild(_winnerShapeHost);
            _outcome = new Label {
                Name = "Outcome",
                HorizontalAlignment = HorizontalAlignment.Center,
                ThemeTypeVariation = UIPalette.LabelRoles.SideCardName,   // T40 (9.2), halo L from the role
                Uppercase = true
            };
            outcomeRow.AddChild(_outcome);

            // The two fighters on glass slabs. The totals Labels keep their names and texts (the F21 test seams).
            var fighters = new HBoxContainer {
                Name = "Fighters", Alignment = BoxContainer.AlignmentMode.Center
            };
            fighters.AddThemeConstantOverride("separation", 40);
            layout.AddChild(fighters);
            _stocksLostOne = MakeTotalsLine("StocksLostOne");
            _stocksLostTwo = MakeTotalsLine("StocksLostTwo");
            fighters.AddChild(BuildFighterColumn(0, _stocksLostOne));
            fighters.AddChild(BuildFighterColumn(1, _stocksLostTwo));

            _suddenDeathNote = MakeTotalsLine("SuddenDeathNote");
            _suddenDeathNote.ThemeTypeVariation = UIPalette.LabelRoles.SceneCaption;
            _suddenDeathNote.Uppercase = true;
            layout.AddChild(_suddenDeathNote);

            // M26 (Package 12 W5): the buttons follow the match origin. Local
            // Versus keeps Rematch / New Stage / Change Fighters / Main Menu;
            // Versus CPU routes New Stage to the CPU configuration panel, Change
            // Fighters to the P1-only select and its exit to the Fighter menu; a
            // Holodeck bout offers Rematch / Reconfigure / Return to Ship. Any
            // player can trigger the non-Rematch choices; only Rematch votes.
            FighterMatchOrigin origin = GameManager.Instance?.CurrentSession.FighterMatchOrigin
                ?? FighterMatchOrigin.LocalVersus;
            _actionButtonNames.Clear();
            var buttonRow = new HBoxContainer { Name = "ButtonRow", Alignment = BoxContainer.AlignmentMode.Center };
            layout.AddChild(buttonRow);
            var buttons = new System.Collections.Generic.List<Button>();
            foreach (FighterResultsAction action in FighterFlowRoutes.ResultsActions(origin)) {
                var button = MakeButton(Tr(FighterFlowRoutes.ResultsLabelKey(action, origin)));
                button.Name = action + "Button";
                _actionButtonNames.Add(button.Name);
                if (action == FighterResultsAction.Rematch) {
                    button.Pressed += OnRematchPressed;
                    _rematchButton = button;
                } else {
                    FighterResultsAction captured = action;
                    button.Pressed += () => {
                        string destination = ApplyResultsAction(captured);
                        if (destination != null) GameManager.Instance?.LoadScene(destination);
                    };
                }
                buttonRow.AddChild(button);
                buttons.Add(button);
                PulpButtonDecor.Apply(button, compact: true).ShowPrompt = false;
            }
            // D6 (UI_SPEC_GLASS 3.2): the row shares its widest required width (at least 290; a compact row shows no prompt,
            // the prompt bar carries it), and its gap is the derived focus-row gap (at least 34, 9.1 item 4).
            PulpButtonDecor.ApplyStackWidth(buttons, 290f, reservePrompt: false);
            PulpScreenKit.SeparateFocusRow(buttonRow);
            GlassScreenDressing.AddOverlayPromptBar(shade, ("ui_accept", "ui_prompt_select"));
        }

        /// <summary>
        /// One fighter's 600 px glass slab: the 110 px portrait frame beside a column of the slot shape and the hero name
        /// (T09), the winner's star, and the per-player numbers as results rows (the F21 stocks-lost line in Time mode).
        /// The slab is the <c>Fighter1</c> / <c>Fighter2</c> node the loser's dimming reads.
        /// </summary>
        private Control BuildFighterColumn(int player, Label stocksLost) {
            var slab = new PanelContainer { Name = "Fighter" + (player + 1), CustomMinimumSize = new Vector2(FighterSlabWidth, 0f) };
            PulpScreenKit.DressPanel(slab);
            _fighterColumns[player] = slab;

            var body = new HBoxContainer { Name = "Body" };
            body.AddThemeConstantOverride("separation", 22);
            slab.AddChild(body);

            var roundel = new PulpPortraitRoundel { Name = "Roundel", Diameter = PortraitFrame, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            body.AddChild(roundel);
            _roundels[player] = roundel;

            var column = new VBoxContainer { Name = "Column", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
            column.AddThemeConstantOverride("separation", 8);
            body.AddChild(column);

            var row = new HBoxContainer { Name = "NameRow" };
            row.AddThemeConstantOverride("separation", 12);
            Color slot = UIPalette.OwnerOnDark(player);
            PulpIcon shape = PulpIcon.Create(player == 0 ? "slot_p1" : "slot_p2", slot, 30f);
            shape.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            row.AddChild(shape);
            var name = new Label {
                Name = "FighterName", ThemeTypeVariation = UIPalette.LabelRoles.HeroNameScene, Uppercase = true,
                VerticalAlignment = VerticalAlignment.Center, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                // A long name ("William Shakespeare" at T09) wraps to a second line rather than losing its end.
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            row.AddChild(name);
            var star = PulpIcon.Create("star4_filled", UIPalette.Accent, 28f);
            star.Name = "WinnerStar";
            star.Visible = false;
            star.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            row.AddChild(star);
            _fighterNames[player] = name;
            _winnerStars[player] = star;
            column.AddChild(row);

            column.AddChild(stocksLost);
            ResultRow.Attach(stocksLost);
            return slab;
        }

        /// <summary>The winner's slot shape with a soft glow in its slot colour behind it (drawn without allocating).</summary>
        private sealed partial class WinnerShapeHost : Control {
            private readonly MatchResults _owner;
            public WinnerShapeHost(MatchResults owner) { _owner = owner; }
            public WinnerShapeHost() { }
            public override void _Draw() {
                if (_owner?._winnerGlow != null) DrawStyleBox(_owner._winnerGlow, new Rect2(Vector2.Zero, Size).Grow(-4f));
            }
        }

        /// <summary>Shows the winner's shape and glow, or hides both on a draw.</summary>
        private void ApplyWinnerShape(FighterMatchResult result) {
            if (_winnerShapeHost == null) return;
            bool decided = !result.IsTrueTie && result.WinnerPlayerID is 0 or 1;
            _winnerShapeHost.Visible = decided;
            if (!decided) return;
            int player = result.WinnerPlayerID;
            Color slot = UIPalette.OwnerOnDark(player);
            _winnerShape.IconName = player == 0 ? "slot_p1" : "slot_p2";
            _winnerShape.Tint = slot;
            _winnerGlow.ShadowColor = new Color(slot, 0.6f);
            _winnerShapeHost.QueueRedraw();
        }

        private void OnRematchPressed() {
            bool localHuman = GameManager.Instance?.CurrentSession.FighterOpponentType
                == FighterOpponentType.LocalHuman;
            if (!localHuman) {
                GameManager.Instance?.LoadScene(RematchScenePath());
                return;
            }
            // Arm the vote (design Section 11: "Rematch requires both"); each
            // player confirms on their own device, Interact cancels the vote.
            _rematchPending = true;
            _rematchReady[0] = false;
            _rematchReady[1] = false;
            if (_rematchButton != null) _rematchButton.Text = Tr("fighter_rematch_waiting");
        }

        public override void _Process(double delta) {
            if (!_rematchPending || InputManager.Instance == null) return;
            for (int player = 0; player < 2; player++) {
                if (InputManager.Instance.IsActionJustPressed("gameplay_basic_attack", player)) {
                    _rematchReady[player] = true;
                }
                if (InputManager.Instance.IsActionJustPressed("gameplay_interact", player)) {
                    _rematchPending = false;
                    if (_rematchButton != null) _rematchButton.Text = Tr("fighter_rematch");
                    return;
                }
            }
            if (_rematchReady[0] && _rematchReady[1]) {
                _rematchPending = false;
                GameManager.Instance?.LoadScene(RematchScenePath());
            }
        }

        /// <summary>
        /// A totals line. Hidden until <see cref="ShowResult"/> decides it applies,
        /// and on the SmallLabel role rather than a font-size override so it follows
        /// the accessibility UI scale (V7.3 type-scale rule).
        /// </summary>
        private static Label MakeTotalsLine(string name) => new() {
            Name = name,
            HorizontalAlignment = HorizontalAlignment.Center,
            ThemeTypeVariation = UIPalette.SmallLabelVariation,
            Visible = false
        };

        private static Button MakeButton(string text) => new() {
            Text = text,
            ThemeTypeVariation = UIPalette.Variations.PulpButtonCompact,
            CustomMinimumSize = new Vector2(0f, 58f)
        };
    }
}
