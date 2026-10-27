using System.Collections.Generic;
using FTT.UI;
using FTT.UI.Pulp;
using Godot;

namespace FTT.Diagnostics {

    /// <summary>
    /// TG-WIDGETS step 1's capture targets (Temporal Glass; the names UR-WIDGETS used are kept so the before and after
    /// galleries pair): the chevron buttons in every state on pad and keyboard (<c>widgets_buttons</c>,
    /// <c>widgets_buttons_kb</c>, <c>widgets_buttons_pressed</c>, <c>widgets_buttons_140</c>), the icons in both weights
    /// (<c>widgets_icons</c>), prompt bars and glass slabs with the sheen (<c>widgets_prompts_pad</c>,
    /// <c>widgets_prompts_kb</c>), the pause slab and tags (<c>widgets_slabs</c>), the Continue card (<c>widgets_continue</c>),
    /// the tiles (<c>widgets_tiles</c>), the title gradient (<c>widgets_title</c>) and the streak with a flourish heading
    /// (<c>widgets_streak</c>). Replicas sit where the mock's renders put them, so <c>tools/ui_theme/compare_capture.py</c>
    /// lines them up with the spec crops 1:1.
    /// </summary>
    public static partial class UiCaptureTargets {

        private static readonly Color WidgetBackdrop = new(0.05f, 0.06f, 0.12f);
        private const string MenuArt = "res://assets/backgrounds/menu/temporal_glass_main_menu_background.png";
        private const string SelectArt = "res://assets/backgrounds/menu/temporal_glass_fighter_select_background.png";
        private const string SubmenuArt = "res://assets/backgrounds/menu/temporal_glass_submenu_background.png";

        private static Control WidgetPage() {
            var page = new Control { Name = "WidgetPage" };
            UIPalette.ApplyTheme(page);
            var back = new ColorRect { Color = WidgetBackdrop };
            back.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            page.AddChild(back);
            return page;
        }

        /// <summary>A page over menu art (cover-scaled), optionally with the menu scrim.</summary>
        private static Control ArtPage(string art, bool scrim, bool mirrored = false) {
            var page = new Control { Name = "WidgetArtPage" };
            UIPalette.ApplyTheme(page);
            var back = new ColorRect { Color = UIPalette.Backdrop };
            back.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            page.AddChild(back);
            var image = new TextureRect {
                Name = "Art", Texture = GD.Load<Texture2D>(art), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = Control.MouseFilterEnum.Ignore
            };
            image.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            page.AddChild(image);
            if (scrim) PulpScreenKit.AddMenuScrim(page, mirrored, 2);
            return page;
        }

        private static Button WidgetButton(string text, string variation, Vector2 size) =>
            new() { Text = text, ThemeTypeVariation = variation, FocusMode = Control.FocusModeEnum.All, CustomMinimumSize = size, Size = size };

        // The main menu's column as MENU-A draws it (4.1: x 96, rows 68 tall at pitch 79 from y 520, the first focused),
        // at the build's width by the 3.2 rule ("CALIBRATION DRILLS" makes it 636), over the menu art and scrim.
        private static Button MenuColumn(Control page, float x, float y) {
            var column = new VBoxContainer { Name = "MenuColumn", Position = new Vector2(x, y) };
            column.AddThemeConstantOverride("separation", 11);
            page.AddChild(column);
            string[] keys = { "menu_story_mode", "menu_fighter_mode", "menu_calibration_drills", "menu_settings", "menu_extras", "menu_quit" };
            var rows = new List<Button>();
            foreach (string key in keys) {
                Button row = WidgetButton(key, UIPalette.Variations.PulpButton, new Vector2(560f, 68f));
                column.AddChild(row);
                PulpButtonDecor.Apply(row);
                rows.Add(row);
            }
            PulpButtonDecor.ApplyStackWidth(rows, 560f, 700f);
            return rows[0];
        }

