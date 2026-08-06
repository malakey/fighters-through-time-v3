using FTT.Core;
using FTT.FighterSim;
using FTT.Networking;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

[TestSuite]
public class RollbackProtocolTests {
    [TestCase]
    public void InputPacketRoundTripsExactQuantizedData() {
        PlayerInputFrame input = PlayerInputFrame.Create(
            44, -0.75f, 0.5f,
            GameplayButtons.Jump | GameplayButtons.Special1,
            GameplayButtons.Jump);
        var source = new RollbackInputPacket(77, 12, 10, 1, 44, input, 36, 99887766L);
        byte[] bytes = source.Serialize();

        AssertThat(bytes.Length).IsEqual(RollbackInputPacket.SerializedSize);
        AssertThat(RollbackInputPacket.TryDeserialize(bytes, out RollbackInputPacket decoded)).IsTrue();
        AssertThat(decoded.SessionID).IsEqual(77u);
        AssertThat(decoded.Sequence).IsEqual(12u);
        AssertThat(decoded.AcknowledgedSequence).IsEqual(10u);
        AssertThat(decoded.PlayerID).IsEqual((byte)1);
        AssertThat(decoded.Tick).IsEqual(44);
        AssertThat(decoded.Input.Equals(input)).IsTrue();
        AssertThat(decoded.HashTick).IsEqual(36);
        AssertThat(decoded.StateHash).IsEqual(99887766L);
    }

    [TestCase]
    public void DelayedPeerInputsRollbackAndConvergeWithinSevenFrames() {
        (InMemoryRollbackTransport firstTransport, InMemoryRollbackTransport secondTransport) =
            InMemoryRollbackTransport.CreatePair(latencyPolls: 3);
        var firstSimulation = new FighterSimulation(seed: 781);
        var secondSimulation = new FighterSimulation(seed: 781);
        using var first = new OnlineRollbackSession(firstSimulation, firstTransport, 9001, 0);
        using var second = new OnlineRollbackSession(secondSimulation, secondTransport, 9001, 1);
        bool lateInput = false;
        first.InputArrivedTooLate += _ => lateInput = true;
        second.InputArrivedTooLate += _ => lateInput = true;

        for (int tick = 0; tick < 300; tick++) {
            first.Advance(Input(tick, 0));
            second.Advance(Input(tick, 1));
        }
        for (int flush = 0; flush < 6; flush++) {
            first.PumpNetwork();
            second.PumpNetwork();
        }

        AssertThat(lateInput).IsFalse();
        AssertThat(firstSimulation.CurrentTick).IsEqual(secondSimulation.CurrentTick);
        AssertThat(firstSimulation.CurrentHash).IsEqual(secondSimulation.CurrentHash);
    }

    private static PlayerInputFrame Input(int tick, int playerID) {
        sbyte direction = ((tick / 45 + playerID) & 1) == 0 ? (sbyte)100 : (sbyte)-100;
        GameplayButtons held = tick % 29 == playerID
            ? GameplayButtons.BasicAttack
            : tick % 113 == 20 + playerID
                ? GameplayButtons.Special1
                : GameplayButtons.None;
        return new PlayerInputFrame {
            Tick = (uint)tick,
            MoveX = direction,
            Held = held,
            Pressed = held
        };
    }
}
