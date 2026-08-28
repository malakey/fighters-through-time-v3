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

        /// <summary>Raised after the screen hides, so the opener can restore focus.</summary>
        public event Action Closed;

        /// <summary>Character the list is currently built for. Test seam.</summary>
        public string CharacterID { get; private set; } = "";

        private Control _root;
        private TextureRect _portrait;
        private Label _characterName;
        private Label _archetype;
        private VBoxContainer _sections;
        private SystemsCardScreen _systemsCard;
        private List<Control> _focusChain = new();

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
            var close = _root.GetNodeOrNull<Button>("Center/Panel/Layout/FooterRow/CloseButton");
            if (close != null) close.Pressed += Close;
            var card = _root.GetNodeOrNull<Button>("Center/Panel/Layout/FooterRow/SystemsCardButton");
            if (card != null) card.Pressed += OpenSystemsCard;
        }

        /// <summary>Builds the list for one roster character and shows the screen.</summary>
        public void Open(string characterID) {
            Populate(characterID);
            Visible = true;
            RebuildFocusChain();
        }

        /// <summary>Hides the screen and hands control back to the opener.</summary>
        public void Close() {
            if (!Visible) return;
            _systemsCard?.Close();
            Visible = false;
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

        /// <summary>The card is reachable from the Move List per the design spec.</summary>
        private void OpenSystemsCard() {
            if (_systemsCard == null || !IsInstanceValid(_systemsCard)) {
                _systemsCard = new SystemsCardScreen { Name = "SystemsCardScreen" };
                AddChild(_systemsCard);
                _systemsCard.Closed += RebuildFocusChain;
            }
            _systemsCard.Open();
        }

        // === Content =======================================================

        /// <summary>
        /// Rebuilds every data-driven row. Public seam so tests can build the
        /// list for all nine roster IDs without driving input.
        /// </summary>
        public void Populate(string characterID) {
            CharacterID = characterID ?? "";
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
            if (_characterName != null) {
                _characterName.Text = data != null && !string.IsNullOrWhiteSpace(data.DisplayNameKey)
                    ? Tr(data.DisplayNameKey)
                    : Tr("common_unknown");
            }
            if (_archetype != null) _archetype.Text = Tr(StyleKey(data?.Style ?? CombatStyle.Hybrid));

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
            AddRow("AerialStartups", string.Format(
                Tr("movelist_aerial_startups"),
                profile.AerialStartupFrames[0],
                profile.AerialStartupFrames[1],
                profile.AerialStartupFrames[2]));
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
            AddAbility("movelist_slot_special1", data?.SpecialAttackOne);
            AddAbility("movelist_slot_special2", data?.SpecialAttackTwo);
            AddAbility("movelist_slot_movement", data?.MovementAbility);
            AddAbility("movelist_slot_ultimate", data?.UltimateAttack);

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
        }

        private static string StyleKey(CombatStyle style) => style switch {
            CombatStyle.Melee => "movelist_style_melee",
            CombatStyle.Ranged => "movelist_style_ranged",
            _ => "movelist_style_hybrid"
        };

        private void AddSectionTitle(string titleKey) {
            var title = new Label {
                Name = $"Section_{titleKey}",
                Text = Tr(titleKey),
                ThemeTypeVariation = UIPalette.HeadingLabelVariation
            };
            title.AddThemeColorOverride("font_color", UIPalette.TextAccent);
            _sections.AddChild(title);
        }

        private void AddRow(string name, string text) {
            var row = new Label {
                Name = name,
                Text = text,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(780, 0)
            };
            _sections.AddChild(row);
        }

        /// <summary>
        /// One authored ability row: name, damage/cooldown, description, and
        /// icon straight off the .tres — the resource owns every number shown.
        /// </summary>
        private void AddAbility(string slotKey, AbilityData ability) {
            if (ability == null) return;
            var row = new HBoxContainer { Name = $"Ability_{slotKey}" };
            row.AddThemeConstantOverride("separation", 12);
            _sections.AddChild(row);

            var icon = new TextureRect {
                Name = "Icon",
                Texture = ability.AbilityIcon,
                CustomMinimumSize = new Vector2(36, 36),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
            };
            row.AddChild(icon);

            var body = new VBoxContainer {
                Name = "Body",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            row.AddChild(body);

            string abilityName = string.IsNullOrWhiteSpace(ability.DisplayNameKey)
                ? ability.AbilityName
                : Tr(ability.DisplayNameKey);
            var nameLabel = new Label { Name = "AbilityName", Text = $"{Tr(slotKey)} — {abilityName}" };
            body.AddChild(nameLabel);

            var stats = new Label {
                Name = "AbilityStats",
                Text = string.Format(
                    Tr("movelist_ability_stats"), ability.BaseDamage, ability.CooldownDuration),
                ThemeTypeVariation = UIPalette.SmallLabelVariation
            };
            stats.AddThemeColorOverride("font_color", UIPalette.Gold);
            body.AddChild(stats);

            if (!string.IsNullOrWhiteSpace(ability.DescriptionKey)) {
                var description = new Label {
                    Name = "AbilityDescription",
                    Text = Tr(ability.DescriptionKey),
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    CustomMinimumSize = new Vector2(720, 0),
                    ThemeTypeVariation = UIPalette.SmallLabelVariation
                };
                description.AddThemeColorOverride("font_color", UIPalette.SlateDim);
                body.AddChild(description);
            }
        }
    }
}