        // Every state on its own row (no focus here: the column owns it): normal, selected, pressed (a toggled row with the
        // selected look cleared draws the theme's pressed box), disabled; and, at its own place, a compact footer row of
        // three at the D6 compact width (no prompt), FocusRowGap apart.
        private static void StateSheet(Control page, Vector2 states, Vector2 footerAt) {
            var column = new VBoxContainer { Name = "States", Position = states };
            column.AddThemeConstantOverride("separation", 18);
            page.AddChild(column);
            (string text, int kind)[] rows = { ("NORMAL", 0), ("SELECTED", 1), ("PRESSED", 2), ("DISABLED", 3) };
            foreach ((string text, int kind) in rows) {
                Button row = WidgetButton(text, UIPalette.Variations.TemporalGlassButton, new Vector2(560f, 64f));
                row.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
                if (kind is 1 or 2) { row.ToggleMode = true; row.ButtonPressed = true; }
                if (kind == 3) row.Disabled = true;
                column.AddChild(row);
                PulpButtonDecor decor = PulpButtonDecor.Apply(row);
                if (kind == 2) decor.Selected = false;
            }
            var label = new Label { Text = $"row gap {PulpScreenKit.FocusRowGap} (derived {PulpScreenKit.DerivedFocusRowGap})", ThemeTypeVariation = UIPalette.LabelRoles.SceneCaption };
            column.AddChild(label);
            var footer = new HBoxContainer { Name = "Footer", Position = footerAt };
            page.AddChild(footer);
            var footerButtons = new List<Button>();
            foreach (string key in new[] { "movelist_title", "systems_card_title", "common_back" }) {
                Button button = WidgetButton(key, UIPalette.Variations.PulpButtonCompact, new Vector2(290f, 58f));
                footer.AddChild(button);
                PulpButtonDecor decor = PulpButtonDecor.Apply(button, compact: true);
                decor.ShowPrompt = false;
                footerButtons.Add(button);
            }
            PulpButtonDecor.ApplyStackWidth(footerButtons, 290f, reservePrompt: false);
        }

        private static Node WidgetButtons(UiCaptureContext context, PromptDevice device) {
            PulpPromptGlyphs.DeviceOverrideForTesting = device;
            Control page = ArtPage(MenuArt, scrim: true);
            Button focus = MenuColumn(page, 96f, 520f);
            StateSheet(page, new Vector2(1080f, 120f), new Vector2(760f, 1004f));
            context.AfterReady = _ => focus?.GrabFocus();
            return page;
        }

        /// <summary>The menu column (Story Mode focused, the pad badge) over the menu art, and every other state beside it.</summary>
        [UiCaptureTarget("widgets_buttons", SettleFrames = 14)]
        public static Node WidgetsButtons(UiCaptureContext context) => WidgetButtons(context, PromptDevice.Pad);

        /// <summary>The same with the keyboard key-cap badge.</summary>
        [UiCaptureTarget("widgets_buttons_kb", SettleFrames = 14)]
        public static Node WidgetsButtonsKeyboard(UiCaptureContext context) => WidgetButtons(context, PromptDevice.Keyboard);

        /// <summary>A focused, pressed pause row (the focus overlay over the pressed box, the press transform) and a long label shrunk then ellipsized.</summary>
        [UiCaptureTarget("widgets_buttons_pressed", SettleFrames = 14)]
        public static Node WidgetsButtonsPressed(UiCaptureContext context) {
            PulpPromptGlyphs.DeviceOverrideForTesting = PromptDevice.Pad;
            Control page = ArtPage(SubmenuArt, scrim: true);
            var column = new VBoxContainer { Position = new Vector2(96f, 300f) };
            column.AddThemeConstantOverride("separation", 9);
            page.AddChild(column);
            Button pressed = WidgetButton("menu_resume", UIPalette.Variations.TemporalGlassButton, new Vector2(560f, 64f));
            pressed.ToggleMode = true;
            pressed.ButtonPressed = true;
            column.AddChild(pressed);
            PulpButtonDecor.Apply(pressed).Selected = false;
            var stack = new List<Button> { pressed };
            foreach (string text in new[] { "menu_settings", "Quick play (Einstein vs CPU)", "A label far too long for any chevron row of the pause list" }) {
                Button row = WidgetButton(text, UIPalette.Variations.TemporalGlassButton, new Vector2(560f, 64f));
                column.AddChild(row);
                PulpButtonDecor.Apply(row);
                stack.Add(row);
            }
            // The pause stack's D6 width, capped at 680: the third row shrinks to fit, the fourth ellipsizes at its floor.
            PulpButtonDecor.ApplyStackWidth(stack, 560f, 680f);
            context.AfterReady = _ => {
                pressed.GrabFocus();
                pressed.EmitSignal(BaseButton.SignalName.ButtonDown);
            };
            return page;
        }

