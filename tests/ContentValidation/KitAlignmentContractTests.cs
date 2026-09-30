using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 12 W4 — the kit-alignment contracts that are about DATA and SOURCE
/// rather than behaviour:
/// <list type="bullet">
/// <item>the frame counts the Fighter sim reads from <see cref="KitMotionRules"/>
/// equal the ones Story reads from the <c>.tres</c> (one value, pinned, never
/// two drifting copies);</item>
/// <item>every kit script that builds its own <see cref="HitPayload"/> stamps the
/// authored M08 contract through the one helper and credits off the stamped
/// payload — no literal <c>collectsEcho</c>/<c>ultimateOrigin</c> left.</item>
/// </list>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class KitAlignmentContractTests {

    [TestCase]
    public void TheBlinkAndTubmanResourcesEqualTheSharedKitRulebooks() {
        var blink = AuthoredResources.Load<MovementAbilityData>("res://resources/Abilities/tesla/movement.tres");
        AssertThat(blink.StartupFrames).IsEqual(KitMotionRules.LightningBlinkStartupFrames);
        AssertThat(blink.ActiveFrames).IsEqual(KitMotionRules.LightningBlinkTravelFrames);
        AssertThat(blink.RecoveryFrames).IsEqual(KitMotionRules.LightningBlinkRecoveryFrames);
        AssertThat(Mathf.RoundToInt(blink.MovementDuration * 60f)).IsEqual(KitMotionRules.LightningBlinkTravelFrames);
        AssertFloat(blink.DistanceMoved).IsEqualApprox(
            KitMotionRules.LightningBlinkDistanceUnits * KitMotionRules.StoryPixelsPerUnit, 0.001f);
        AssertThat(KitMotionRules.LightningBlinkTotalFrames <= KitMotionRules.LightningBlinkActionCapFrames).IsTrue();

        // Package 13 W5 (replaces the retired Spirit Strike pin): the sim reads
        // TubmanKitRules for North Star Leap's travel and Foresight's stance; the
        // Story phase timers read the resources. They must agree.
        var leap = AuthoredResources.Load<MovementAbilityData>("res://resources/Abilities/tubman/movement.tres");
        AssertThat(leap.ActiveFrames).IsEqual(TubmanKitRules.NorthStarLeapTravelFrames);
        AssertThat(Mathf.RoundToInt(leap.MovementDuration * 60f)).IsEqual(TubmanKitRules.NorthStarLeapTravelFrames);
        AssertFloat(leap.DistanceMoved).IsEqualApprox(
            TubmanKitRules.NorthStarLeapDistanceUnits * KitMotionRules.StoryPixelsPerUnit, 0.001f);
        var foresight = AuthoredResources.Load<AbilityData>("res://resources/Abilities/tubman/special_2.tres");
        AssertThat(foresight.StartupFrames).IsEqual(TubmanKitRules.ForesightStartupFrames);
        AssertThat(foresight.ActiveFrames).IsEqual(TubmanKitRules.ForesightWindowFrames);
        AssertThat(foresight.RecoveryFrames).IsEqual(TubmanKitRules.ForesightWhiffRecoveryFrames);
        AssertFloat(foresight.CooldownDuration).IsEqual(10f);
    }

    [TestCase]
    public void EveryKitBuiltPayloadIsStampedAndCreditedOffThePayload() {
        string root = ProjectSettings.GlobalizePath("res://scripts/Characters/Abilities");
        var issues = new List<string>();
        var bareConstruction = new Regex(@"new HitPayload\s*\{");
        foreach (string path in Directory.GetFiles(root, "*.cs")) {
            string[] lines = File.ReadAllLines(path);
            for (int index = 0; index < lines.Length; index++) {
                string line = lines[index];
                string file = Path.GetFileName(path);
                if (bareConstruction.IsMatch(line)
                    && !line.Contains("Stamp(") && !line.Contains("WithAbilityContract(")
                    && !line.Contains("StampConstruct(")) {
                    issues.Add($"{file}:{index + 1} builds a HitPayload without stamping the M08 contract");
                }
                if (line.Contains("AddInfluenceFromDamageDealt(")) {
                    issues.Add($"{file}:{index + 1} credits with literal flags instead of the stamped payload");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void AStampedPayloadCarriesTheAuthoredContractAndAFreshContactId() {
        PlayerController player = CharacterFactory.CreateCharacter("tesla");
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        try {
            AbilityData pulse = player.GetNode<BaseSpecial>("Special2").Data;
            HitPayload first = BaseSpecial.WithAbilityContract(new HitPayload { Damage = 1f }, pulse, player);
            HitPayload second = BaseSpecial.WithAbilityContract(new HitPayload { Damage = 1f }, pulse, player);
            AssertThat(first.Origin).IsEqual(pulse.Origin);
            AssertThat(first.Delivery).IsEqual(pulse.Delivery);
            AssertThat(first.Launches).IsEqual(pulse.Launches);
            AssertThat(first.SourceActorId).IsEqual(player.GetInstanceId());
            AssertThat(first.ContactId == 0UL).IsFalse();
            AssertThat(second.ContactId == first.ContactId).IsFalse();

            // A construct arc overrides the delivery, which is what keeps its
            // Rally reclaim at zero (D03g).
            HitPayload arc = BaseSpecial.WithAbilityContract(
                new HitPayload { Damage = 1f }, pulse, player, HitDelivery.Construct);
            AssertThat(arc.Delivery).IsEqual(HitDelivery.Construct);
            AssertThat(HitClassification.CollectsEcho(arc.Delivery)).IsFalse();
        } finally {
            player.Free();
        }
    }
}
