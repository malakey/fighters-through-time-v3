using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// CM-STORY (combat and movement update 2026-10-16, decision 7.3; findings
/// CMB-16, TWO-10; ledger <c>USER-STORY-ECHO-STEP-COST</c>, amending
/// <c>PROVISIONAL-STORY-METER-SCALE-RULES</c> (5)): the campaign hero earns meter
/// at the D5 Story scale (×0.35), so his Echo Step now costs 30 × 0.35 = 10.5
/// meter (<see cref="StoryCombatRules.StoryEchoStepMeterCost"/>, derived, never a
/// second 0.35). Every build without the D5 scale — the Level 13 Mirror clone,
/// the drills, the Holodeck, Fighter Mode, the bots' unscaled A/B — keeps the
/// cross-mode 30. A refused step spends nothing.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryEchoStepCostTests {
    private const GameplayButtons Attack = GameplayButtons.BasicAttack;
    private const GameplayButtons EchoStep = GameplayButtons.EchoStep;
    private const GameplayButtons None = GameplayButtons.None;

    [TestCase]
    public void TheCampaignHeroEchoStepsAtTenAndAHalfAndIsRefusedJustBelowIt() {
        foreach ((float meter, bool accepted) in new[] { (10.5f, true), (10.4f, false) }) {
            StaticBody2D floor = null;
            PlayerController hero = null;
            try {
                floor = CreateFlatFloor();
                // The default build is the campaign hero: the D5 scale and its cost.
                hero = CreateGroundedHero("joan", applyStoryProgression: true);
                AssertThat(hero.AppliesStoryMeterGainScale).IsTrue();
                AssertThat(hero.EchoStepMeterCost).IsEqualApprox(10.5f, 0.0001f);
                TryEchoStepFromASwing(hero, meter, out bool stepped);
                AssertThat(stepped)
                    .OverrideFailureMessage($"At {meter} meter the campaign hero's Echo Step accepted={stepped}.")
                    .IsEqual(accepted);
                if (accepted) {
                    AssertThat(hero.CurrentUltimateMeter).IsEqualApprox(meter - 10.5f, 0.0001f);
                } else {
                    AssertThat(hero.CurrentUltimateMeter)
                        .OverrideFailureMessage("A refused Echo Step spends no meter.")
                        .IsEqualApprox(meter, 0.0001f);
                    AssertThat(hero.EchoStepCooldownFramesRemaining)
                        .OverrideFailureMessage("A refused Echo Step arms no cooldown.")
                        .IsEqual(0);
                }
            } finally {
                Release(hero, floor);
            }
        }
    }

    [TestCase]
    public void ABuildWithoutTheStoryScaleStillNeedsTheFullThirty() {
        foreach ((float meter, bool accepted) in new[] { (29.5f, false), (30f, true) }) {
            StaticBody2D floor = null;
            PlayerController hero = null;
            try {
                floor = CreateFlatFloor();
                // The Mirror clone, the drills, the Holodeck and Fighter Mode all
                // build without the campaign-hero branch.
                hero = CreateGroundedHero("joan", applyStoryProgression: false);
                AssertThat(hero.AppliesStoryMeterGainScale).IsFalse();
                AssertThat(hero.EchoStepMeterCost).IsEqualApprox(BasicComboRules.EchoStepMeterCost, 0.0001f);
                TryEchoStepFromASwing(hero, meter, out bool stepped);
                AssertThat(stepped)
                    .OverrideFailureMessage($"At {meter} meter an unscaled build's Echo Step accepted={stepped}.")
                    .IsEqual(accepted);
                AssertThat(hero.CurrentUltimateMeter)
                    .IsEqualApprox(accepted ? meter - BasicComboRules.EchoStepMeterCost : meter, 0.0001f);
            } finally {
                Release(hero, floor);
            }
        }
    }

    [TestCase]
    public void TheStoryCostIsTheCrossModeCostTimesTheStoryMeterScale() {
        AssertThat(StoryCombatRules.StoryEchoStepMeterCost)
            .IsEqual(BasicComboRules.EchoStepMeterCost * StoryCombatRules.MeterGainScale);
        AssertThat(StoryCombatRules.StoryEchoStepMeterCost).IsEqualApprox(10.5f, 0.00001f);
        AssertThat(StoryCombatRules.EchoStepMeterCost(storyScaled: true))
            .IsEqual(StoryCombatRules.StoryEchoStepMeterCost);
        AssertThat(StoryCombatRules.EchoStepMeterCost(storyScaled: false))
            .IsEqual((float)BasicComboRules.EchoStepMeterCost);
        AssertThat(BasicComboRules.EchoStepMeterCost)
            .OverrideFailureMessage("Fighter Mode, the CPU and the drill keep the cross-mode 30.")
            .IsEqual(30);
    }

    // === Harness ===

    /// <summary>
    /// Fills a full 31-tick Echo Step history standing still, sets the meter,
    /// starts a grounded string hit 1 into the air, and presses the direct Echo
    /// Step bind on every tick of the swing until it either winds up or the
    /// swing ends.
    /// </summary>
    private static void TryEchoStepFromASwing(PlayerController hero, float meter, out bool stepped) {
        for (int tick = 0; tick < BasicComboRules.EchoStepRingSamples + 4; tick++) Step(hero, None);
        AssertThat(hero.EchoStepRingValidCount).IsEqual(BasicComboRules.EchoStepRingSamples);
        hero.SetUltimateMeter(meter);
        Step(hero, Attack);
        AssertThat(hero.CurrentState).OverrideFailureMessage("Harness: the swing did not start.").IsEqual(CharacterState.Attacking);
        stepped = false;
        for (int tick = 0; tick < 90 && hero.CurrentState == CharacterState.Attacking; tick++) {
            Step(hero, EchoStep);
            if (hero.EchoStepWindingUp) {
                stepped = true;
                break;
            }
        }
    }

    /// <summary>One tick with <paramref name="held"/> freshly pressed (nothing was held before).</summary>
    private static void Step(PlayerController hero, GameplayButtons held) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, held, None));
        InputManager.Instance.SetInputSource(hero.PlayerIndex, source);
        hero._PhysicsProcess(1.0 / 60.0);
    }

    private static StaticBody2D CreateFlatFloor() {
        var floor = new StaticBody2D {
            Name = "StoryEchoStepCostFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(4000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(floor);
        return floor;
    }

    private static PlayerController CreateGroundedHero(string heroID, bool applyStoryProgression) {
        PlayerController hero = CharacterFactory.CreateCharacter(heroID, 0, applyStoryProgression: applyStoryProgression);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(hero);
        try {
            hero.GlobalPosition = new Vector2(0f, -8f);
            for (int frame = 0; frame < 120 && !hero.IsOnFloor(); frame++) {
                hero.Velocity = new Vector2(hero.Velocity.X, 400f);
                Step(hero, None);
            }
            for (int frame = 0; frame < 10 && hero.CurrentState != CharacterState.Idle; frame++) Step(hero, None);
            AssertThat(hero.CurrentState).IsEqual(CharacterState.Idle);
            hero.Velocity = Vector2.Zero;
            return hero;
        } catch {
            Release(hero, null);
            throw;
        }
    }

    private static void Release(PlayerController hero, StaticBody2D floor) {
        if (hero != null && GodotObject.IsInstanceValid(hero)) {
            InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
            hero.Free();
        }
        if (floor != null && GodotObject.IsInstanceValid(floor)) floor.Free();
    }
}
