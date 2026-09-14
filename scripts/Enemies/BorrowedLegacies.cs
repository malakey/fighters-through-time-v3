using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;

namespace FTT.Enemies {

    /// <summary>
    /// V7.5 "Borrowed Legacies" — the First Unbound's Phase 3 identity
    /// (Package 11 A7b; plan §2.5). In its last phase the villain tears the
    /// signature moves of <b>every roster legend the player did not pick</b> out of
    /// their cradles and throws them back, so the set is built by projection at
    /// encounter start rather than authored.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The roster is never enumerated in code.</b> It comes from the content
    /// manifest through <see cref="CampaignCaptiveRoster"/> (the same
    /// manifest-backed source the Mystery Thread's captive tokens use), minus the
    /// active hero. A tenth character widens the borrowed set with no code change —
    /// that is the scaling rule the content test pins.
    /// </para>
    /// <para>
    /// Each borrowed character contributes its <b>Special 1</b>, loaded through
    /// <see cref="AuthoredResources"/> and projected into a runtime
    /// <see cref="EnemyAbilityData"/>: archetype by ability shape, damage from
    /// <see cref="AbilityData.BaseDamage"/>, telegraph from the authored startup
    /// frames, and <i>everything else left at the archetype's own defaults</i>.
    /// Nothing bespoke is invented, because the projection must stay honest about
    /// being a projection.
    /// </para>
    /// <para>
    /// The results are <b>runtime-only</b> instances. They are never written to
    /// disk, never entered in the content manifest, and never pass through the
    /// <see cref="AuthoredResources"/> cache — that cache pins authored tuning for
    /// the process lifetime, and a per-encounter construction has no business in
    /// it. The <i>sources</i> they read do go through the cache, which is what keeps
    /// the C#-scripted ability graphs alive rather than churning them past the
    /// finalizer thread.
    /// </para>
    /// </remarks>
    public static class BorrowedLegacies {

        /// <summary>Prefix every projected ability ID carries, so a borrowed move is
        /// always distinguishable from an authored one at a glance and in logs.</summary>
        public const string AbilityIDPrefix = "borrowed";

        /// <summary>
        /// Projects the borrowed kit for a campaign locked to
        /// <paramref name="activeHeroID"/>. Returns <c>rosterCount - 1</c> abilities
        /// for a roster hero, in manifest order; an empty array when the roster is
        /// unavailable. Never returns <c>null</c>.
        /// </summary>
        public static EnemyAbilityData[] ProjectFor(string activeHeroID) =>
            ProjectFor(activeHeroID, CampaignCaptiveRoster.Roster());

        /// <summary>
        /// Pure overload over an explicit roster — the seam the scaling test drives
        /// so it can prove the rule against a hypothetical tenth character without
        /// authoring one.
        /// </summary>
        public static EnemyAbilityData[] ProjectFor(string activeHeroID, IEnumerable<string> roster) {
            IReadOnlyList<string> borrowed = CampaignCaptiveRoster.For(activeHeroID, roster);
            var projected = new List<EnemyAbilityData>(borrowed.Count);
            foreach (string characterID in borrowed) {
                EnemyAbilityData ability = ProjectCharacter(characterID);
                if (ability != null) projected.Add(ability);
            }
            return projected.ToArray();
        }

        /// <summary>
        /// Projects one character's Special 1. Returns <c>null</c> when the
        /// character's data or Special 1 cannot be resolved — a missing cradle
        /// narrows the borrowed set rather than taking the encounter down.
        /// </summary>
        public static EnemyAbilityData ProjectCharacter(string characterID) {
            if (string.IsNullOrWhiteSpace(characterID)) return null;
            CharacterData character;
            try {
                character = AuthoredResources.Load<CharacterData>(
                    $"res://resources/Characters/{characterID}_data.tres");
            } catch (Exception error) {
                Godot.GD.PushWarning(
                    $"Borrowed Legacies could not load '{characterID}': {error.Message}");
                return null;
            }
            return Project(characterID, character?.SpecialAttackOne);
        }

        /// <summary>
        /// The projection itself, free of resource loading so it can be driven from
        /// an arbitrary <see cref="AbilityData"/> in a test.
        /// </summary>
        public static EnemyAbilityData Project(string characterID, AbilityData source) {
            if (source == null || string.IsNullOrWhiteSpace(characterID)) return null;
            return new EnemyAbilityData {
                AbilityID = $"{AbilityIDPrefix}.{characterID}.{SlotSuffix}",
                DisplayNameKey = source.DisplayNameKey,
                Archetype = ArchetypeFor(source.ExecutionType),
                // Damage and telegraph are the two values §2.5 carries across; every
                // other field stays at the archetype default.
                Damage = source.BaseDamage,
                TelegraphFrames = Math.Max(0, source.StartupFrames),
                // The cradle hook: the borrowed character's own canonical ability ID,
                // which is exactly what AbilityVisualLibrary resolves, so the move
                // tears out wearing its owner's effect instead of a generic boss one.
                PresentationEventID = source.AbilityID
            };
        }

        /// <summary>Suffix naming the borrowed slot; only Special 1 is borrowed today.</summary>
        public const string SlotSuffix = "special_1";

        /// <summary>
        /// §2.5's archetype-by-ability-shape rule. Projectile-shaped abilities
        /// become <see cref="EnemyAbilityArchetype.Projectile"/>, zone-shaped ones
        /// (an area burst or a placed construct) become
        /// <see cref="EnemyAbilityArchetype.AreaPulse"/>, and everything else —
        /// melee, and the two shapes a Special 1 never uses — falls to
        /// <see cref="EnemyAbilityArchetype.MeleeStrike"/>, which is also
        /// <see cref="EnemyAbilityData"/>'s own default.
        /// </summary>
        public static EnemyAbilityArchetype ArchetypeFor(AbilityExecutionType executionType) =>
            executionType switch {
                AbilityExecutionType.Projectile => EnemyAbilityArchetype.Projectile,
                AbilityExecutionType.Area => EnemyAbilityArchetype.AreaPulse,
                AbilityExecutionType.PersistentObject => EnemyAbilityArchetype.AreaPulse,
                _ => EnemyAbilityArchetype.MeleeStrike
            };
    }
}
