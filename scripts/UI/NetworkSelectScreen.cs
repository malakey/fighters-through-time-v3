using FTT.Core;
using FTT.Networking;
using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// V7 Network Select (initial-release LAN): host or join a direct-IP 1v1
    /// over the deterministic rollback session — the exact code path online
    /// will use. Until the Package 7 handshake negotiates rules and seed, both
    /// machines must pick the same characters, stage, and rules; the screen
    /// says so. Continue proceeds to the character select with the session's
    /// opponent type set to Lan.
    /// </summary>
    public partial class NetworkSelectScreen : Control {
        public const string ScenePath = "res://scenes/menus/NetworkSelect.tscn";
        public const int DefaultPort = 27850;

        private LineEdit _addressEdit;
        private Label _statusLabel;
        private Button _continueButton;

        public override void _Ready() {
            var layout = new VBoxContainer {
                Name = "Layout",
                CustomMinimumSize = new Vector2(420f, 0f)
            };
            layout.AddThemeConstantOverride("separation", 12);
            // UR-SCREENS (light pass, de-scoped screen): backdrop and an ink panel around the stack.
            var backdrop = new ColorRect { Name = "Background", Color = UIPalette.Backdrop, MouseFilter = MouseFilterEnum.Ignore };
            backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(backdrop);
            var center = new CenterContainer { Name = "Center", MouseFilter = MouseFilterEnum.Ignore };
            center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(center);
            var panel = new PanelContainer { Name = "Panel", ThemeTypeVariation = UIPalette.Variations.InkPanel };
            center.AddChild(panel);
            panel.AddChild(layout);
            MenuSafeAreaFit.Attach(panel);
            UIPalette.ApplyTheme(this);
            PulpScreenKit.DressPanel(panel, buttonInset: true);

            layout.AddChild(new Label {
                Name = "Title",
                Text = Tr("network_select_title"),
                HorizontalAlignment = HorizontalAlignment.Center,
                ThemeTypeVariation = UIPalette.TitleLabelVariation,
                Uppercase = true
            });
            layout.AddChild(new Label {
                Name = "Notice",
                Text = Tr("network_select_notice"),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            var hostButton = MakeButton("HostButton", "network_host", OnHostPressed);
            layout.AddChild(hostButton);

            _addressEdit = new LineEdit {
                Name = "AddressEdit",
                PlaceholderText = Tr("network_address_placeholder"),
                Text = "127.0.0.1"
            };
            layout.AddChild(_addressEdit);
            layout.AddChild(MakeButton("JoinButton", "network_join", OnJoinPressed));

            _statusLabel = new Label {
                Name = "Status",
                Text = Tr("network_status_offline"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            layout.AddChild(_statusLabel);

            _continueButton = MakeButton("ContinueButton", "network_continue", OnContinuePressed);
            _continueButton.Disabled = true;
            layout.AddChild(_continueButton);
            layout.AddChild(MakeButton("BackButton", "common_back", OnBackPressed));

            hostButton.GrabFocus();
        }

        private static Button MakeButton(string name, string textKey, System.Action handler) {
            var button = new Button {
                Name = name,
                Text = textKey,
                CustomMinimumSize = new Vector2(UIPalette.ButtonMinWidth, 58f),
                ThemeTypeVariation = UIPalette.Variations.PulpButtonCompact
            };
            button.Pressed += () => handler();
            PulpButtonDecor.Apply(button, compact: true);
            return button;
        }

        private void OnHostPressed() {
            bool hosting = NetworkManager.Instance?.HostLan(DefaultPort) == true;
            SetStatus(hosting
                ? string.Format(Tr("network_status_hosting"), DefaultPort)
                : NetworkManager.Instance?.LastError ?? Tr("network_status_offline"));
            if (_continueButton != null) _continueButton.Disabled = !hosting;
        }

        private void OnJoinPressed() {
            string address = _addressEdit?.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(address)) {
                SetStatus(Tr("network_status_need_address"));
                return;
            }
            bool joined = NetworkManager.Instance?.JoinLan(address, DefaultPort) == true;
            SetStatus(joined
                ? string.Format(Tr("network_status_joining"), address)
                : NetworkManager.Instance?.LastError ?? Tr("network_status_offline"));
            if (_continueButton != null) _continueButton.Disabled = !joined;
        }

        private void OnContinuePressed() {
            var gameManager = GameManager.Instance;
            if (gameManager == null) return;
            SessionData session = gameManager.CurrentSession;
            session.FighterOpponentType = FighterOpponentType.Lan;
            gameManager.CurrentSession = session;
            gameManager.LoadScene("res://scenes/menus/CharacterSelect.tscn");
        }

        private void OnBackPressed() {
            NetworkManager.Instance?.Disconnect();
            GameManager.Instance?.LoadScene("res://scenes/menus/MainMenu.tscn");
        }

        private void SetStatus(string text) {
            if (_statusLabel != null) _statusLabel.Text = text;
        }
    }
}
