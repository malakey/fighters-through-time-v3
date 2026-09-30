using System;
using System.Buffers.Binary;
using FTT.Core;
using FTT.FighterSim;
using FTT.Networking;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 13 W5 (plan D3) — the roster swap's protocol-affecting half.
/// <c>FighterCharacterID</c> is append-only because its ordinals serialize into
/// snapshots and the network protocol: Tubman takes 9, Pocahontas's 8 is
/// reserved and resolves from no character ID, the retired kit's persistent
/// type 4 and zone type 83 are never reused, and the protocol moves v3 → v4 so
/// a v3 peer's packet is refused.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RosterSwapProtocolTests {

    [TestCase]
    public void TubmanIsAppendedAtNineAndPocahontasEightIsReserved() {
        AssertThat((int)FighterCharacterID.Tubman).IsEqual(9);
        AssertThat(FighterLoadoutFactory.TryGetCharacterID("tubman", out FighterCharacterID tubman)).IsTrue();
        AssertThat(tubman).IsEqual(FighterCharacterID.Tubman);
        AssertThat(FighterLoadoutFactory.TryGetCharacterID("pocahontas", out _))
            .OverrideFailureMessage("The retired roster ID must not build a fighter.")
            .IsFalse();
        // Retired sim identifiers are never reused.
        AssertThat(FighterLoadoutFactory.PersistentObjectTypeID("vine_snare")).IsEqual(0);
        AssertThat(FighterConductorsCallRules.ProjectileTypeID).IsEqual(91);
        AssertThat((int)FighterCharacterID.Tubman * 10 + FighterUltimateRules.UltimateSlot).IsEqual(93);
        AssertThat(FighterKitMotion.LeapTravel > 20 && FighterKitMotion.LeapEnd > 20)
            .OverrideFailureMessage("Kit phase codes 19 and 20 (Spirit Strike) are retired.")
            .IsTrue();
    }

    [TestCase]
    public void TheProtocolIsVersionFourAndRefusesAVersionThreePacket() {
        AssertThat(RollbackInputPacket.ProtocolVersion).IsEqual((ushort)4);
        var packet = new RollbackInputPacket(7, 1, 0, 1, 42, default, 40, 123456789L);
        byte[] bytes = packet.Serialize();
        AssertThat(bytes.Length).IsEqual(RollbackInputPacket.SerializedSize);
        AssertThat(RollbackInputPacket.TryDeserialize(bytes, out RollbackInputPacket roundTrip)).IsTrue();
        AssertThat(roundTrip.Tick).IsEqual(42);

        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 3);
        AssertThat(RollbackInputPacket.TryDeserialize(bytes, out _))
            .OverrideFailureMessage("A v3 peer (the Pocahontas-era roster) must be refused.")
            .IsFalse();
    }
}
