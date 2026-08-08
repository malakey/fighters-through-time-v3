using System.Text;
using Godot;
using FTT.Characters;
using FTT.Core;
using FTT.Environment;

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
        private TextureRect _playerPortrait;
        private TextureRect _opponentPortrait;
        private TextureRect _stagePreview;

        public override void _Ready() {
            Layer = 100;
            ProcessMode = ProcessModeEnum.Always;
            if (GetNodeOrNull<Control>("Root") == null) BuildFallbackUI();
            ResolveVariantNodes();
        }

        /// <summary>
        /// Resolves the optional variant nodes once, on entry. Variant scenes nest
        /// them differently, so resolution is by name rather than by path — but it
        /// happens exactly once per screen, never per frame.
        /// </summary>
        private void ResolveVariantNodes() {
            _titleLabel = FindControl<Label>("TitleLabel");
            _stageNameLabel = FindControl<Label>("StageNameLabel");
            _playerPortrait = FindControl<TextureRect>("PlayerPortrait");
            _opponentPortrait = FindControl<TextureRect>("OpponentPortrait");
            _stagePreview = FindControl<TextureRect>("StagePreview");
        }

        private void BuildFallbackUI() {
            var root = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Stop };
            root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            UIPalette.ApplyTheme(root);
            AddChild(root);

            var background = new ColorRect { Name = "Background", Color = UIPalette.NavyDeep };
            background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            root.AddChild(background);

            var status = new Label {
                Name = "StatusLabel",
                Text = "loading",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            status.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            status.AddThemeColorOverride("font_color", UIPalette.TextAccent);
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
            string titleKey = LevelTitleKeyForScene(destinationScenePath);
            _titleLabel.Text = titleKey;
            _titleLabel.Visible = !string.IsNullOrEmpty(titleKey);
        }

        private void ConfigureFighterVersus(SessionData session) {
            ApplyPortrait(_playerPortrait, session.SelectedCharacterID);
            ApplyPortrait(_opponentPortrait, session.OpponentCharacterID);
            ApplyStage(session.SelectedStageID);
        }

        private static void ApplyPortrait(TextureRect target, string characterID) {
            if (target == null) return;
            CharacterData data = LoadCharacter(characterID);
            target.Texture = data?.CharacterPortrait;
            target.Visible = target.Texture != null;
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
        }

        private T FindControl<T>(string nodeName) where T : Control =>
            FindChild(nodeName, recursive: true, owned: false) as T;
    }
}
