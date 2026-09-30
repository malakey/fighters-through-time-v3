using FTT.Characters;
using FTT.Characters.Abilities;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 13 W7b — the Story halves of the four reworked kits' rules that do
/// not need a level: Desert Mirage's 8-way rush direction (C03), Sonata
/// Drift's rising glissando direction (M04), the Requiem Chord's
/// once-per-execution Fortissimo shave (M02), and Joan's Wing-Dive and
/// Divine Piercing lunge geometry (A08/J01). Each reads the same
/// <c>KitReachRules</c> value the Fighter sim reads.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryReachKitTests {

    [TestCase]
    public void TheMirageRushIsEightWayAndNormalized() {
        Vector2 diagonal = CleopatraDesertMirage.ResolveRushDirection(1f, -1f, facingRight: true);
        AssertFloat(diagonal.X).IsEqualApprox(0.7071f, 0.001f);
        AssertFloat(diagonal.Y).IsEqualApprox(-0.7071f, 0.001f);
        // Down is a legal rush direction (it is a rush, not a rise).
        AssertThat(CleopatraDesertMirage.ResolveRushDirection(0f, 1f, facingRight: true)).IsEqual(Vector2.Down);
        // Neutral rushes along facing; a shallow stick snaps to its axis.
        AssertThat(CleopatraDesertMirage.ResolveRushDirection(0f, 0f, facingRight: false)).IsEqual(Vector2.Left);
        AssertThat(CleopatraDesertMirage.ResolveRushDirection(0.9f, 0.1f, facingRight: false)).IsEqual(Vector2.Right);
    }

    [TestCase]
    public void TheGlissandoRisesByDefaultAndNeverDescends() {
        AssertThat(MozartSonataDrift.ResolveGlissandoDirection(0f, 0f)).IsEqual(Vector2.Up);
        // A held Down is dropped: down-right drifts sideways, never down.
        AssertThat(MozartSonataDrift.ResolveGlissandoDirection(1f, 1f)).IsEqual(Vector2.Right);
        AssertThat(MozartSonataDrift.ResolveGlissandoDirection(0f, 1f)).IsEqual(Vector2.Up);
        Vector2 upLeft = MozartSonataDrift.ResolveGlissandoDirection(-1f, -1f);
        AssertFloat(upLeft.X).IsEqualApprox(-0.7071f, 0.001f);
        AssertFloat(upLeft.Y).IsEqualApprox(-0.7071f, 0.001f);
    }

    [TestCase]
    public void TheRequiemChordShavesFortissimoOncePerExecution() {
        PlayerController player = CharacterFactory.CreateCharacter("mozart");
        Node host = Attach(player);
        try {
            var chord = player.GetNode<MozartRequiemChord>("Special1");
            player.SpecialTwoCooldownTimer = 5f;
            chord.ShaveFortissimo();
            AssertFloat(player.SpecialTwoCooldownTimer).IsEqualApprox(3f, 0.0001f);
            AssertThat(chord.ExecutionShavedFortissimo).IsTrue();
            // The contact and every pulse of one execution share the one shave.
            chord.ShaveFortissimo();
            AssertFloat(player.SpecialTwoCooldownTimer).IsEqualApprox(3f, 0.0001f);
            // M03: the burst radius is 1.2 units.
            AssertFloat(MozartRequiemChord.BurstRadiusPixels).IsEqualApprox(72f, 0.001f);
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void TheWingDiveIsSteepAndForwardAndThePiercingLungeCoversThreeUnits() {
        Vector2 right = JoanAscendantWings.WingDiveVelocity(facingRight: true);
        Vector2 left = JoanAscendantWings.WingDiveVelocity(facingRight: false);
        AssertFloat(right.X).IsEqualApprox(300f, 0.001f);
        AssertFloat(right.Y).IsEqualApprox(540f, 0.001f);
        AssertFloat(left.X).IsEqualApprox(-300f, 0.001f);
        // Steeper than 45 degrees below horizontal.
        AssertThat(right.Y > right.X).IsTrue();

        PlayerController player = CharacterFactory.CreateCharacter("joan");
        Node host = Attach(player);
        try {
            var piercing = player.GetNode<JoanDivinePiercing>("Special2");
            // 3 units (180 px) across the authored 12 active frames.
            float travelled = piercing.LungeSpeedPixelsPerSecond * piercing.Data.ActiveFrames / 60f;
            AssertFloat(travelled).IsEqualApprox(180f, 0.01f);
        } finally {
            Teardown(host);
        }
    }

    private static Node Attach(PlayerController player) {
        var host = new Node { Name = "StoryReachKitHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        host.AddChild(player);
        return host;
    }

    private static void Teardown(Node host) {
        if (!GodotObject.IsInstanceValid(host)) return;
        host.GetParent()?.RemoveChild(host);
        host.Free();
    }
}
