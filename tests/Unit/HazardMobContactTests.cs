using System;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>A level with nothing in it, for reaching the base's spike builder without booting a level.</summary>
internal partial class HazardSpikeFixtureLevel : StoryLevelControllerBase {
    public override string LevelID => "hazard_spike_fixture";
    public override CampaignLevel Level => CampaignLevel.Orleans;
    public override string LevelTitleKey => "";
    public override string DialogueSetPath => "";
    public override Vector2 PlayerSpawnPosition => Vector2.Zero;
    public override Rect2 LevelBounds => new(0, 0, 100, 100);
    protected override void BuildLevel() { }
    public Area2D Spikes(float x, float y, float width) => BuildHazardSpikes(x, y, width);
}

/// <summary>
/// Ruling 7d, plan section 1.13: each hazard type, in real physics frames. A mob on its feet is
/// the regression guard (the widened mask must not hurt it); the same mob thrown takes one impact.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class HazardMobContactTests {
    private const float X = HazardMobTestKit.OriginX;
    private static readonly Vector2 Inside = new(X, -60f);
    private static readonly Vector2 Outside = new(X + 900f, 0f);

    [TestCase]
    public async Task ACyclicHazardSparesAWalkingMobAndHitsAThrownOneOnce() {
        var hazard = new StoryCyclicHazard { HazardID = "gr_haz_cyclic", Position = Inside };
        hazard.AddChild(HazardMobTestKit.RectShape(new Vector2(240f, 240f), Vector2.Zero));
        await WithHazard(hazard, async mob => {
            hazard.ForcePhase(HazardPhase.Active, 60f);
            await Sequence(mob, Inside);
        });
    }

    [TestCase]
    public async Task SpikesSpareAWalkingMobAndHitAThrownOneOnce() {
        var fixture = new HazardSpikeFixtureLevel();
        Area2D spikes = fixture.Spikes(X, -20f, 240f);
        fixture.RemoveChild(spikes);
        fixture.Free();
        await WithHazard(spikes, async mob => await Sequence(mob, new Vector2(X, -8f)));
    }

    [TestCase]
    public async Task FlorencesOwnSpikesSpareAWalkingMobAndHitAThrownOneOnce() {
        var level = new Level01Controller { Name = "FlorenceSpikeFixture" };
        HazardMobTestKit.Tree.Root.AddChild(level);
        EnemyController mob = HazardMobTestKit.CreateMob(HazardMobTestKit.Slasher, new Vector2(600f, 900f));
        try {
            AssertObject(level.GetNodeOrNull<Area2D>("Spikes_600")).IsNotNull();
            int hp0 = mob.CurrentHP;
            await HazardMobTestKit.Frames(4);
            AssertThat(mob.CurrentHP).IsEqual(hp0);

            mob.GlobalPosition = new Vector2(600f, 500f);
            await HazardMobTestKit.Frames(3);
            HazardMobTestKit.Throw(mob);
            mob.GlobalPosition = new Vector2(600f, 895f);
            await HazardMobTestKit.Frames(3);
            AssertThat(hp0 - mob.CurrentHP).IsEqual(HazardMobRules.StandardImpactDamage);
        } finally {
            mob.Free();
            level.Free();
        }
    }

    [TestCase]
    public async Task AStageHazardSparesAWalkingMobAndHitsAThrownOneOnce() {
        var hazard = new StageHazard {
            Position = Inside,
            WarningDuration = 0.02f,
            ActiveDuration = 60f,
            CooldownDuration = 0.02f,
            Damage = 15f
        };
        var zone = new Area2D { Name = "DamageZone", CollisionLayer = CollisionLayers.Trigger };
        zone.AddChild(HazardMobTestKit.RectShape(new Vector2(240f, 240f), Vector2.Zero));
        hazard.AddChild(zone);
        await WithHazard(hazard, async mob => {
            await HazardMobTestKit.Frames(4);
            await Sequence(mob, Inside);
        });
    }

    [TestCase]
    public async Task AnExtractorDischargeSparesAWalkingMobAndHitsAThrownOneOnce() {
        var extractor = ResourceLoader.Load<PackedScene>("res://scenes/templates/ChronalExtractorTemplate.tscn")
            .Instantiate<ChronalExtractor>();
        extractor.ObjectID = "gr_haz_extractor";
        extractor.Position = new Vector2(X, -72f);
        // Beside the machine's solid body, inside its 190 px discharge circle.
        var feet = new Vector2(X + 120f, 0f);
        await WithHazard(extractor, async mob => {
            mob.GlobalPosition = feet;
            await HazardMobTestKit.Frames(3);
            int hp0 = mob.CurrentHP;
            extractor.Discharge();
            AssertThat(mob.CurrentHP)
                .OverrideFailureMessage("A discharge hurt a mob on its feet.")
                .IsEqual(hp0);

            HazardMobTestKit.Throw(mob);
            await HazardMobTestKit.Frames(2);
            extractor.Discharge();
            AssertThat(hp0 - mob.CurrentHP).IsEqual(HazardMobRules.StandardImpactDamage);
            extractor.Discharge();
            AssertThat(hp0 - mob.CurrentHP)
                .OverrideFailureMessage("One launch pays one impact per source.")
                .IsEqual(HazardMobRules.StandardImpactDamage);
        });
    }

    [TestCase]
    public async Task AFloodSparesAWalkingMobAndHitsAThrownOneOnlyBelowItsLine() {
        var water = new RisingWaterZone {
            WaterID = "gr_haz_flood",
            Position = new Vector2(X, -80f),
            StepHeights = new[] { 0f },
            LabelKey = ""
        };
        water.AddChild(HazardMobTestKit.RectShape(new Vector2(600f, 800f), Vector2.Zero));
        await WithHazard(water, async mob => {
            // On its feet, submerged: safe.
            mob.GlobalPosition = new Vector2(X, 0f);
            await HazardMobTestKit.Frames(4);
            int hp0 = mob.CurrentHP;
            AssertThat(hp0).IsEqual(mob.ScaledMaxHP);

            // Thrown, in the zone but over the water line: safe.
            HazardMobTestKit.Throw(mob);
            mob.GlobalPosition = new Vector2(X, -240f);
            mob.Velocity = Vector2.Zero;
            await HazardMobTestKit.Frames(2);
            AssertThat(mob.GlobalPosition.Y < -80f).IsTrue();
            AssertThat(mob.CurrentHP)
                .OverrideFailureMessage("A body over the line is not in the water.")
                .IsEqual(hp0);

            // Below it: one impact, however long it stays in.
            mob.GlobalPosition = new Vector2(X, -40f);
            await HazardMobTestKit.Frames(3);
            AssertThat(hp0 - mob.CurrentHP).IsEqual(HazardMobRules.StandardImpactDamage);
            await HazardMobTestKit.Frames(6);
            AssertThat(hp0 - mob.CurrentHP).IsEqual(HazardMobRules.StandardImpactDamage);
            AssertThat(water.TrackedPlayerCount).IsEqual(0);
        });
    }

    [TestCase]
    public async Task ALavaFrontSparesAWalkingMobAndHitsAThrownOneOnce() {
        var escape = ResourceLoader.Load<PackedScene>("res://scenes/templates/EscapeSequenceControllerTemplate.tscn")
            .Instantiate<EscapeSequenceController>();
        escape.SequenceID = "gr_haz_lava";
        escape.StartX = X + 160f;
        escape.EndX = X + 161f;
        escape.FinishX = X + 50000f;
        escape.AdvanceSpeed = 1f;
        await WithHazard(escape, async mob => {
            escape.Begin();
            await Sequence(mob, Inside);
            AssertThat(escape.CatchCount)
                .OverrideFailureMessage("A mob is not a caught hero.")
                .IsEqual(0);
        });
    }

    [TestCase]
    public async Task AShelteredMobAndTheStoryHostileCloneTakeNothing() {
        var hazard = new StoryCyclicHazard { HazardID = "gr_haz_cover", Position = Inside };
        hazard.AddChild(HazardMobTestKit.RectShape(new Vector2(600f, 240f), Vector2.Zero));
        var cover = new VolleyCover { Name = "Mantlet", ShelterSize = new Vector2(120f, 400f) };
        hazard.AddChild(cover);
        PlayerController clone = CharacterFactory.CreateCharacter("einstein", 1, applyStoryProgression: false);
        clone.IsStoryHostile = true;
        await WithHazard(hazard, async mob => {
            hazard.ForcePhase(HazardPhase.Active, 60f);
            HazardMobTestKit.Tree.Root.AddChild(clone);
            clone.CollisionLayer = CollisionLayers.Enemy;
            clone.GlobalPosition = Inside + new Vector2(200f, 0f);
            HazardMobTestKit.Throw(mob);
            mob.GlobalPosition = cover.GlobalPosition + new Vector2(0f, 20f);
            mob.Velocity = Vector2.Zero;
            AssertThat(hazard.IsSheltered(mob.GlobalPosition)).IsTrue();
            int mobHP = mob.CurrentHP;
            int cloneHP = clone.CurrentHP;
            await HazardMobTestKit.Frames(2);
            AssertThat(mob.CurrentHP)
                .OverrideFailureMessage("Cover shelters a mob as it shelters the hero.")
                .IsEqual(mobHP);
            AssertThat(clone.CurrentHP)
                .OverrideFailureMessage("The Mirror clone stands on the Enemy layer; the wider mask must not reach it.")
                .IsEqual(cloneHP);

            // Out from cover into the same volley: the source was not spent.
            mob.GlobalPosition = hazard.GlobalPosition + new Vector2(-200f, 0f);
            await HazardMobTestKit.Frames(2);
            AssertThat(mobHP - mob.CurrentHP).IsEqual(HazardMobRules.StandardImpactDamage);
        });
        clone.Free();
    }

    private static async Task WithHazard(Node hazard, Func<EnemyController, Task> body) {
        StaticBody2D floor = HazardMobTestKit.CreateFloor();
        HazardMobTestKit.Tree.Root.AddChild(hazard);
        EnemyController mob = HazardMobTestKit.CreateMob(HazardMobTestKit.Slasher, Outside);
        try {
            await HazardMobTestKit.Frames(2);
            await body(mob);
        } finally {
            mob.Free();
            hazard.Free();
            floor.Free();
        }
    }

    /// <summary>The walking regression and the thrown hit, at <paramref name="inside"/>.</summary>
    private static async Task Sequence(EnemyController mob, Vector2 inside) {
        int hp0 = mob.CurrentHP;
        // On its feet, on the floor under the hazard or walking into it.
        mob.GlobalPosition = new Vector2(inside.X, 0f);
        await HazardMobTestKit.Frames(6);
        AssertThat(mob.IsHazardVulnerable).IsFalse();
        AssertThat(mob.CurrentHP)
            .OverrideFailureMessage("A hazard hurt a mob on its feet.")
            .IsEqual(hp0);

        mob.GlobalPosition = Outside;
        await HazardMobTestKit.Frames(3);
        HazardMobTestKit.Throw(mob);
        mob.GlobalPosition = inside;
        await HazardMobTestKit.Frames(3);
        AssertThat(hp0 - mob.CurrentHP).IsEqual(HazardMobRules.StandardImpactDamage);
        await HazardMobTestKit.Frames(6);
        AssertThat(hp0 - mob.CurrentHP)
            .OverrideFailureMessage("One launch pays one impact per source.")
            .IsEqual(HazardMobRules.StandardImpactDamage);
    }
}
