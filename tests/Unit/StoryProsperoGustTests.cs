using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 13 W7a (A08) — Prospero's Flight in Story is a single gust burst:
/// 4 units (240 px) forward and 2.5 units (150 px) up over 20 frames, then a
/// normal fall with no glide. The Story-only Midsummer Gust major (node and perk
/// key kept as <c>midsummer_glide</c>) carries it 20% farther.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryProsperoGustTests {
    private const double Step = 1.0 / 60.0;

    [TestCase]
    public void TheGustBurstsFourUnitsForwardAndTwoAndAHalfUpThenEnds() {
        var host = new Node2D { Name = "ProsperoGust" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        try {
            PlayerController shakespeare = CharacterFactory.CreateCharacter("shakespeare", 0, applyStoryProgression: false);
            host.AddChild(shakespeare);
            var flight = shakespeare.GetNode<ShakespeareProsperosFlight>("MovementAbility");
            AssertThat(flight.Data.ActiveFrames).IsEqual(KitMotionRules.ProsperoGustFrames);
            AssertThat(flight.TryExecute()).IsTrue();
            for (int frame = 0; frame < 5 && !flight.IsGusting; frame++) flight._PhysicsProcess(Step);
            AssertThat(flight.IsGusting).IsTrue();
            float facing = shakespeare.IsFacingRight ? 1f : -1f;
            // 240 px over 20 frames = 720 px/s; 150 px over 20 frames = 450 px/s up.
            AssertThat(Mathf.Abs(flight.GustVelocity.X - facing * 720f) < 0.5f).IsTrue();
            AssertThat(Mathf.Abs(flight.GustVelocity.Y + 450f) < 0.5f).IsTrue();
            AssertThat(shakespeare.Velocity).IsEqual(flight.GustVelocity);

            for (int frame = 0; frame < 30 && flight.IsGusting; frame++) flight._PhysicsProcess(Step);
            // A burst, not a glide: it ends with its momentum spent.
            AssertThat(flight.IsGusting).IsFalse();
            AssertThat(shakespeare.Velocity).IsEqual(Vector2.Zero);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void MidsummerGustCarriesTheBurstTwentyPercentFarther() {
        var host = new Node2D { Name = "MidsummerGust" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        try {
            PlayerController shakespeare = CharacterFactory.CreateCharacter("shakespeare", 0, applyStoryProgression: false);
            host.AddChild(shakespeare);
            shakespeare.StoryAbilityPerks.Add(ShakespeareProsperosFlight.MidsummerGlidePerkKey);
            var flight = shakespeare.GetNode<ShakespeareProsperosFlight>("MovementAbility");
            AssertThat(flight.TryExecute()).IsTrue();
            for (int frame = 0; frame < 5 && !flight.IsGusting; frame++) flight._PhysicsProcess(Step);
            AssertThat(Mathf.Abs(Mathf.Abs(flight.GustVelocity.X) - 720f * 1.2f) < 0.5f).IsTrue();
            AssertThat(ShakespeareProsperosFlight.MidsummerGustDamage).IsEqual(8f);
        } finally {
            host.Free();
        }
    }
}
