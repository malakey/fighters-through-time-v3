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

    /// <summary>
    /// Audit M-33 companion: the deserialize boundary is the last line against a
    /// mis-sized datagram on any platform, so a truncated buffer, an oversized
    /// buffer (even one carrying a perfectly valid 47-byte prefix — the POSIX
    /// truncation smuggling case), and an empty buffer must all be rejected.
    /// </summary>
    [TestCase]
    public void MisSizedPacketsAreRejectedAtTheDeserializeBoundary() {
        PlayerInputFrame input = PlayerInputFrame.Create(
            10, 0.25f, 0f, GameplayButtons.BasicAttack, GameplayButtons.None);
        byte[] valid = new RollbackInputPacket(5, 1, 0, 0, 10, input, -1, 0L).Serialize();
        AssertThat(RollbackInputPacket.TryDeserialize(valid, out _)).IsTrue();

        byte[] truncated = new byte[RollbackInputPacket.SerializedSize - 1];
        System.Array.Copy(valid, truncated, truncated.Length);
        AssertThat(RollbackInputPacket.TryDeserialize(truncated, out _)).IsFalse();

        byte[] oversizedWithValidPrefix = new byte[RollbackInputPacket.SerializedSize + 1];
        System.Array.Copy(valid, oversizedWithValidPrefix, valid.Length);
        AssertThat(RollbackInputPacket.TryDeserialize(oversizedWithValidPrefix, out _)).IsFalse();

        AssertThat(RollbackInputPacket.TryDeserialize(System.Array.Empty<byte>(), out _)).IsFalse();
    }

    /// <summary>
    /// Audit §4 Networking: <c>_remoteInputs</c> accepted arbitrarily-future
    /// ticks, the one remaining unbounded structure. A tick within one full
    /// input-history window of the local clock is stored; anything beyond it is
    /// dropped, so a hostile peer cannot grow the pending map without limit.
    /// </summary>
    [TestCase]
    public void FutureInputsBeyondTheHistoryWindowAreDropped() {
        (InMemoryRollbackTransport sessionTransport, InMemoryRollbackTransport peerTransport) =
            InMemoryRollbackTransport.CreatePair();
        var simulation = new FighterSimulation(seed: 907);
        using var session = new OnlineRollbackSession(simulation, sessionTransport, 8801, 0);
        try {
            AssertThat(session.PendingRemoteInputCount).IsEqual(0);

            // Exactly on the window boundary: stored.
            int boundaryTick = simulation.CurrentTick + FighterSimulation.RollbackHistoryTicks;
            peerTransport.Send(RemotePacket(8801, sequence: 1, tick: boundaryTick));
            session.PumpNetwork();
            AssertThat(session.PendingRemoteInputCount).IsEqual(1);

            // One past the boundary, and absurdly far ahead: both dropped.
            peerTransport.Send(RemotePacket(8801, sequence: 2, tick: boundaryTick + 1));
            peerTransport.Send(RemotePacket(8801, sequence: 3, tick: int.MaxValue - 1));
            session.PumpNetwork();
            AssertThat(session.PendingRemoteInputCount).IsEqual(1);
        } finally {
            peerTransport.Dispose();
        }
    }

    private static byte[] RemotePacket(uint sessionID, uint sequence, int tick) {
        var input = new PlayerInputFrame { Tick = (uint)System.Math.Max(0, tick) };
        // PlayerID 1 is the remote slot for a session whose local player is 0;
        // HashTick -1 / hash 0 skip the confirmed-hash comparison.
        return new RollbackInputPacket(sessionID, sequence, 0, 1, tick, input, -1, 0L).Serialize();
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
