using Godot;
using FTT.Core;
using FTT.Environment;
using FTT.FighterSim;

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
            }
            if (_outcome != null) {
                _outcome.Text = result.IsTrueTie
                    ? Tr("fighter_results_draw")
                    : string.Format(Tr("fighter_results_winner"), result.WinnerPlayerID + 1);
            }
            ApplyStocksLostTotals(result);
            // Focus is authored only once the panel is on screen: a chain built
            // against a hidden subtree collects nothing (FocusChainBuilder skips
            // invisible branches), and a controller player would land on nothing.
            FocusChainBuilder.Apply(_panel);
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
                _stocksLostOne.Text = string.Format(
                    Tr("fighter_results_stocks_lost"), 1, result.PlayerOneStocksLost);
            }
            if (_stocksLostTwo != null) {
                _stocksLostTwo.Visible = timeMode;
                _stocksLostTwo.Text = string.Format(
                    Tr("fighter_results_stocks_lost"), 2, result.PlayerTwoStocksLost);
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

        private void BuildUI() {
            var shade = new ColorRect {
                Name = "Shade",
                Color = UIPalette.Shade,
                MouseFilter = Control.MouseFilterEnum.Stop,
                Visible = false
            };
            shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            UIPalette.ApplyTheme(shade);
            AddChild(shade);
            _root = shade;

            var center = new CenterContainer();
            center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            shade.AddChild(center);

            var panel = new PanelContainer { Name = "Panel", CustomMinimumSize = new Vector2(560, 360) };
            center.AddChild(panel);
            _panel = panel;

            var layout = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            layout.AddThemeConstantOverride("separation", UIPalette.PanelSeparation);
            panel.AddChild(layout);

            // V7.3 UI-scale pass: the stamp rides the TitleLabel role — its old
            // 56 px override froze it out of the accessibility scale, and the
            // type scale deliberately has no display size beyond Title. The
            // outline keeps the stamp reading as a stamp.
            _stamp = new Label { Name = "Stamp", HorizontalAlignment = HorizontalAlignment.Center };
            _stamp.ThemeTypeVariation = UIPalette.TitleLabelVariation;
            _stamp.AddThemeColorOverride("font_outline_color", UIPalette.NavyDeep);
            _stamp.AddThemeConstantOverride("outline_size", 6);
            layout.AddChild(_stamp);

            var title = new Label {
                Text = Tr("fighter_results_title"),
                HorizontalAlignment = HorizontalAlignment.Center,
                ThemeTypeVariation = UIPalette.TitleLabelVariation
            };
            title.AddThemeColorOverride("font_color", UIPalette.TextAccent);
            layout.AddChild(title);

            _outcome = new Label {
                Name = "Outcome",
                HorizontalAlignment = HorizontalAlignment.Center,
                ThemeTypeVariation = UIPalette.HeadingLabelVariation
            };
            layout.AddChild(_outcome);

            _stocksLostOne = MakeTotalsLine("StocksLostOne");
            layout.AddChild(_stocksLostOne);
            _stocksLostTwo = MakeTotalsLine("StocksLostTwo");
            layout.AddChild(_stocksLostTwo);
            _suddenDeathNote = MakeTotalsLine("SuddenDeathNote");
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
                layout.AddChild(button);
            }
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
            CustomMinimumSize = new Vector2(UIPalette.ButtonMinWidth, UIPalette.ButtonMinHeight)
        };
    }
}
