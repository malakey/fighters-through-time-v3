using System.Text;
using Godot;
using FTT.Characters;
using FTT.Core;
using FTT.Environment;
using FTT.UI.Pulp;

namespace FTT.UI {

    /// <summary>Which loading treatment a destination scene gets.</summary>
    public enum LoadingVariant {
        /// <summary>Menus and anything unrecognised: background plus status copy.</summary>
        Generic,

        /// <summary>Campaign destinations: a Chronal Rift portal framing the level title.</summary>
        StoryPortal,

        /// <summary>Fighter destinations: both portraits, VS, and the stage plate.</summary>
        FighterVersus
    }

    /// <summary>
    /// Package 8 A1. The themed loading screen that replaced
    /// <see cref="GameManager"/>'s code-built navy rectangle.
    ///
    /// A loading screen is the one piece of UI every player sees on every
    /// transition, and it was the least designed thing in the project. It now has
    /// three variants chosen from the destination path alone, so no caller has to
    /// tell it what kind of transition this is — <see cref="GameManager.LoadScene"/>
    /// passes a path and gets the right treatment.
    ///
    /// The two-second minimum display time stays where it was: it is what stops a
    /// warm-cache transition from flashing a screen for three frames.
    ///
    /// All three variants share this script and differ only in authored nodes;
    /// <see cref="Configure"/> fills whichever ones exist, so a variant scene that
    /// omits a node simply skips that treatment instead of failing.
    /// </summary>
    public partial class LoadingScreen : CanvasLayer {

        public const string GenericScenePath = "res://scenes/ui/LoadingScreen.tscn";
        public const string StoryScenePath = "res://scenes/ui/StoryLoading.tscn";
        public const string FighterScenePath = "res://scenes/ui/FighterLoading.tscn";

        /// <summary>Campaign hub: not a numbered level, so it carries the ship's own title.</summary>
        public const string HubTitleKey = "hub_ship_title";

        private const string CampaignSceneDirectory = "/scenes/campaign/";
        private const string FighterSceneDirectory = "/scenes/fighter/";
        private const string TestArenaScene = "res://scenes/arenas/TestArena.tscn";
        private const string CharacterResourceFormat = "res://resources/Characters/{0}_data.tres";

        /// <summary>
        /// Picks the treatment from the destination path.
        ///
        /// The Test Arena is named explicitly rather than by directory: it lives
        /// under <c>scenes/arenas/</c> for historical reasons but is a Fighter
        /// destination in every way that matters to the player.
        /// </summary>
        public static LoadingVariant SelectVariant(string destinationScenePath) {
            if (string.IsNullOrWhiteSpace(destinationScenePath)) return LoadingVariant.Generic;

            string path = destinationScenePath.Replace('\\', '/');
            if (path == TestArenaScene || path.Contains(FighterSceneDirectory)) {
                return LoadingVariant.FighterVersus;
            }
            if (path.Contains(CampaignSceneDirectory)) return LoadingVariant.StoryPortal;
            return LoadingVariant.Generic;
        }

        /// <summary>The scene resource backing a variant.</summary>
        public static string ScenePathForVariant(LoadingVariant variant) => variant switch {
            LoadingVariant.StoryPortal => StoryScenePath,
            LoadingVariant.FighterVersus => FighterScenePath,
            _ => GenericScenePath
        };

        /// <summary>
        /// Derives a campaign scene's title translation key.
        ///
        /// The sixteen level title keys were authored per level with a slug rather
        /// than an index (<c>orleans_level_title</c>, not <c>level_02_title</c>), so
        /// the key comes from the scene filename's era token:
        /// <c>Level_13_ChronalVoid.tscn</c> becomes <c>chronal_void_level_title</c>.
        /// Returns an empty string for a campaign scene with no such token, and the
        /// caller then simply shows no title.
        /// </summary>
        public static string LevelTitleKeyForScene(string campaignScenePath) {
            if (string.IsNullOrWhiteSpace(campaignScenePath)) return "";

            string file = campaignScenePath.Replace('\\', '/').GetFile().GetBaseName();
            if (file == "HubWorld") return HubTitleKey;

            string[] parts = file.Split('_');
            if (parts.Length < 3 || parts[0] != "Level") return "";

            string era = string.Join("_", parts, 2, parts.Length - 2);
            string slug = ToSnakeCase(era);
            return string.IsNullOrEmpty(slug) ? "" : $"{slug}_level_title";
        }

        private static string ToSnakeCase(string pascal) {
            if (string.IsNullOrEmpty(pascal)) return "";
            var builder = new StringBuilder();
            for (int index = 0; index < pascal.Length; index++) {
                char character = pascal[index];
                if (char.IsUpper(character) && index > 0 && pascal[index - 1] != '_') builder.Append('_');
                builder.Append(char.ToLowerInvariant(character));
            }
            return builder.ToString();
        }

