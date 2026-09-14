using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// V7.6 (Package 11 A7a): the Eraser's authored elite contract, mirroring
/// <c>TimelineIntegrityTests.TheChronoWardenMatchesItsAuthoredEliteContract</c>
/// — the Warden is this enemy's worked template, down to reusing another mob's
/// sprite sheet until its own art exists.
///
/// <para>The resource ID is <c>unbound_eraser</c> rather than the design prose's
/// bare "Eraser": two BOSSES already carry that word (<c>tidal_eraser</c> at
/// Level 5, <c>apex_eraser</c> at Level 15), and a bare ID would make every grep
/// and the VFX library's owner parse ambiguous against them.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EraserContentTests {

    [TestCase]
    public void TheEraserMatchesItsAuthoredEliteContract() {
        var eraser = AuthoredResources.Load<EnemyData>("res://resources/Enemies/unbound_eraser.tres");
        AssertObject(eraser).IsNotNull();
        AssertString(eraser.EnemyID).IsEqual("unbound_eraser");
        AssertThat(eraser.Tier).IsEqual(EnemyTier.Elite);
        AssertThat(eraser.MaxHP).IsEqual(190);
        AssertFloat(eraser.StunResistance).IsEqual(0.5f);

        // "Chase" in the design is the OPPOSITE of StandGuard, which is already
        // the default Ground behaviour plus aggro — no enum value was appended.
        // What makes it a hunter is the room-scale aggro pair: the design says it
        // "ignores siphon-defence positioning", so there is no corner of a room it
        // will not cross. The Chrono-Warden's 640/940 is the contrast.
        AssertThat(eraser.Behavior).IsEqual(DefaultBehavior.Ground);
        AssertFloat(eraser.AggroRadius).IsGreaterEqual(3000f);
        AssertFloat(eraser.DeAggroRadius).IsGreater(eraser.AggroRadius);

        // Two elite abilities, alternating, plus a melee primary. The primary is
        // deliberately NOT a projectile: EnemyController widens AttackRangePixels
        // to 90% of the aggro radius for a ranged primary, which against a
        // room-scale aggro would make the hunter attack from across the room and
        // never close.
        AssertObject(eraser.PrimaryAttack).IsNotNull();
        AssertThat(eraser.PrimaryAttack.Archetype).IsEqual(EnemyAbilityArchetype.MeleeStrike);
        AssertThat(eraser.PrimaryAttack.SpawnsProjectiles).IsFalse();
        AssertThat(eraser.EliteAbilities.Length).IsEqual(2);

        EnemyAbilityData lance = eraser.EliteAbilities[0];
        EnemyAbilityData snare = eraser.EliteAbilities[1];
        AssertObject(lance).IsNotNull();
        AssertObject(snare).IsNotNull();

        // Null Lance: a slow, visible bolt carrying 2 s of Suppression, and
        // Basic-class against the block because "blocking is never a trap".
        AssertString(lance.AbilityID).IsEqual("enemy.unbound_eraser.null_lance");
        AssertThat(lance.Archetype).IsEqual(EnemyAbilityArchetype.Projectile);
        AssertThat(lance.RangeClass).IsEqual(EnemyAbilityRangeClass.Ranged);
        AssertThat(lance.TelegraphFrames).IsEqual(30);
        AssertFloat(lance.CooldownSeconds).IsEqual(8f);
        AssertThat(lance.AppliedStatus).IsEqual(StatusType.Suppression);
        AssertFloat(lance.StatusDuration).IsEqual(2f);
        AssertThat(lance.ForcesBasicBlockClass)
            .OverrideFailureMessage("The Null Lance must override the implicit elite Guard-Crush.")
            .IsTrue();
        AssertFloat(lance.ProjectileSpeed)
            .OverrideFailureMessage("The lance is authored as a slow, visible bolt.")
            .IsLess(300f);

        // Siphon Snare: the F14 channel. Its numbers are pinned in detail by
        // SiphonSnareTests; this is the roster-side identity check.
        AssertString(snare.AbilityID).IsEqual("enemy.unbound_eraser.siphon_snare");
        AssertThat(snare.Archetype).IsEqual(EnemyAbilityArchetype.SiphonTether);
        AssertThat(snare.TelegraphFrames).IsEqual(45);
        AssertFloat(snare.CooldownSeconds).IsEqual(10f);

        // Every kit validates against its own archetype's required fields.
        foreach (EnemyAbilityData ability in new[] { eraser.PrimaryAttack, lance, snare }) {
            AssertThat(ability.HasValidArchetypeFields())
                .OverrideFailureMessage($"'{ability.AbilityID}' fails its archetype validation.")
                .IsTrue();
        }

        // Localization resolves through the COMPILED translation, not just the CSV.
        TranslationServer.SetLocale("en");
        foreach (string key in new[] {
            eraser.DisplayNameKey, lance.DisplayNameKey, snare.DisplayNameKey,
            eraser.PrimaryAttack.DisplayNameKey
        }) {
            AssertThat(TranslationServer.Translate(key).ToString())
                .OverrideFailureMessage($"'{key}' does not resolve through en.en.translation.")
                .IsNotEqual(key);
        }

        // The manifest row exists and points back at the same ID.
        ContentManifest manifest = ContentManifest.LoadDefault();
        ContentManifestEntry row = null;
        foreach (ContentManifestEntry entry in manifest.ForCategory(ContentCategory.Enemy)) {
            if (entry.ContentID == "unbound_eraser") { row = entry; break; }
        }
        AssertObject(row)
            .OverrideFailureMessage("The Eraser needs a content_manifest.csv Enemy row.")
            .IsNotNull();
        AssertString(row.ResourcePath).IsEqual("res://resources/Enemies/unbound_eraser.tres");
    }
}