        /// <summary>The menu column and the states at UI scale 1.4 (heights, paddings, marker and badge scale; restored after).</summary>
        [UiCaptureTarget("widgets_buttons_140", SettleFrames = 16)]
        public static Node WidgetsButtons140(UiCaptureContext context) {
            UIPalette.ApplyUiScale(1.4f);
            PulpPromptGlyphs.DeviceOverrideForTesting = PromptDevice.Pad;
            Control page = ArtPage(MenuArt, scrim: true);
            Button focus = MenuColumn(page, 96f, 200f);
            StateSheet(page, new Vector2(1000f, 60f), new Vector2(96f, 900f));
            context.AfterReady = _ => focus?.GrabFocus();
            return page;
        }

        /// <summary>Every glass icon in both weights (bold below 30 px), tinted, plus a legacy alias and the pulp fallback.</summary>
        [UiCaptureTarget("widgets_icons")]
        public static Node WidgetsIcons(UiCaptureContext context) {
            Control page = WidgetPage();
            var panel = new PanelContainer { Position = new Vector2(40f, 30f), ThemeTypeVariation = UIPalette.Variations.InkPanel };
            page.AddChild(panel);
            var rows = new VBoxContainer();
            rows.AddThemeConstantOverride("separation", 10);
            panel.AddChild(rows);
            foreach (int size in new[] { 18, 24, 28, 38, 50 }) {
                var flow = new HFlowContainer { CustomMinimumSize = new Vector2(1760f, 0f) };
                flow.AddThemeConstantOverride("h_separation", 14);
                flow.AddThemeConstantOverride("v_separation", 6);
                rows.AddChild(flow);
                flow.AddChild(new Label { Text = $"{size} px {(PulpIcon.UseBold(size) ? "bold" : "regular")}", ThemeTypeVariation = UIPalette.SmallLabelVariation, CustomMinimumSize = new Vector2(140f, 0f) });
                foreach (string name in PulpIcon.GlassNames) flow.AddChild(PulpIcon.Create(name, IconTint(name), size));
            }
            var names = new HFlowContainer { CustomMinimumSize = new Vector2(1760f, 0f) };
            names.AddThemeConstantOverride("h_separation", 10);
            rows.AddChild(names);
            foreach (string name in PulpIcon.GlassNames) names.AddChild(new Label { Text = name, ThemeTypeVariation = UIPalette.SmallLabelVariation });
            return page;
        }

        // UI_SPEC_GLASS 7.2's tints.
        private static Color IconTint(string name) => name switch {
            "venom" or "burn" => UIPalette.StatusDamage,
            "dilation" or "static" or "root" => UIPalette.StatusControl,
            "suppress" or "lock_suppressed" => UIPalette.UnboundCold,
            "shield" or "shield_off" => UIPalette.Shield,
            "hourglass" or "freeze" or "beacon_anchor" or "beacon_anchor_hollow" => UIPalette.Accent2,
            "dust" => UIPalette.Res,
            "flag" or "defy_ready" or "hourglass_caret" => UIPalette.Res2,
            "defy_notready" or "defy_spent" or "lock_dormant" => UIPalette.Ink3,
            "defy_barred" => UIPalette.Danger,
            "slot_p1" => UIPalette.P1,
            "slot_p2" => UIPalette.P2,
            "chevron_down" => UIPalette.Ink2,
            _ => UIPalette.Accent
        };

        private static Node WidgetPrompts(PromptDevice device) {
            PulpPromptGlyphs.DeviceOverrideForTesting = device;
            Control page = ArtPage(MenuArt, scrim: true);
            // Two glass slabs with the sheen: the 620 x 214 Vitals panel and a 600-wide list slab.
            var vitals = new PanelContainer {
                Name = "Vitals", Position = new Vector2(80f, 80f), CustomMinimumSize = new Vector2(620f, 214f),
                ThemeTypeVariation = UIPalette.Variations.GlassPanelVitals
            };
            vitals.AddChild(new Label { Text = "VITALS PANEL 620 x 214", ThemeTypeVariation = UIPalette.LabelRoles.HeroName });
            page.AddChild(vitals);
            PulpScreenKit.DressPanel(vitals, "hud_vitals");
            var slab = new PanelContainer {
                Name = "Slab", Position = new Vector2(80f, 360f), CustomMinimumSize = new Vector2(600f, 420f),
                ThemeTypeVariation = UIPalette.Variations.InkPanel
            };
            var inside = new VBoxContainer();
            inside.AddThemeConstantOverride("separation", 16);
            slab.AddChild(inside);
            inside.AddChild(new Label { Text = "GLASS SLAB 600 x 420", ThemeTypeVariation = UIPalette.TitleLabelVariation });
            var onGlass = new PulpPromptBar { Name = "OnGlass" };
            onGlass.AddPrompt("ui_accept", "ui_prompt_select");
            onGlass.AddPrompt("ui_cancel", "common_back");
            inside.AddChild(onGlass);
            page.AddChild(slab);
            PulpScreenKit.DressPanel(slab, "pause_tag_controller");
            // The full-screen prompt bar where MENU-A puts it (right 70, bottom 44).
            PulpScreenKit.AddPromptBar(page, ("ui_accept", "ui_prompt_select"), ("ui_cancel", "common_back"));
            return page;
        }

