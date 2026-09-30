using System.Collections.Generic;
using FTT.Combat;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 13 W7b — the Story half of the ground-wave primitive
/// (<see cref="StoryGroundWave"/>) that Righteous Smite, The Emancipator and
/// Fortissimo Wave ride: the authored travel, once per target, and the
/// grounded-only rule that lets a jump clear the wave.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryGroundWaveTests {
    private const float Step = 1f / 60f;

    [TestCase]
    public void TheFrontTravelsTheAuthoredDistanceThenStops() {
        var wave = new StoryGroundWave();
        // No physics space: the wave rides the caster's feet line.
        wave.Start(null, new Vector2(100f, 400f), movingRight: true,
            speed: 600f, distance: 150f, size: new Vector2(40f, 30f), groundedOnly: true);
        AssertThat(wave.Active).IsTrue();
        AssertThat(wave.SurfaceKnown).IsFalse();
        AssertFloat(wave.Front.Y).IsEqualApprox(400f - 15f, 0.001f);
        int steps = 0;
        while (wave.Active && steps < 100) {
            wave.Advance(Step, null, 0u, 0, null);
            steps++;
        }
        AssertThat(steps).IsEqual(15);
        AssertFloat(wave.Front.X).IsEqualApprox(250f, 0.01f);
    }

    [TestCase]
    public void AGroundedOnlyWaveSkipsAnAirborneTargetWithoutSpendingItsStrike() {
        var wave = new StoryGroundWave();
        var hurtbox = new Hurtbox();
        try {
            wave.Start(null, Vector2.Zero, true, 600f, 600f, new Vector2(40f, 30f), groundedOnly: true);
            bool airborne = true;
            wave.GroundedProbe = _ => !airborne;
            var struck = new List<Hurtbox>();
            void Strike(Hurtbox target, Vector2 front) => struck.Add(target);

            AssertThat(wave.TryStrike(hurtbox, Strike)).IsFalse();
            AssertThat(struck.Count).IsEqual(0);
            // It lands into the wave's path: now it is struck.
            airborne = false;
            AssertThat(wave.TryStrike(hurtbox, Strike)).IsTrue();
            AssertThat(struck.Count).IsEqual(1);
        } finally {
            hurtbox.Free();
        }
    }

    [TestCase]
    public void EachTargetIsStruckOncePerWaveAndAWaveThatIsNotGroundedOnlyIgnoresTheProbe() {
        var wave = new StoryGroundWave();
        var hurtbox = new Hurtbox();
        try {
            wave.Start(null, Vector2.Zero, true, 240f, 360f, new Vector2(30f, 90f), groundedOnly: false);
            wave.GroundedProbe = _ => false;
            int strikes = 0;
            void Strike(Hurtbox target, Vector2 front) => strikes++;
            AssertThat(wave.TryStrike(hurtbox, Strike)).IsTrue();
            AssertThat(wave.TryStrike(hurtbox, Strike)).IsFalse();
            AssertThat(strikes).IsEqual(1);
        } finally {
            hurtbox.Free();
        }
    }
}
