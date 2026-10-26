using System.Collections.Generic;
using System.IO;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using Godot;
using xpTURN.Klotho.Deterministic.Math;
using static FTT.Tests.Determinism.KnockbackFeelTestKit;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// FU-KNOCK R1 (feel and UI update 2026-10-25; ledger
/// <c>USER-FIGHTER-FINISHER-2X</c>): the Fighter finisher flies twice as far.
/// Its horizontal launch speed is 2 x the force and its vertical stays the
/// force, so the arc and airtime are unchanged and the travel doubles. The
/// missing-HP ramp and the weight term still apply, and Story's finisher (the
/// profile tenths and the shared table) is untouched.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterFinisherKnockbackTests {

    /// <summary>Raw tolerance for the (2F, F) pair: the halved vertical rounds by at most a few raw units.</summary>
    private const long RawTolerance = 8;

    [TestCase]
    public void EveryKitsFinisherLaunchesWithTwiceTheForceAcrossAndTheForceUpAtFullAndHalfHP() {
        var issues = new List<string>();
        foreach (string id in CharacterRoster.IDs) {
            var data = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{id}_data.tres");
            FighterLoadout kit = FighterLoadoutFactory.FromCharacterData(data);
            BasicStringProfile profile = BasicComboRules.StringProfileFor(id);

            // Full HP first; its damage sizes the half-HP victim (MaxHP = 2 x the hit).
            (FighterSimulation fullSim, _, int damage) = WalkToHit(kit, kit, step: 2);
            foreach (bool half in new[] { false, true }) {
                FighterSimulation sim = fullSim;
                if (half) {
                    var halfData = (CharacterData)data.Duplicate();
                    halfData.MaxHP = damage * 2;
                    (sim, _, _) = WalkToHit(kit, FighterLoadoutFactory.FromCharacterData(halfData), step: 2);
                }
                sim.TryGetFighter(1, out FighterStateComponent victim);
                double hpFraction = (double)victim.CurrentHP / victim.MaxHP;
                double scale = 2.0 - hpFraction;
                double expected = D(kit.BasicKnockback) * profile.FinisherKnockbackTenths / 10.0 * scale
                    / (1.0 + D(victim.Weight));
                double vx = D(victim.Velocity.x);
                double vy = D(victim.Velocity.y);
                string label = $"{id} {(half ? "50%" : "100%")}";
                if (System.Math.Abs(vy - expected) > 0.02) issues.Add($"{label}: vertical {vy:0.000} != force {expected:0.000}.");
                if (System.Math.Abs(vx - 2 * expected) > 0.04) issues.Add($"{label}: horizontal {vx:0.000} != 2 x force {2 * expected:0.000}.");
                if (System.Math.Abs(victim.Velocity.x.RawValue - 2 * victim.Velocity.y.RawValue) > RawTolerance) {
                    issues.Add($"{label}: vx is not 2 x vy.");
                }
                if (half && System.Math.Abs(hpFraction - 0.5) > 0.01) issues.Add($"{label}: setup HP fraction {hpFraction:0.00}.");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void TheFinisherTravelsAboutTwiceAsFarAsBeforeAtTheSameAirtime() {
        // Mirror matchups from mid-stage, no DI, landing ends the travel. The
        // "before" figures were measured on 2026-10-25 against c30d7cb8 (the
        // commit this change was cut from) with the same protocol, in units.
        (string Id, double Before, int AirborneTicksBefore)[] cases = {
            ("cleopatra", 2.09, 28),
            ("leonardo", 2.60, 31),
            ("einstein", 3.17, 34)
        };
        var issues = new List<string>();
        foreach ((string id, double before, int airBefore) in cases) {
            var data = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{id}_data.tres");
            FighterLoadout kit = FighterLoadoutFactory.FromCharacterData(data);
            (FighterSimulation sim, int connect, _) = WalkToHit(kit, kit, step: 2);
            sim.TryGetFighter(1, out FighterStateComponent struck);
            double startX = D(struck.Position.x);
            bool left = false;
            int airborne = 0;
            double travel = -1;
            int tick = connect + 1;
            for (int i = 0; i < 120 && travel < 0; i++) {
                Idle(sim, ref tick);
                sim.TryGetFighter(1, out FighterStateComponent f);
                if (f.IsGrounded == 0) { left = true; airborne++; }
                else if (left) travel = System.Math.Abs(D(f.Position.x) - startX);
            }
            if (travel < 0) { issues.Add($"{id}: never landed."); continue; }
            if (airborne != airBefore) issues.Add($"{id}: airtime {airborne} != {airBefore} (the arc must not change).");
            double ratio = travel / before;
            if (ratio < 1.9 || ratio > 2.1) issues.Add($"{id}: travel {travel:0.00} is {ratio:0.00}x the old {before:0.00}.");
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void StorysFinisherAndTheSharedTableAreUntouchedAndNothingOutsideTheSimReadsTheNewRules() {
        AssertThat(BasicComboRules.KnockbackMultipliers[2]).IsEqual(4.5f);
        foreach (string id in CharacterRoster.IDs) {
            int expectedTenths = id == "mozart" ? 55 : 45;
            AssertThat(BasicComboRules.StringProfileFor(id).FinisherKnockbackTenths)
                .OverrideFailureMessage($"{id}: Story's finisher tenths moved.")
                .IsEqual(expectedTenths);
        }

        // Source scan: only the rulebook, the sim and the driver partial name the feel rules.
        string root = ProjectSettings.GlobalizePath("res://");
        var offenders = new List<string>();
        string[] needles = {
            "FighterKnockbackFeelRules", "FighterFinisherHorizontalScale", "FighterWallBounce"
        };
        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "scripts"), "*.cs", SearchOption.AllDirectories)) {
            string rel = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            if (rel.StartsWith("scripts/FighterSim/")) continue;
            if (rel == "scripts/Combat/BasicComboRules.cs") continue;
            string text = File.ReadAllText(file);
            foreach (string needle in needles) {
                if (text.Contains(needle)) offenders.Add($"{rel} names {needle}");
            }
        }
        if (offenders.Count > 0) AssertThat(string.Join(" | ", offenders)).IsEqual("");
    }
}
