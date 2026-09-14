using FTT.Combat;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A1c. The V7.6 same-frame chord priority in attack recovery
/// (design master 1451-1452), resolved by the one shared table both modes read:
/// <see cref="BasicComboRules.SelectRecoveryVerb"/>.
///
/// <para><b>Why a shared table at all.</b> Before this the ordering was incidental
/// rather than authored: <c>FighterMovementSystem</c> happened to call
/// <c>TryStartEchoStep</c> eight lines before it evaluated the roll cancel, so Echo
/// Step beat Roll by accident, while the grab chord was resolved in a different
/// system entirely and Story called all three from three separate sites. Any
/// reordering of those calls would have silently changed the game.</para>
///
/// <para>The rule in one line: <b>the chord beats the single input, and the priced
/// verb beats the free one.</b></para>
/// </summary>
[TestSuite]
public class RecoveryChordPriorityTests {

    /// <summary>
    /// Block + Roll on the same frame is Echo Step, not the free block-cancel. Both
    /// edges count: pressing Roll onto a held Block, and pressing Block onto a held
    /// Roll, are the same chord — the shipped behaviour, preserved.
    /// </summary>
    [TestCase]
    public void BlockPlusRollIsEchoStepOnEitherEdge() {
        AssertThat(Verb(blockHeld: true, blockPressed: false, rollHeld: true, rollPressed: true))
            .IsEqual(BasicComboRules.RecoveryVerb.EchoStep);
        AssertThat(Verb(blockHeld: true, blockPressed: true, rollHeld: true, rollPressed: false))
            .IsEqual(BasicComboRules.RecoveryVerb.EchoStep);

        // A chord the player is merely leaning on is not a fresh request; it must
        // not re-fire Echo Step every frame of the recovery window.
        AssertThat(Verb(blockHeld: true, blockPressed: false, rollHeld: true, rollPressed: false))
            .IsEqual(BasicComboRules.RecoveryVerb.BlockCancel);
    }

    /// <summary>Block alone is the block-cancel — the free stance, unchanged.</summary>
    [TestCase]
    public void BlockAloneIsTheBlockCancel() {
        AssertThat(Verb(blockHeld: true, blockPressed: true, rollHeld: false, rollPressed: false))
            .IsEqual(BasicComboRules.RecoveryVerb.BlockCancel);
        AssertThat(Verb(blockHeld: true, blockPressed: false, rollHeld: false, rollPressed: false))
            .IsEqual(BasicComboRules.RecoveryVerb.BlockCancel);
        // No Block at all: the swing simply runs its recovery out.
        AssertThat(Verb(blockHeld: false, blockPressed: false, rollHeld: true, rollPressed: true))
            .IsEqual(BasicComboRules.RecoveryVerb.None);
    }

    /// <summary>
    /// Block + BasicAttack is a grab attempt where grabs are legal, and the
    /// block-cancel where they are not. Both modes refuse a grab mid-swing today —
    /// the sim's <c>CanStartGrab</c> requires no attack phase — so recovery frames
    /// take the second branch, which is exactly why the fallback has to exist
    /// rather than the input being eaten.
    /// </summary>
    [TestCase]
    public void BlockPlusAttackIsAGrabWhereGrabsAreLegalAndTheCancelWhereTheyAreNot() {
        AssertThat(Verb(blockHeld: true, blockPressed: false, rollHeld: false, rollPressed: false,
                basicPressed: true, grabLegal: true))
            .IsEqual(BasicComboRules.RecoveryVerb.Grab);
        AssertThat(Verb(blockHeld: true, blockPressed: false, rollHeld: false, rollPressed: false,
                basicPressed: true, grabLegal: false))
            .IsEqual(BasicComboRules.RecoveryVerb.BlockCancel);
    }

    /// <summary>
    /// The priced verb only wins when it can actually be paid for. An Echo Step
    /// refused for meter, cooldown, missing history or a blocked destination falls
    /// back to the block-cancel rather than swallowing the input — the player
    /// pressed Block and must at least get the stance.
    ///
    /// <para>And priority is strict: with Roll and BasicAttack pressed on the same
    /// frame, a legal Echo Step outranks the grab.</para>
    /// </summary>
    [TestCase]
    public void AnIllegalPricedVerbFallsBackAndEchoStepOutranksTheGrab() {
        AssertThat(Verb(blockHeld: true, blockPressed: false, rollHeld: true, rollPressed: true,
                echoLegal: false))
            .OverrideFailureMessage("A refused Echo Step must not eat the Block input.")
            .IsEqual(BasicComboRules.RecoveryVerb.BlockCancel);

        AssertThat(Verb(blockHeld: true, blockPressed: false, rollHeld: true, rollPressed: true,
                basicPressed: true, grabLegal: true, echoLegal: true))
            .OverrideFailureMessage("Echo Step must outrank the grab on a shared frame.")
            .IsEqual(BasicComboRules.RecoveryVerb.EchoStep);

        // ...and when Echo Step is unavailable, the grab chord still loses to the
        // Roll chord's fallback: one verb per frame, decided by one table.
        AssertThat(Verb(blockHeld: true, blockPressed: false, rollHeld: true, rollPressed: true,
                basicPressed: true, grabLegal: true, echoLegal: false))
            .IsEqual(BasicComboRules.RecoveryVerb.BlockCancel);
    }

    private static BasicComboRules.RecoveryVerb Verb(
        bool blockHeld,
        bool blockPressed,
        bool rollHeld,
        bool rollPressed,
        bool basicPressed = false,
        bool grabLegal = false,
        bool echoLegal = true) =>
        BasicComboRules.SelectRecoveryVerb(
            blockHeld, blockPressed, rollHeld, rollPressed, basicPressed, grabLegal, echoLegal);
}