        /// <summary>A pad prompt bar bottom-right over the menu art and on glass, and two slabs with the sheen and tags.</summary>
        [UiCaptureTarget("widgets_prompts_pad")]
        public static Node WidgetsPromptsPad(UiCaptureContext context) => WidgetPrompts(PromptDevice.Pad);

        /// <summary>The same with keyboard key-caps.</summary>
        [UiCaptureTarget("widgets_prompts_kb")]
        public static Node WidgetsPromptsKeyboard(UiCaptureContext context) => WidgetPrompts(PromptDevice.Keyboard);

        /// <summary>The PAUSE-A slab as the mock draws it (600 wide at (660, 92), the tag, title, strip and eight rows) and the three tag hosts with short and overlong tags.</summary>
        [UiCaptureTarget("widgets_slabs", SettleFrames = 14)]
        public static Node WidgetsSlabs(UiCaptureContext context) {
            PulpPromptGlyphs.DeviceOverrideForTesting = PromptDevice.Pad;
            Control page = ArtPage(SubmenuArt, scrim: false);
            PanelContainer dim = PulpScreenKit.MakeScrim("Dim");
            page.AddChild(dim);
            var panel = new PausePanel { Name = "PauseReplica", Position = new Vector2(660f, 92f), TagText = "Pompeii 79 AD" };
            var layout = new VBoxContainer { Name = "Layout" };
            layout.AddThemeConstantOverride("separation", 9);
            panel.AddChild(layout);
            layout.AddChild(new Label { Text = "menu_paused", ThemeTypeVariation = UIPalette.LabelRoles.PauseTitle, HorizontalAlignment = HorizontalAlignment.Center, Uppercase = true });
            layout.AddChild(new Label { Text = "NORMAL  ·  INTEGRITY 64%  ·  REWINDS ×3", ThemeTypeVariation = UIPalette.LabelRoles.StatusLine, HorizontalAlignment = HorizontalAlignment.Center });
            Button first = null;
            foreach (string key in new[] { "menu_resume", "menu_settings", "movelist_title", "dialogue_log_title", "menu_save", "menu_restart", "pause_lower_difficulty", "pause_quit_to_menu" }) {
                var row = new Button { Text = key };
                layout.AddChild(row);
                PulpScreenKit.StylePauseButton(row);
                first ??= row;
            }
            page.AddChild(panel);
            // The tag hosts: a short tag, an overlong one (clamped 24 px inside, ellipsized), a tagged panel.
            var hosts = new VBoxContainer { Position = new Vector2(1360f, 120f) };
            hosts.AddThemeConstantOverride("separation", 60);
            page.AddChild(hosts);
            foreach (string key in new[] { "hud_vitals", "AN EXCEEDINGLY LONG LOCALIZED PANEL TITLE" }) {
                var tagged = new PanelContainer { CustomMinimumSize = new Vector2(440f, 150f), ThemeTypeVariation = UIPalette.Variations.InkPanel };
                tagged.AddChild(new Label { Text = "PanelTagAnchor", ThemeTypeVariation = UIPalette.LabelRoles.HeroName });
                hosts.AddChild(tagged);
                PulpScreenKit.DressPanel(tagged, key);
            }
            var taggedPanel = new PulpTaggedPanel { TagKey = "hud_abilities" };
            var inner = new PanelContainer { CustomMinimumSize = new Vector2(440f, 150f), ThemeTypeVariation = UIPalette.Variations.GlassPanelAbilities };
            inner.AddChild(new Label { Text = "PulpTaggedPanel", ThemeTypeVariation = UIPalette.LabelRoles.HeroName });
            taggedPanel.AddChild(inner);
            hosts.AddChild(taggedPanel);
            context.AfterReady = _ => first?.GrabFocus();
            return page;
        }

