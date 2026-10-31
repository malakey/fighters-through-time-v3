using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// The Fighter sim's D02 defensive layer after the passive Defy was deleted
/// (meter and Defy update 2026-10-31): the granted shield's lifetime and what a
/// stock loss clears. These two cases lived in the retired passive-Defy suite
/// and moved here unchanged in intent.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterDefenseLayerTests {

    [TestCase]
    public void AKnockoutClearsTheBubbleAndTheBarrierButLeavesTheHitConfirmWord() {
        // "Death/stock loss, Death Rewind cleanup, scene reconstruction and F22
        // Sudden Death clear" the temporary defensive state.
        var fighter = new FighterStateComponent {
            PlayerID = 1, Stocks = 3, MaxHP = 100, CurrentHP = 0,
            BlockCharges = 0, Weight = FP64.One
        };
        // The retired AegisHits slot now holds the D1 hit-confirm word
        // (2026-10-11): a stock loss leaves S and C to expire on the respawn
        // platform, so they are seeded here and must come through untouched.
        var runtime = new FighterRuntimeComponent { UsesStocks = 1, SpecialLockoutFrames = 12, SwingLanded = 1 };
        var verb = new FighterVerbComponent { MomentumUnits = 20000 };
        var defense = new FighterDefenseComponent {
            AegisActive = 1, BarrierPoints = 10, BarrierCapacity = 10, BarrierRemainingFrames = 200
        };
        var tuning = new FighterTuningComponent { MaxBlockCharges = 3, MaxJumpCount = 2 };

        FighterSimulationRules.ApplyStockLoss(ref fighter, ref runtime, ref verb, ref defense, in tuning);

        AssertThat(defense.AegisActive).IsEqual(0);
        AssertThat(defense.BarrierPoints).IsEqual(0);
        AssertThat(defense.BarrierRemainingFrames).IsEqual(0);
        AssertThat(runtime.SpecialLockoutFrames)
            .OverrideFailureMessage("The stock loss must leave the new tenant of the AegisHits slot alone.")
            .IsEqual(12);
        AssertThat(runtime.SwingLanded).IsEqual(1);
        AssertThat(verb.MomentumUnits)
            .OverrideFailureMessage("A stock loss keeps the Momentum gauge (G3.2).")
            .IsEqual(20000);
    }

    [TestCase]
    public void TheGrantedShieldExpiresAtExactlyFourHundredAndEightyActiveTicks() {
        // D02c, sim mirror: eight seconds of LIVE gameplay, and a suspended tick
        // consumes no lifetime.
        var defense = new FighterDefenseComponent {
            BarrierPoints = 10,
            BarrierCapacity = 10,
            BarrierEffectID = 1,
            BarrierRemainingFrames = FighterDefenseRules.GrantedShieldLifetimeTicks
        };

        for (int tick = 0; tick < 100; tick++) {
            FighterDefenseRules.TickBarrier(ref defense, suspended: true);
        }
        AssertThat(defense.BarrierRemainingFrames)
            .OverrideFailureMessage("A frozen world consumes no shield lifetime.")
            .IsEqual(FighterDefenseRules.GrantedShieldLifetimeTicks);

        for (int tick = 0; tick < FighterDefenseRules.GrantedShieldLifetimeTicks - 1; tick++) {
            FighterDefenseRules.TickBarrier(ref defense, suspended: false);
        }
        AssertThat(defense.BarrierPoints)
            .OverrideFailureMessage("At tick 479 the shield is still live.")
            .IsEqual(10);

        FighterDefenseRules.TickBarrier(ref defense, suspended: false);
        AssertThat(defense.BarrierPoints)
            .OverrideFailureMessage("Tick 480 expires it, discarding the remaining absorption.")
            .IsEqual(0);
        AssertThat(defense.BarrierEffectID).IsEqual(0);
    }
}
