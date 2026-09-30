using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 13 W4 (S32, design §6 Hakata Bay roster): the Thunderbomb Engineer —
/// "hurls arcing <i>tetsuhau</i> ceramic thunder-bombs that leave burning pools —
/// the area-denial elite". Authored on the retained <c>neural_mech_walker</c>
/// resource with existing archetypes only: an arcing <c>Projectile</c> lob and a
/// <c>PersistentFieldAtTarget</c> burning pool (RadiantBurn) at the cast-time
/// target. The Lantern-Eye Archer and the Thunderbomb General are text-only renames
/// over their retained resources.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ThunderbombEngineerKitTests {

    private const double Step = 1.0 / 60.0;

    private static EnemyData Engineer() =>
        AuthoredResources.Load<EnemyData>("res://resources/Enemies/neural_mech_walker.tres");

    [TestCase]
    public void TheEngineerLobsArcingThunderBombsAndLeavesABurningPool() {
        EnemyData engineer = Engineer();
        AssertString(engineer.EnemyID).IsEqual("neural_mech_walker");
        AssertThat(engineer.Tier).IsEqual(EnemyTier.Elite);
        AssertThat(engineer.EliteAbilities.Length).IsEqual(2);

        EnemyAbilityData lob = engineer.EliteAbilities[0];
        AssertString(lob.AbilityID).IsEqual("enemy.neural_mech_walker.autocannon_volley");
        AssertThat(lob.Archetype).IsEqual(EnemyAbilityArchetype.Projectile);
        AssertThat(lob.ProjectileGravity > 0f).OverrideFailureMessage("A tetsuhau arcs; it is not a flat volley.").IsTrue();
        AssertThat(lob.ProjectileCount > 1).IsTrue();
        AssertThat(lob.Damage > 0f).IsTrue();

        EnemyAbilityData pool = engineer.EliteAbilities[1];
        AssertString(pool.AbilityID).IsEqual("enemy.neural_mech_walker.burning_pool");
        AssertThat(pool.Archetype).IsEqual(EnemyAbilityArchetype.PersistentFieldAtTarget);
        AssertThat(pool.AppliedStatus).IsEqual(StatusType.RadiantBurn);
        AssertThat(pool.PulseRadius > 0f).IsTrue();
        AssertThat(pool.FieldDurationSeconds > 0f).IsTrue();
        AssertThat(pool.StatusDuration > 0f).IsTrue();
    }

    [TestCase]
    public void TheHakataRosterAndBossReadTheDesignNames() {
        TranslationServer.SetLocale("en");
        AssertString(Tr(Engineer().DisplayNameKey)).IsEqual("Thunderbomb Engineer");
        AssertString(Tr(Engineer().EliteAbilities[0].DisplayNameKey)).IsEqual("Thunder-Bomb Lob");
        AssertString(Tr(Engineer().EliteAbilities[1].DisplayNameKey)).IsEqual("Burning Pool");
        var archer = AuthoredResources.Load<EnemyData>("res://resources/Enemies/infrared_border_sentry.tres");
        AssertString(Tr(archer.DisplayNameKey)).IsEqual("Lantern-Eye Archer");
        var general = AuthoredResources.Load<BossData>("res://resources/Bosses/iron_chancellor.tres");
        AssertString(general.BossID).IsEqual("iron_chancellor");
        AssertString(Tr(general.DisplayNameKey)).IsEqual("The Thunderbomb General");
        AssertString(Tr("speaker_iron_chancellor")).IsEqual("The Thunderbomb General");
        AssertString(Tr("boss_ability_artillery_barrage_name")).IsEqual("Fleet Lantern Volley");
        AssertString(Tr("stage_berlin_name")).IsEqual("Hakata Seawall");
        AssertString(Tr("stage_berlin_hazard_name")).IsEqual("Fleet Lantern");
        // Same HP and phases (S32): the General keeps the Chancellor's authored pool.
        AssertThat(general.MaxHP).IsEqual(780);
        AssertThat(general.PhaseThresholds.Length).IsEqual(1);
    }

    [TestCase]
    public void ThePoolBurnsAHeroStandingInItAndLetsGoWhenTheyLeave() {
        EnemyAbilityData poolAbility = Engineer().EliteAbilities[1];
        var player = FTT.Characters.CharacterFactory.CreateCharacter("einstein");
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(player);
        var pool = new DilationFieldZone { Name = "BurningPool" };
        pool.Configure(poolAbility);
        tree.Root.AddChild(pool);
        pool.GlobalPosition = player.GlobalPosition;
        var status = player.GetNode<StatusController>("StatusController");
        try {
            AssertThat(pool.AppliedStatus).IsEqual(StatusType.RadiantBurn);
            for (int frame = 0; frame < 10; frame++) pool._PhysicsProcess(Step);
            AssertThat(status.HasStatus(StatusType.RadiantBurn)).IsTrue();

            player.GlobalPosition = pool.GlobalPosition + new Vector2(pool.RadiusPixels + 200f, 0f);
            int frames = Mathf.CeilToInt((poolAbility.StatusDuration + 0.5f) * 60f);
            for (int frame = 0; frame < frames; frame++) {
                pool._PhysicsProcess(Step);
                status._PhysicsProcess(Step);
            }
            AssertThat(status.HasStatus(StatusType.RadiantBurn)).IsFalse();
        } finally {
            if (GodotObject.IsInstanceValid(pool)) pool.Free();
            player.Free();
        }
    }

    private static string Tr(string key) => TranslationServer.Translate(key).ToString();
}
