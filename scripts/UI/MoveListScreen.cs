using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// V7.3 Fighter Onboarding: the per-character Move List screen
    /// (design-godot-v7.md Section 7, "Fighter Onboarding"). Content is sourced
    /// LIVE from the shipped data — <see cref="BasicComboRules"/> /
    /// <see cref="UniversalMovementRules"/> for the universal chassis (string
    /// frames per the character's authored profile, grab 10/4/24, the three
    /// throws, roll/jump/fast-fall) and the character's authored
    /// <c>{id}_data.tres</c> for specials/movement/ultimate name, description,
    /// damage, cooldown, and icon — never a second authored copy of a number.
    ///
    /// <para>Follows the <see cref="SettingsMenu"/> overlay pattern: a
    /// script-less authored scene instantiated by this CanvasLayer, opened as an
    /// overlay inside the pause CanvasLayer (the pause menu keeps ownership of
    /// <see cref="SceneTree.Paused"/>; this screen never touches it) or over the
    /// character select. Closing raises <see cref="Closed"/> so the opener can
    /// restore focus.</para>
    /// </summary>
    public partial class MoveListScreen : CanvasLayer {
        public const string ScenePath = "res://scenes/ui/MoveList.tscn";

        /// <summary>
        /// Package 11 A5: which mode the list is being read in.
        /// <c>Fighter</c> always shows the full kit — Fighter Mode, the hub
        /// Holodeck and the Calibration Drills never gate an ability.
        /// <c>Story</c> consults the V7.5 Legacy Unlock Schedule and marks
        /// not-yet-restored slots Dormant.
        /// </summary>
        public enum MoveListMode {
            Fighter,
            Story
        }

        /// <summary>Raised after the screen hides, so the opener can restore focus.</summary>
        public event Action Closed;

        /// <summary>Character the list is currently built for. Test seam.</summary>
        public string CharacterID { get; private set; } = "";

        /// <summary>Mode the list was last built in. Test seam.</summary>
        public MoveListMode Mode { get; private set; } = MoveListMode.Fighter;

        private Control _root;
        private TextureRect _portrait;
        private Pulp.PulpPortraitRoundel _portraitFrame;
        private Label _characterName;
        private Label _archetype;
        private VBoxContainer _sections;
        private SystemsCardScreen _systemsCard;
        private List<Control> _focusChain = new();
        private PausePanel _panel;
        private Control _shade;
        private Control _promptBar;

        /// <summary>The portrait frame beside the title (UI_SPEC_GLASS G5).</summary>
        public const float PortraitSize = 96f;

        /// <summary>
        /// One dim (UI_SPEC_GLASS 9.2): opened over a pause menu the list sits on the pause's dim and draws none of its own.
        /// Default false: the list brings its 0.70 dim (the character select, a drill).
        /// </summary>
        public bool UsesHostDim { get; set; }

        /// <summary>The instantiated authored scene root. Test seam.</summary>
        internal Control Root => _root;

        public override void _Ready() {
            Layer = 100;
            ProcessMode = ProcessModeEnum.Always;
            Visible = false;
            var packed = ResourceLoader.Load<PackedScene>(ScenePath);
            _root = packed?.Instantiate<Control>();
            if (_root == null) {
                GD.PushError($"MoveListScreen could not instantiate {ScenePath}.");
                return;
            }
            AddChild(_root);
            UIPalette.ApplyTheme(_root);
            _portrait = _root.GetNodeOrNull<TextureRect>("Center/Panel/Layout/Header/Portrait");
            _characterName = _root.GetNodeOrNull<Label>("Center/Panel/Layout/Header/HeaderText/CharacterName");
            _archetype = _root.GetNodeOrNull<Label>("Center/Panel/Layout/Header/HeaderText/Archetype");
            _sections = _root.GetNodeOrNull<VBoxContainer>("Center/Panel/Layout/Scroll/Sections");
            _shade = _root.GetNodeOrNull<Control>("Shade");
            var close = _root.GetNodeOrNull<Button>("Center/Panel/Layout/FooterRow/CloseButton");
            if (close != null) close.Pressed += Close;
            var card = _root.GetNodeOrNull<Button>("Center/Panel/Layout/FooterRow/SystemsCardButton");
            if (card != null) card.Pressed += OpenSystemsCard;
            // TG-PAUSE (UI_SPEC_GLASS 9.2): a 1240 x 900 glass slab tagged with the hero's style, the G5 portrait frame
            // beside the title, section headings over a fading rule, and a footer of compact chevrons (D6: compact footers
            // share the widest label's width and show no badge; the prompt bar carries A and B).
            _panel = _root.GetNodeOrNull<PausePanel>("Center/Panel");
            if (_panel != null) {
                PulpScreenKit.DressPanel(_panel, sheen: true, buttonInset: true);
                MenuSafeAreaFit.Attach(_panel, GlassScreenDressing.OverlayPromptClearance);
            }
            if (_portrait?.GetParent() is Control header) {
                _portraitFrame = new Pulp.PulpPortraitRoundel { Name = "PortraitFrame", Diameter = PortraitSize };
                header.AddChild(_portraitFrame);
                header.MoveChild(_portraitFrame, 0);
            }
            var footer = new List<Button>();
            foreach (Button button in new[] { card, close }) {
                if (button == null) continue;
                Pulp.PulpButtonDecor.Apply(button, compact: true).ShowPrompt = false;
                footer.Add(button);
            }
            Pulp.PulpButtonDecor.ApplyStackWidth(footer, CompactFooterMinWidth, reservePrompt: false);
            if (close?.GetParent() is BoxContainer row) PulpScreenKit.SeparateFocusRow(row);
            _promptBar = PulpScreenKit.AddBottomPromptBar(_root, "common_back");
        }

        /// <summary>The D6 floor of a compact footer button (3.2: 290).</summary>
        public const float CompactFooterMinWidth = 290f;

        /// <summary>Builds the list for one roster character and shows the screen.</summary>
        public void Open(string characterID) => Open(characterID, MoveListMode.Fighter);

        /// <summary>Mode-aware overload: the Story pause opens the gated list.</summary>
        public void Open(string characterID, MoveListMode mode) {
            Populate(characterID, mode);
            Visible = true;
            ShowOwnPanel(true);
            if (_shade != null) _shade.Visible = !UsesHostDim;
            RebuildFocusChain();
            // MOT-A row 4: the slab fades and rises (its own dim fading in with it unless it sits on a host's).
            _panel?.PlayAppear(UsesHostDim ? null : _shade);
        }

        /// <summary>The Systems Card replaces the list's slab while it is open (one slab over one dim).</summary>
        private void ShowOwnPanel(bool shown) {
            if (_panel != null) _panel.Visible = shown;
            if (_promptBar != null) _promptBar.Visible = shown;
        }

        /// <summary>Hides the screen and hands control back to the opener.</summary>
        public void Close() {
            if (!Visible) return;
            _systemsCard?.Close();
            Visible = false;
            _panel?.StopAppear(UsesHostDim ? null : _shade as CanvasItem);
            Closed?.Invoke();
        }

        public override void _UnhandledInput(InputEvent @event) {
            if (!Visible || @event == null) return;
            if (_systemsCard != null && _systemsCard.Visible) return;
            if (@event.IsActionPressed("ui_cancel")
                || @event.IsActionPressed(InputManager.Actions.Pause)) {
                Close();
                GetViewport()?.SetInputAsHandled();
            }
        }

        private void RebuildFocusChain() {
            _focusChain = FocusChainBuilder.Apply(
                _root?.GetNodeOrNull<Control>("Center/Panel/Layout/FooterRow"),
                FocusChainAxis.Horizontal);
        }

        /// <summary>
        /// The card is reachable from the Move List per the design spec. It takes the list's place over the same dim (the
        /// list's own, or its host's): the list's slab and prompt bar stand down until the card closes.
        /// </summary>
        private void OpenSystemsCard() {
            if (_systemsCard == null || !IsInstanceValid(_systemsCard)) {
                _systemsCard = new SystemsCardScreen { Name = "SystemsCardScreen", UsesHostDim = true };
                AddChild(_systemsCard);
                _systemsCard.Closed += OnSystemsCardClosed;
            }
            _systemsCard.Open();
            ShowOwnPanel(false);
        }

        private void OnSystemsCardClosed() {
            if (Visible) ShowOwnPanel(true);
            RebuildFocusChain();
        }

        // === Content =======================================================

        /// <summary>
        /// Rebuilds every data-driven row. Public seam so tests can build the
        /// list for all nine roster IDs without driving input.
        /// </summary>
        public void Populate(string characterID) => Populate(characterID, MoveListMode.Fighter);

        /// <summary>Mode-aware overload (Package 11 A5).</summary>
        public void Populate(string characterID, MoveListMode mode) {
            CharacterID = characterID ?? "";
            Mode = mode;
            if (_sections == null) return;
            for (int index = _sections.GetChildCount() - 1; index >= 0; index--) {
                Node child = _sections.GetChild(index);
                _sections.RemoveChild(child);
                child.QueueFree();
            }

            string path = $"res://resources/Characters/{CharacterID}_data.tres";
            CharacterData data = ResourceLoader.Exists(path)
                ? AuthoredResources.Load<CharacterData>(path)
                : null;
            BasicStringProfile profile = BasicComboRules.StringProfileFor(CharacterID);

            if (_portrait != null) _portrait.Texture = data?.CharacterPortrait;
            if (_portraitFrame != null) _portraitFrame.Portrait = data?.CharacterPortrait;
            if (_characterName != null) {
                // UI_SPEC_GLASS 9.2: the title is movelist_title_for ("Move List — {name}"), a 36 px panel title.
                string heroName = data != null && !string.IsNullOrWhiteSpace(data.DisplayNameKey)
                    ? Tr(data.DisplayNameKey)
                    : Tr("common_unknown");
                _characterName.Text = string.Format(Tr("movelist_title_for"), heroName);
            }
            // The style is the slab's tag (as the select's side stat panel carries it); the caption under the title that held
            // it in the pulp build keeps the text but stands down.
            string style = Tr(StyleKey(data?.Style ?? CombatStyle.Hybrid));
            if (_archetype != null) _archetype.Text = style;
            if (_panel != null) _panel.TagText = style;

            float basicDamage = data?.BasicAttackDamage ?? 10f;

            // -- Basic string: the universal chassis with the character's
            //    authored startups, damage shape, and reach.
            AddSectionTitle("movelist_section_string");
            for (int hit = 0; hit < BasicComboRules.ComboHits; hit++) {
                AddRow($"StringHit{hit + 1}", string.Format(
                    Tr("movelist_string_hit"),
                    hit + 1,
                    profile.GroundStartupFrames[hit],
                    BasicComboRules.GroundActiveFrames[hit],
                    BasicComboRules.GroundRecoveryFrames[hit],
                    basicDamage * profile.DamageTenths[hit] / 10f));
            }
            // GU-A (2026-10-12): an air press is one strike, never the string.
            AddRow("AirAttack", string.Format(
                Tr("movelist_air_attack"),
                profile.AirAttackStartupFrames,
                BasicComboRules.AirAttackActiveFrames,
                BasicComboRules.AirAttackRecoveryFrames,
                basicDamage * BasicComboRules.AirAttackDamageMultiplier));
            AddRow("Reach", string.Format(
                Tr("movelist_reach"), profile.ReachWidthPercent, profile.ReachHeightPercent));
            AddRow("UpAttack", string.Format(
                Tr("movelist_up_attack"),
                BasicComboRules.UpAttackStartupFrames,
                BasicComboRules.UpAttackActiveFrames,
                BasicComboRules.UpAttackRecoveryFrames,
                basicDamage * BasicComboRules.DirectionalAttackDamageMultiplier));
            AddRow("DownAir", string.Format(
                Tr("movelist_down_air"),
                BasicComboRules.DownAirStartupFrames,
                BasicComboRules.DownAirActiveFrames,
                BasicComboRules.DownAirRecoveryFrames,
                basicDamage * BasicComboRules.DirectionalAttackDamageMultiplier));

            // -- Grabs & throws: universal numbers plus the triangle note.
            AddSectionTitle("movelist_section_grabs");
            AddRow("GrabFrames", string.Format(
                Tr("movelist_grab_frames"),
                BasicComboRules.GrabStartupFrames,
                BasicComboRules.GrabActiveFrames,
                BasicComboRules.GrabWhiffRecoveryFrames));
            AddRow("Throws", string.Format(
                Tr("movelist_throws"),
                BasicComboRules.ForwardThrowKnockbackMultiplier,
                BasicComboRules.UpThrowKnockbackMultiplier,
                BasicComboRules.BackThrowKnockbackMultiplier,
                BasicComboRules.ThrowDamageMultiplier));
            AddRow("TriangleNote", Tr("movelist_triangle_note"));

            // -- Specials, movement ability, ultimate: authored .tres data only.
            AddSectionTitle("movelist_section_abilities");
            HashSet<AbilitySlot> unlocked = mode == MoveListMode.Story
                ? FTT.Characters.CharacterFactory.ResolveStoryUnlockedSlots(CharacterID)
                : null;
            AddAbility("movelist_slot_special1", data?.SpecialAttackOne, AbilitySlot.Special1, unlocked);
            AddAbility("movelist_slot_special2", data?.SpecialAttackTwo, AbilitySlot.Special2, unlocked);
            AddAbility("movelist_slot_movement", data?.MovementAbility, AbilitySlot.MovementAbility, unlocked);
            AddAbility("movelist_slot_ultimate", data?.UltimateAttack, AbilitySlot.Ultimate, unlocked);

            // -- Universal movement.
            AddSectionTitle("movelist_section_movement");
            AddRow("Roll", string.Format(
                Tr("movelist_roll"),
                UniversalMovementRules.RollStartupFrames,
                UniversalMovementRules.RollTravelFrames,
                UniversalMovementRules.RollRecoveryFrames,
                UniversalMovementRules.RollInvulnerabilityFrames));
            AddRow("Jumps", string.Format(Tr("movelist_jumps"), data?.MaxJumpCount ?? 2));
            AddRow("FastFall", string.Format(
                Tr("movelist_fast_fall"), UniversalMovementRules.FastFallSpeed));
            // CM-MOVE (decision 5.2, 2026-10-16): the Fighter ledge get-ups, every
            // number read live from BasicComboRules' ledge section. Fighter only:
            // Story's LedgeGrabPoint keeps its climb jump and drop.
            if (Mode == MoveListMode.Fighter) {
                AddRow("LedgeOptions", string.Format(
                    Tr("movelist_ledge_options"),
                    BasicComboRules.LedgeNeutralClimbFrames,
                    BasicComboRules.LedgeNeutralClimbIntangibleFrames,
                    BasicComboRules.LedgeActionClimbFrames,
                    BasicComboRules.LedgeAttackStartupFrames,
                    BasicComboRules.LedgeAttackActiveFrames,
                    BasicComboRules.LedgeAttackRecoveryFrames,
                    BasicComboRules.LedgeFirstGrabIntangibleFrames));
            }
        }

        private static string StyleKey(CombatStyle style) => style switch {
            CombatStyle.Melee => "movelist_style_melee",
            CombatStyle.Ranged => "movelist_style_ranged",
            _ => "movelist_style_hybrid"
        };

        /// <summary>
        /// A section heading inside a slab (UI_SPEC_GLASS 9.1 item 2): Cinzel 700 26, .13em, upper case, <c>accent</c>, with
        /// a 1 px rule under it (90 degrees, <c>edge</c> to transparent, the full width). A later section keeps a little air
        /// above it so the sections read apart.
        /// </summary>
        private void AddSectionTitle(string titleKey) {
            if (_sections.GetChildCount() > 0) {
                _sections.AddChild(new Control {
                    Name = $"Gap_{titleKey}", CustomMinimumSize = new Vector2(0f, SectionGap),
                    MouseFilter = Control.MouseFilterEnum.Ignore
                });
            }
            var title = new Label {
                Name = $"Section_{titleKey}",
                Text = Tr(titleKey),
                Uppercase = true,
                ThemeTypeVariation = UIPalette.HeadingLabelVariation
            };
            _sections.AddChild(title);
            Pulp.GlassFlourish rule = Pulp.GlassFlourish.Create(Pulp.GlassFlourish.FlourishShape.Rule);
            rule.Name = $"Section_{titleKey}_Rule";
            rule.RuleColor = UIPalette.Edge;
            rule.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _sections.AddChild(rule);
        }

        /// <summary>The extra air above a section heading after the first.</summary>
        public const float SectionGap = 12f;

        /// <summary>A data row (9.2: stats in Outfit 400 22, <c>ink2</c>), wrapping across the slab.</summary>
        private void AddRow(string name, string text) {
            var row = new Label {
                Name = name,
                Text = text,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                ThemeTypeVariation = UIPalette.LabelRoles.BodySmallDimLabel
            };
            _sections.AddChild(row);
        }

        /// <summary>
        /// One authored ability row: name, damage/cooldown, description, and
        /// icon straight off the .tres — the resource owns every number shown.
        /// </summary>
        private void AddAbility(
            string slotKey, AbilityData ability, AbilitySlot slot, HashSet<AbilitySlot> unlockedSlots) {
            if (ability == null) return;
            // Package 11 A5: a Story row for a slot the Legacy Unlock Schedule
            // has not restored is DORMANT — shown, never hidden, but with its
            // frame data withheld until the hero earns it. A null set means "no
            // gate" (Fighter Mode, or a Story context with no active save).
            bool dormant = unlockedSlots != null
                && LegacyUnlockSchedule.MilestoneLevelFor(slot) > 0
                && !unlockedSlots.Contains(slot);
            var row = new HBoxContainer { Name = $"Ability_{slotKey}" };
            row.AddThemeConstantOverride("separation", 18);
            _sections.AddChild(row);

            // The game's own ability icon (spec 3.5: ability art is not the ICN-A set), dimmed while the slot is dormant.
            var icon = new TextureRect {
                Name = "Icon",
                Texture = ability.AbilityIcon,
                CustomMinimumSize = new Vector2(AbilityIconSize, AbilityIconSize),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
                Modulate = dormant ? UIPalette.DimModulate : Colors.White
            };
            row.AddChild(icon);

            var body = new VBoxContainer {
                Name = "Body",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            body.AddThemeConstantOverride("separation", 2);
            row.AddChild(body);

            // 9.2: the slot name (Outfit 18, .16em, accent) over the ability name (Cinzel 700 24, ink).
            var slotLabel = new Label {
                Name = "SlotName", Text = Tr(slotKey), Uppercase = true,
                ThemeTypeVariation = UIPalette.LabelRoles.SlotCaption
            };
            body.AddChild(slotLabel);

            string abilityName = string.IsNullOrWhiteSpace(ability.DisplayNameKey)
                ? ability.AbilityName
                : Tr(ability.DisplayNameKey);
            var nameLabel = new Label {
                Name = "AbilityName", Text = abilityName, Uppercase = true,
                ThemeTypeVariation = UIPalette.LabelRoles.StickerLine1
            };
            body.AddChild(nameLabel);

            // Stats (Outfit 400 22, ink2); a dormant slot shows the dormant glyph and its milestone line instead.
            var stats = new Label {
                Name = dormant ? "AbilityDormant" : "AbilityStats",
                Text = dormant
                    ? string.Format(
                        Tr("movelist_dormant_until"), LegacyUnlockSchedule.MilestoneLevelFor(slot))
                    : string.Format(
                        Tr("movelist_ability_stats"), ability.BaseDamage, ability.CooldownDuration),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                ThemeTypeVariation = UIPalette.LabelRoles.BodySmallDimLabel
            };
            body.AddChild(stats);
            if (dormant) PulpScreenKit.AddLeadingIcon(stats, "dormant", UIPalette.Ink3, DormantGlyphSize);

            if (!string.IsNullOrWhiteSpace(ability.DescriptionKey)) {
                var description = new Label {
                    Name = "AbilityDescription",
                    Text = Tr(ability.DescriptionKey),
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    ThemeTypeVariation = UIPalette.LabelRoles.BodySmallDimLabel
                };
                body.AddChild(description);
            }
        }

        /// <summary>The ability icon beside an ability row.</summary>
        public const float AbilityIconSize = 56f;

        /// <summary>The dormant glyph before a dormant slot's milestone line (bold below 30 px).</summary>
        public const float DormantGlyphSize = 24f;
    }
}