        /// <summary>
        /// Instantiates a variant, falling back to a code-built generic screen when
        /// the resource is missing. A broken loading screen must never be able to
        /// strand a scene transition.
        /// </summary>
        public static LoadingScreen Create(LoadingVariant variant) {
            string scenePath = ScenePathForVariant(variant);
            if (ResourceLoader.Exists(scenePath)) {
                var packed = ResourceLoader.Load<PackedScene>(scenePath);
                if (packed?.Instantiate() is LoadingScreen authored) return authored;
            }

            var fallback = new LoadingScreen { Name = "LoadingScreen" };
            fallback.BuildFallbackUI();
            return fallback;
        }

        /// <summary>Instantiates the variant matching a destination path.</summary>
        public static LoadingScreen CreateFor(string destinationScenePath) =>
            Create(SelectVariant(destinationScenePath));

        private Label _titleLabel;
        private Label _stageNameLabel;
        private Control _playerPortrait;
        private Control _opponentPortrait;
        private Label _playerName;
        private Label _opponentName;
        private Control _stageFrame;
        private TextureRect _stagePreview;
        private TitleFlourishes _titleFlourishes;
        private readonly SlotFrameDecor[] _frameDecor = new SlotFrameDecor[4];

        /// <summary>The versus portrait frames' side (UI_SPEC_GLASS 9.2: 320).</summary>
        public const float VersusFrameSize = 320f;

        /// <summary>The plain screen's hourglass (9.2: 64 px in accent2) and its 0.5 Hz breathing glow (static under Reduced).</summary>
        public const float HourglassSize = 64f;
        public const float BreathSeconds = 2f;

        public override void _Ready() {
            Layer = 100;
            ProcessMode = ProcessModeEnum.Always;
            if (GetNodeOrNull<Control>("Root") == null) BuildFallbackUI();
            ResolveVariantNodes();
        }

        /// <summary>
        /// Resolves the optional variant nodes once, on entry, and dresses them (TG-SCREENS, UI_SPEC_GLASS 9.2). Variant
        /// scenes nest them differently, so resolution is by name rather than by path — but it happens exactly once per
        /// screen, never per frame. Every variant is a full-screen menu on the bg0 field with the screen streak.
        /// </summary>
        private void ResolveVariantNodes() {
            _titleLabel = FindControl<Label>("TitleLabel");
            _stageNameLabel = FindControl<Label>("StageNameLabel");
            // The VS portraits are PulpPortraitRoundel frames (a TextureRect still works).
            _playerPortrait = FindControl<Control>("PlayerPortrait");
            _opponentPortrait = FindControl<Control>("OpponentPortrait");
            _playerName = FindControl<Label>("PlayerName");
            _opponentName = FindControl<Label>("OpponentName");
            _stageFrame = FindControl<Control>("StageFrame");
            _stagePreview = FindControl<TextureRect>("StagePreview");

            if (GetNodeOrNull<Control>("Root") is { } root) PulpScreenKit.AddScreenStreak(root);
            if (_titleLabel != null) _titleFlourishes = GlassScreenDressing.FlankTitle(_titleLabel);
            if (FindControl<Label>("VersusLabel") is { } versus) {
                TitleGradient.Attach(versus);
                GlassScreenDressing.ApplySceneHalo(versus, large: true);
            }
            if (_stageFrame is PanelContainer stageSlab) PulpScreenKit.DressPanel(stageSlab);
            DressVersusSide(0, _playerPortrait, "PlayerSlotRow");
            DressVersusSide(1, _opponentPortrait, "OpponentSlotRow");
            if (FindControl<Control>("HourglassHost") is { } hourglass) BuildHourglass(hourglass);
        }

        /// <summary>
        /// One versus side: the 320 px frame with a 2 px edge and a 14 px glow in the slot colour (the glow behind the frame,
        /// the edge over its gold one), and the slot shape before the player label (shape always beside colour, 9.1 item 6).
        /// </summary>
        private void DressVersusSide(int player, Control portrait, string slotRowName) {
            if (portrait is PulpPortraitRoundel frame && portrait.GetParent() is Control host) {
                frame.Diameter = VersusFrameSize;
                Color slot = UIPalette.OwnerOnDark(player);
                var glow = new SlotFrameDecor { Name = "SlotGlow", Glow = true, SlotColor = slot };
                host.AddChild(glow);
                host.MoveChild(glow, 0);
                var edge = new SlotFrameDecor { Name = "SlotEdge", Glow = false, SlotColor = slot };
                host.AddChild(edge);
                _frameDecor[player * 2] = glow;
                _frameDecor[player * 2 + 1] = edge;
            }
            if (FindControl<HBoxContainer>(slotRowName) is { } row) {
                PulpIcon shape = PulpIcon.Create(player == 0 ? "slot_p1" : "slot_p2", UIPalette.OwnerOnDark(player), 26f);
                shape.Name = "SlotShape";
                shape.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
                row.AddChild(shape);
                row.MoveChild(shape, 0);
            }
        }