        /// <summary>The MENU-A Continue card as the mock draws it (600 wide at (1250, 842)): a glass slab with the sheen, the tag and the 86 px portrait frame.</summary>
        [UiCaptureTarget("widgets_continue")]
        public static Node WidgetsContinue(UiCaptureContext context) {
            Control page = ArtPage(MenuArt, scrim: true);
            var card = new PanelContainer {
                Name = "ContinueReplica", Position = new Vector2(1250f, 842f), CustomMinimumSize = new Vector2(600f, 0f),
                ThemeTypeVariation = UIPalette.Variations.GlassPanelContinue
            };
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 18);
            card.AddChild(row);
            row.AddChild(new PulpPortraitRoundel { Diameter = 86f, Portrait = GD.Load<Texture2D>("res://assets/sprites/characters/portraits/einstein_portrait.png") });
            var lines = new VBoxContainer();
            lines.AddThemeConstantOverride("separation", 6);
            row.AddChild(lines);
            lines.AddChild(new Label { Text = "character_einstein_name", ThemeTypeVariation = UIPalette.LabelRoles.StickerLine1, Uppercase = true });
            lines.AddChild(new Label { Text = "Level 6 — Pompeii 79 AD", ThemeTypeVariation = UIPalette.LabelRoles.StickerLine2 });
            lines.AddChild(new Label { Text = "Normal  ·  Chapter 6", ThemeTypeVariation = UIPalette.LabelRoles.StickerLine3 });
            page.AddChild(card);
            PulpScreenKit.DressPanel(card, "CONTINUE · SLOT 1");
            return page;
        }

        private static readonly string[] Roster = { "einstein", "joan", "leonardo", "lincoln", "cleopatra", "tesla", "shakespeare", "mozart", "tubman" };

        /// <summary>
        /// The CARD-A grid as the mock draws it (3 x 3 tiles 164 px at pitch 180 from (698, 196), the Random tile 524 x 72):
        /// Joan with P1's cursor and lock, Tesla with P2's; then every other tile state: dim, standard, lit, focus, both
        /// owners, a P2 lock, and chips at chamfer 12.
        /// </summary>
        [UiCaptureTarget("widgets_tiles", SettleFrames = 12)]
        public static Node WidgetsTiles(UiCaptureContext context) {
            Control page = ArtPage(SelectArt, scrim: false);
            for (int i = 0; i < Roster.Length; i++) {
                var tile = new Button {
                    Name = "Tile" + i, FocusMode = Control.FocusModeEnum.None, Flat = true,
                    Position = new Vector2(698f + i % 3 * 180f, 196f + i / 3 * 180f), Size = new Vector2(164f, 164f)
                };
                tile.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
                tile.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
                tile.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
                page.AddChild(tile);
                GlassTileDecor decor = GlassTileDecor.Attach(tile);
                decor.Portrait = GD.Load<Texture2D>($"res://assets/sprites/characters/portraits/{Roster[i]}_portrait.png");
                if (Roster[i] == "joan") { decor.Owners = GlassTileDecor.OwnerP1; decor.LockedBy = 0; }
                if (Roster[i] == "tesla") decor.Owners = GlassTileDecor.OwnerP2;
                var name = new Label {
                    Text = $"character_{Roster[i]}_card_name", ThemeTypeVariation = UIPalette.LabelRoles.CardName, Uppercase = true,
                    HorizontalAlignment = HorizontalAlignment.Center, Position = new Vector2(0f, 136f), Size = new Vector2(164f, 18f)
                };
                if (decor.Owners != 0) name.AddThemeColorOverride("font_color", Colors.White);
                tile.AddChild(name);
            }
            var random = new Control { Name = "Random", Position = new Vector2(698f, 736f), Size = new Vector2(524f, 72f) };
            page.AddChild(random);
            GlassTileDecor.Attach(random);
            var words = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Size = new Vector2(524f, 72f) };
            words.AddThemeConstantOverride("separation", 22);
            random.AddChild(words);
            words.AddChild(new Label { Text = "?", ThemeTypeVariation = UIPalette.LabelRoles.CardQuestion, VerticalAlignment = VerticalAlignment.Center });
            words.AddChild(new Label { Text = "fighter_random_character", ThemeTypeVariation = UIPalette.LabelRoles.RandomWord, Uppercase = true, VerticalAlignment = VerticalAlignment.Center });

