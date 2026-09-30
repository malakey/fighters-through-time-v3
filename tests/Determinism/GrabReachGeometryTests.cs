using System;
using System.Collections.Generic;
using FTT.Combat;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 13 W1 — A13, the COMBAT_VALIDATION "Grab reach" row. The pushbox is
/// 0.6 units for every combatant, and the grab box runs from the grabber's
/// pivot to 0.8 forward, tested against the target's hurtbox (0.8 template,
/// Lincoln 0.9, Cleopatra 0.7) — so a grab connects at pushbox contact for
/// every character pair, and out to reach + half the target hurtbox (1.2
/// against the template). Swept over all 81 ordered pairs.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GrabReachGeometryTests {

    [TestCase]
    public void TheSharedGeometryConstantsMatchTheDesign() {
        AssertThat(BasicComboRules.PushboxWidthUnits).IsEqual(0.6f);
        AssertThat(BasicComboRules.GrabReachUnits).IsEqual(0.8f);
        AssertThat(BasicComboRules.GrabMaxPivotDistanceUnits("einstein")).IsEqual(1.2f);
        AssertThat(FighterPushboxSystem.MinimumHorizontalDistance.RawValue)
            .IsEqual(FP64.FromDouble(BasicComboRules.PushboxWidthUnits).RawValue);
    }

    [TestCase]
    public void AGrabConnectsAtPushboxContactAndToTheHurtboxEdgeForEveryPair() {
        var issues = new List<string>();
        FP64 contact = FighterPushboxSystem.MinimumHorizontalDistance;
        FP64 epsilon = FP64.FromDouble(0.01);
        foreach (FighterCharacterID grabberID in Enum.GetValues<FighterCharacterID>()) {
            foreach (FighterCharacterID targetID in Enum.GetValues<FighterCharacterID>()) {
                string targetKey = targetID.ToString().ToLowerInvariant();
                FP64 maximum = FP64.FromDouble(BasicComboRules.GrabMaxPivotDistanceUnits(targetKey));
                if (!Seizes(grabberID, targetID, contact)) issues.Add($"{grabberID}->{targetID} misses at contact");
                if (!Seizes(grabberID, targetID, maximum - epsilon)) issues.Add($"{grabberID}->{targetID} misses inside max");
                if (Seizes(grabberID, targetID, maximum + epsilon)) issues.Add($"{grabberID}->{targetID} reaches past max");
                // The Story predicate agrees on the same numbers.
                float width = BasicComboRules.HurtboxWidthUnitsFor(targetKey);
                if (!BasicComboRules.GrabBoxReaches(BasicComboRules.PushboxWidthUnits, width)) {
                    issues.Add($"{targetKey} Story predicate misses at contact");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    private static bool Seizes(FighterCharacterID grabberID, FighterCharacterID targetID, FP64 distance) {
        FighterStateComponent grabber = new() {
            PlayerID = 0, CharacterID = (int)grabberID, Stocks = 3, IsGrounded = 1, FacingRight = 1
        };
        FighterStateComponent target = new() {
            PlayerID = 1, CharacterID = (int)targetID, Stocks = 3, IsGrounded = 1,
            Position = new FPVector2(distance, FP64.Zero)
        };
        FighterRuntimeComponent targetRuntime = default;
        FighterVerbComponent targetVerb = default;
        return FighterGrabRules.ActiveWindowSeizes(in grabber, in target, in targetRuntime, in targetVerb);
    }
}
