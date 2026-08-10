using System.Collections.Generic;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// Story Mode pause menu. Resume / Settings / Quit-to-menu, the last one behind
    /// a confirmation because it discards everything since the last checkpoint.
    ///
    /// Package 8 A1 moved this onto the authored, themed
    /// <c>res://scenes/ui/StoryPause.tscn</c> and onto <see cref="PauseMenuBase"/>.
    /// The code-built fallback below is kept deliberately: campaign scenes attach
    /// this through <see cref="FTT.Environment.StorySceneBootstrapper"/>, and a
    /// missing or broken scene resource must degrade to a working menu rather than
    /// leave a paused campaign with no way out.
    /// </summary>
    public partial class PauseMenu : PauseMenuBase {

        public const string SceneResourcePath = "res://scenes/ui/StoryPause.tscn";

        private Control _root;
        private Control _menuPanel;
        private ConfirmModal _quitConfirm;
        private SettingsMenu _settingsMenu;
        private List<Control> _focusChain = new();

        /// <summary>
        /// Instantiates the authored scene, falling back to a code-built menu when
        /// it is unavailable. Mirrors <see cref="StoryHUD.CreateDefault"/>.
        /// </summary>
        public static PauseMenu CreateDefault() {
            if (ResourceLoader.Exists(SceneResourcePath)) {
                var packed = ResourceLoader.Load<PackedScene>(SceneResourcePath);
                if (packed?.Instantiate() is PauseMenu authored) {
                    // Both paths produce the same node name so campaign scene trees
                    // are identical whether or not the authored scene resolved.
                    authored.Name = "PauseMenu";
                    return authored;
                }
            }
            return new PauseMenu { Name = "PauseMenu" };
        }

        public override void _Ready() {
            Layer = 99;
            ProcessMode = ProcessModeEnum.Always;
            ResolveOrBuildUI();
            BuildQuitConfirmation();
            if (_root != null) _root.Visible = false;
        }

        private void ResolveOrBuildUI() {
            _root = GetNodeOrNull<Control>("Root");
            if (_root != null) {
                _menuPanel = _root.GetNodeOrNull<Control>("Center/Panel");
                WireButton("Root/Center/Panel/Layout/ResumeButton", () => SetPaused(false));
                WireButton("Root/Center/Panel/Layout/SettingsButton", OpenSettings);
                WireButton("Root/Center/Panel/Layout/QuitButton", ShowQuitConfirmation);
                return;
            }
            BuildFallbackUI();
        }

        private void WireButton(string path, System.Action handler) {
            var button = GetNodeOrNull<Button>(path);
            if (button != null) button.Pressed += handler;
        }

        private void BuildFallbackUI() {
            _root = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Stop };
            _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            UIPalette.ApplyTheme(_root);
            AddChild(_root);

            var shade = new ColorRect { Name = "Shade", Color = UIPalette.Shade };
            shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _root.AddChild(shade);

            var center = new CenterContainer { Name = "Center" };
            center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _root.AddChild(center);

            var panel = new PanelContainer { Name = "Panel" };
            panel.CustomMinimumSize = new Vector2(420, 0);
            center.AddChild(panel);
            _menuPanel = panel;

            var layout = new VBoxContainer { Name = "Layout" };
            layout.AddThemeConstantOverride("separation", UIPalette.PanelSeparation);
            panel.AddChild(layout);

            var title = new Label {
                Name = "Title",
                Text = "menu_paused",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            title.AddThemeColorOverride("font_color", UIPalette.TextAccent);
            title.AddThemeFontSizeOverride("font_size", UIPalette.TitleFontSize);
            layout.AddChild(title);

            layout.AddChild(MakeButton("ResumeButton", "menu_resume", () => SetPaused(false)));
            layout.AddChild(MakeButton("SettingsButton", "menu_settings", OpenSettings));
            layout.AddChild(MakeButton("QuitButton", "pause_quit_to_menu", ShowQuitConfirmation));
        }

        private static Button MakeButton(string name, string textKey, System.Action handler) {
            var button = new Button {
                Name = name,
                Text = textKey,
                CustomMinimumSize = new Vector2(UIPalette.ButtonMinWidth, UIPalette.ButtonMinHeight)
            };
            button.Pressed += handler;
            return button;
        }

        private void BuildQuitConfirmation() {
            if (_root == null) return;
            _quitConfirm = ConfirmModal.Create("pause_quit_confirm");
            _quitConfirm.Confirmed += QuitToMainMenu;
            _quitConfirm.Cancelled += RestoreMenuFocus;
            _root.AddChild(_quitConfirm);
        }

        private void ShowQuitConfirmation() {
            if (_quitConfirm == null) {
                QuitToMainMenu();
                return;
            }
            _quitConfirm.Open();
        }

        private void QuitToMainMenu() {
            SetPaused(false);
            FTT.Core.GameManager.Instance?.LoadScene("res://scenes/menus/MainMenu.tscn");
        }

        private void RestoreMenuFocus() => FocusChainBuilder.GrabInitialFocus(_focusChain);

        /// <summary>
        /// The confirmation owns the player while it is open, so the pause button
        /// must not resume out from under it.
        /// </summary>
        protected override bool CanTogglePause() => _quitConfirm?.IsOpen != true;

        protected override void OnPauseStateChanged(bool paused) {
            if (_root != null) _root.Visible = paused;

            if (!paused) {
                _quitConfirm?.Close();
                return;
            }

            // Rebuilt on every open: the chain has to reflect what is actually
            // visible now, and the modal's buttons must not join the menu's chain.
            _focusChain = FocusChainBuilder.Build(_menuPanel);
            FocusChainBuilder.GrabInitialFocus(_focusChain);
        }

        private void OpenSettings() {
            if (_settingsMenu == null || !IsInstanceValid(_settingsMenu)) {
                _settingsMenu = new SettingsMenu { Name = "SettingsMenu" };
                AddChild(_settingsMenu);
                // H-10: Settings holds focus while open; hand it back to this
                // menu's chain on close (mirrors MainMenu's restore).
                _settingsMenu.Closed += RestoreMenuFocus;
            }
            _settingsMenu.Show();
        }
    }
}