            (string text, GlassTileDecor.TileEdge edge, int owners, int locked, float chamfer)[] states = {
                ("DIM", GlassTileDecor.TileEdge.Dim, 0, -1, 16f), ("STANDARD", GlassTileDecor.TileEdge.Standard, 0, -1, 16f),
                ("LIT", GlassTileDecor.TileEdge.Lit, 0, -1, 16f), ("FOCUS", GlassTileDecor.TileEdge.Focus, 0, -1, 16f),
                ("BOTH", GlassTileDecor.TileEdge.Dim, 3, -1, 16f), ("P2 LOCK", GlassTileDecor.TileEdge.Dim, 2, 1, 16f),
                ("CHIP DIM", GlassTileDecor.TileEdge.Dim, 0, -1, 12f), ("CHIP LIT", GlassTileDecor.TileEdge.Lit, 0, -1, 12f)
            };
            for (int i = 0; i < states.Length; i++) {
                var host = new Control { Position = new Vector2(1300f + i % 2 * 280f, 120f + i / 2 * 200f), Size = new Vector2(240f, 164f) };
                page.AddChild(host);
                GlassTileDecor decor = GlassTileDecor.Attach(host, states[i].chamfer);
                decor.Edge = states[i].edge;
                decor.Owners = states[i].owners;
                decor.LockedBy = states[i].locked;
                host.AddChild(new Label { Text = states[i].text, ThemeTypeVariation = UIPalette.LabelRoles.CardName, Position = new Vector2(16f, 120f) });
            }
            return page;
        }

        /// <summary>The MENU-A title lock-up as the mock draws it at (110, 64): the flourish row, FIGHTERS and TIME in the title gradient, THROUGH and its rule.</summary>
        [UiCaptureTarget("widgets_title", SettleFrames = 12)]
        public static Node WidgetsTitle(UiCaptureContext context) {
            Control page = ArtPage(MenuArt, scrim: true);
            Theme theme = UIPalette.LoadTheme();
            Font titleFont = theme.GetFont("font", UIPalette.LabelRoles.CoverTitle);
            int titleSize = theme.GetFontSize("font_size", UIPalette.LabelRoles.CoverTitle);
            Font bannerFont = theme.GetFont("font", UIPalette.LabelRoles.CoverBanner);
            int bannerSize = theme.GetFontSize("font_size", UIPalette.LabelRoles.CoverBanner);
            // Each line's glyph box is centred on the mock's line box (T01: 105.3 at y 97 and 259.3; T02: 57 at 202.3).
            float titleBox = titleFont.GetHeight(titleSize), bannerBox = bannerFont.GetHeight(bannerSize);
            var flourish = new GlassFlourish { Shape = GlassFlourish.FlourishShape.RuleDiamondRule, Gap = GlassFlourish.MenuGap, Position = new Vector2(110f, 64f), Size = new Vector2(700f, 11f) };
            page.AddChild(flourish);
            foreach ((string key, float boxTop) in new[] { ("menu_cover_title_word", 97f), ("TIME", 259.3f) }) {
                var word = new Label { Text = key, ThemeTypeVariation = UIPalette.LabelRoles.CoverTitle, Uppercase = true, Position = new Vector2(110f, boxTop + (105.28f - titleBox) / 2f) };
                page.AddChild(word);
                TitleGradient.Attach(word);
            }
            var through = new Label { Text = "THROUGH", ThemeTypeVariation = UIPalette.LabelRoles.CoverBanner, Uppercase = true, Position = new Vector2(116f, 202.3f + (57f - bannerBox) / 2f) };
            page.AddChild(through);
            var rule = new GlassFlourish { Shape = GlassFlourish.FlourishShape.Rule, RuleColor = UIPalette.Edge, Position = new Vector2(514.6f, 230.3f), Size = new Vector2(295.4f, 1f) };
            page.AddChild(rule);
            return page;
        }

        /// <summary>The dir-a screen streak over the select art with the CARD-A title between flourishes (3.7: top-centre, y 40).</summary>
        [UiCaptureTarget("widgets_streak", SettleFrames = 12)]
        public static Node WidgetsStreak(UiCaptureContext context) {
            Control page = ArtPage(SelectArt, scrim: false);
            var top = new VBoxContainer { Position = new Vector2(0f, 40f), Size = new Vector2(1920f, 56f) };
            page.AddChild(top);
            PulpScreenKit.AddFlourishHeading(top, "fighter_select_title");
            PulpScreenKit.AddScreenStreak(page);
            return page;
        }
    }
}
