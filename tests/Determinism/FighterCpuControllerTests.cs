using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

[TestSuite]
public class FighterCpuControllerTests {
    [TestCase]
    public void SameSeedAndStateProduceIdenticalCpuInputFrames() {
        var first = new FighterCpuController(CpuDifficulty.Hard, 1234);
        var second = new FighterCpuController(CpuDifficulty.Hard, 1234);
        FighterStateComponent self = Fighter(1, 4);
        FighterStateComponent target = Fighter(0, -4);
        FighterRuntimeComponent selfRuntime = default;
        FighterRuntimeComponent targetRuntime = default;
        PlayerInputFrame previousFirst = default;
        PlayerInputFrame previousSecond = default;

        for (uint tick = 0; tick < 300; tick++) {
            PlayerInputFrame firstFrame = first.Sample(tick, in self, in selfRuntime, in target, in targetRuntime, in previousFirst);
            PlayerInputFrame secondFrame = second.Sample(tick, in self, in selfRuntime, in target, in targetRuntime, in previousSecond);
            AssertThat(firstFrame.Equals(secondFrame)).IsTrue();
            previousFirst = firstFrame;
            previousSecond = secondFrame;
        }
    }

    [TestCase]
    public void DifficultyReactionWindowsMatchDesign() {
        var easy = new FighterCpuController(CpuDifficulty.Easy, 1);
        var medium = new FighterCpuController(CpuDifficulty.Normal, 1);
        var hard = new FighterCpuController(CpuDifficulty.Hard, 1);
        AssertThat(easy.GetReactionDelayBounds(out int easyMax)).IsEqual(30);
        AssertThat(easyMax).IsEqual(45);
        AssertThat(medium.GetReactionDelayBounds(out int mediumMax)).IsEqual(15);
        AssertThat(mediumMax).IsEqual(20);
        AssertThat(hard.GetReactionDelayBounds(out int hardMax)).IsEqual(4);
        AssertThat(hardMax).IsEqual(8);
    }

    private static FighterStateComponent Fighter(int playerID, int x) => new() {
        PlayerID = playerID,
        Stocks = 3,
        CurrentHP = 100,
        MaxHP = 100,
        IsGrounded = 1,
        Position = new FPVector2(FP64.FromInt(x), FP64.Zero)
    };
}