        /// <summary>The plain screen's hourglass: the bold-free 64 px glyph in accent2 over a glow breathing at 0.5 Hz.</summary>
        private void BuildHourglass(Control host) {
            var glow = new HourglassGlow { Name = "HourglassGlow" };
            host.AddChild(glow);
            glow.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            PulpIcon icon = PulpIcon.Create("hourglass", UIPalette.Accent2, HourglassSize);
            icon.Name = "Hourglass";
            host.AddChild(icon);
            if (FTT.UI.Motion.PulpMotion.Reduced) {
                glow.Modulate = new Color(1f, 1f, 1f, 0.6f);
                return;
            }
            // 0.5 Hz: 1 s up, 1 s down, looping while the screen is shown (it runs while the tree is paused).
            glow.Modulate = new Color(1f, 1f, 1f, 0.25f);
            Tween breath = FTT.UI.Motion.PulpMotion.NewTween(glow).SetLoops();
            breath.TweenProperty(glow, "modulate:a", 1f, BreathSeconds / 2f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            breath.TweenProperty(glow, "modulate:a", 0.25f, BreathSeconds / 2f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        }

        /// <summary>A versus frame's slot-colour glow (behind it) or its 2 px slot-colour edge (over its gold one).</summary>
        private sealed partial class SlotFrameDecor : Control {
            private readonly StyleBoxFlat _box = new() { DrawCenter = false, AntiAliasing = true, CornerDetail = 1, BgColor = new Color(0f, 0f, 0f, 0f) };
            private bool _glow;
            private Color _slot = Colors.White;

            public SlotFrameDecor() {
                MouseFilter = MouseFilterEnum.Ignore;
                SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            }

            public bool Glow { get => _glow; set { _glow = value; Restyle(); } }
            public Color SlotColor { get => _slot; set { _slot = value; Restyle(); } }

            private void Restyle() {
                int cut = Mathf.RoundToInt(VersusFrameSize * PulpPortraitRoundel.ChamferShare);
                _box.CornerRadiusTopLeft = cut;
                _box.CornerRadiusBottomRight = cut;
                if (_glow) {
                    _box.SetBorderWidthAll(0);
                    _box.ShadowSize = GlowSize;
                    _box.ShadowColor = new Color(_slot, 0.6f);
                } else {
                    _box.SetBorderWidthAll(Mathf.RoundToInt(PulpPortraitRoundel.EdgeWidth));
                    _box.BorderColor = _slot;
                    _box.ShadowSize = 0;
                }
                QueueRedraw();
            }

            public const int GlowSize = 14;

            public override void _Draw() => DrawStyleBox(_box, new Rect2(Vector2.Zero, Size));
        }

        /// <summary>
        /// The hourglass's soft accent2 glow (its alpha breathes): a round shadow whose inside is filled too. A StyleBoxFlat
        /// that does not draw its centre leaves the shadow's inside empty, a dark hole ringed in light, and the hourglass's
        /// open shapes showed it; the centre is drawn (transparent), so the shadow fills the disc and feathers out 22 px.
        /// </summary>
        private sealed partial class HourglassGlow : Control {
            private readonly StyleBoxFlat _box = new() {
                DrawCenter = true, AntiAliasing = true, BgColor = new Color(0f, 0f, 0f, 0f),
                ShadowSize = 22, ShadowColor = new Color(UIPalette.Accent2, 0.35f)
            };
            public HourglassGlow() {
                MouseFilter = MouseFilterEnum.Ignore;
                _box.SetCornerRadiusAll(22);
            }
            public override void _Draw() => DrawStyleBox(_box, new Rect2(Vector2.Zero, Size).Grow(-10f));
        }

        private void BuildFallbackUI() {
            var root = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Stop };
            root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            UIPalette.ApplyTheme(root);
            AddChild(root);

            var background = new ColorRect { Name = "Background", Color = UIPalette.Backdrop };
            background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            root.AddChild(background);

            var status = new Label {
                Name = "StatusLabel",
                Text = "loading",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            status.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            status.ThemeTypeVariation = UIPalette.HeadingLabelVariation;
            root.AddChild(status);
        }

        /// <summary>
        /// Fills whichever variant nodes this scene actually has from the pending
        /// destination and the live session.
        /// </summary>
        public void Configure(string destinationScenePath, SessionData session) {
            switch (SelectVariant(destinationScenePath)) {
                case LoadingVariant.StoryPortal:
                    ConfigureStoryPortal(destinationScenePath);
                    break;
                case LoadingVariant.FighterVersus:
                    ConfigureFighterVersus(session);
                    break;
            }
        }

        private void ConfigureStoryPortal(string destinationScenePath) {
            if (_titleLabel == null) return;
            string titleKey = PortalTitleKeyForScene(destinationScenePath);
            _titleLabel.Text = titleKey;
            _titleLabel.Visible = !string.IsNullOrEmpty(titleKey);
            _titleFlourishes?.Refresh();
        }

        /// <summary>
        /// The portal's title key (UI_SPEC_GLASS 9.2: "the destination's title (<c>campaign_level_*</c> or the hub title)"):
        /// the level's <c>campaign_level_&lt;slug&gt;</c> name when it has one, else the per-level title key of
        /// <see cref="LevelTitleKeyForScene"/> (the hub keeps the ship's title).
        /// </summary>
        public static string PortalTitleKeyForScene(string campaignScenePath) {
            string levelKey = LevelTitleKeyForScene(campaignScenePath);
            const string Suffix = "_level_title";
            if (string.IsNullOrEmpty(levelKey) || levelKey == HubTitleKey || !levelKey.EndsWith(Suffix)) return levelKey;
            string campaignKey = "campaign_level_" + levelKey.Substring(0, levelKey.Length - Suffix.Length);
            return TranslationServer.Translate(campaignKey) != campaignKey ? campaignKey : levelKey;
        }

        private void ConfigureFighterVersus(SessionData session) {
            ApplyPortrait(_playerPortrait, session.SelectedCharacterID);
            ApplyPortrait(_opponentPortrait, session.OpponentCharacterID);
            ApplyName(_playerName, session.SelectedCharacterID);
            ApplyName(_opponentName, session.OpponentCharacterID);
            // A side with no fighter shows no frame, so its edge and glow go with it.
            for (int player = 0; player < 2; player++) {
                bool shown = (player == 0 ? _playerPortrait : _opponentPortrait)?.Visible == true;
                if (_frameDecor[player * 2] != null) _frameDecor[player * 2].Visible = shown;
                if (_frameDecor[player * 2 + 1] != null) _frameDecor[player * 2 + 1].Visible = shown;
            }
            ApplyStage(session.SelectedStageID);
        }

        private static void ApplyPortrait(Control target, string characterID) {
            if (target == null) return;
            CharacterData data = LoadCharacter(characterID);
            Texture2D portrait = data?.CharacterPortrait;
            if (target is PulpPortraitRoundel roundel) roundel.Portrait = portrait;
            else if (target is TextureRect rect) rect.Texture = portrait;
            target.Visible = portrait != null;
        }

        /// <summary>The hero's name under its frame (T40): the raw key, resolved by the label's auto-translation.</summary>
        private static void ApplyName(Label target, string characterID) {
            if (target == null) return;
            string key = LoadCharacter(characterID)?.DisplayNameKey ?? "";
            target.Text = key;
            target.Visible = !string.IsNullOrWhiteSpace(key);
        }

        private static CharacterData LoadCharacter(string characterID) {
            if (string.IsNullOrWhiteSpace(characterID)) return null;
            string path = string.Format(CharacterResourceFormat, characterID);
            if (!ResourceLoader.Exists(path)) return null;
            // Authored tuning data: pinned by the cache, never loaded directly.
            return AuthoredResources.Load<CharacterData>(path);
        }

        private void ApplyStage(string stageID) {
            if (_stageNameLabel == null && _stagePreview == null) return;

            FighterStageData stage = FighterStageCatalog.LoadDefault()?.Find(stageID);

            if (_stageNameLabel != null) {
                string key = stage?.DisplayNameKey ?? "";
                _stageNameLabel.Text = key;
                _stageNameLabel.Visible = !string.IsNullOrEmpty(key);
            }

            if (_stagePreview == null) return;
            string previewPath = stage?.PreviewTexturePath ?? "";
            bool hasPreview = !string.IsNullOrWhiteSpace(previewPath) && ResourceLoader.Exists(previewPath);
            // A preview plate is streamed art, not authored tuning: loaded directly
            // and released with the screen rather than pinned for the process.
            _stagePreview.Texture = hasPreview ? ResourceLoader.Load<Texture2D>(previewPath) : null;
            _stagePreview.Visible = _stagePreview.Texture != null;
            if (_stageFrame != null) _stageFrame.Visible = _stagePreview.Visible;
        }

        private T FindControl<T>(string nodeName) where T : Control =>
            FindChild(nodeName, recursive: true, owned: false) as T;
    }
}
