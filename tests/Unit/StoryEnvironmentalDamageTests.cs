using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Pins the V7.3 Story environmental-damage chokepoint
/// (<c>PlayerController.ApplyEnvironmentalDamage</c>) — the Story mirror of the
/// sim's <c>FighterDamageRules.ApplyUnattributedDamage</c>. Environmental
/// sources (extractor discharges, drown/searchlight/rift ticks, escape
/// catches, stage hazards) route through it instead of raw <c>ApplyDamage</c>,
/// so the victim-side pipeline runs: Rally echo accrual, victim meter on the
/// permanent portion, and Defy History flag consumption (the flag leak that
/// used to poison the NEXT hurtbox hit's accounting). Also pins the
/// <c>StageHazard</c> per-body accumulator's frame-rate independence.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryEnvironmentalDamageTests {

    [TestCase]
    public void EnvironmentalDamageAccruesRallyEchoAndMeter() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        try {
            int applied = player.ApplyEnvironmentalDamage(20);
            AssertThat(applied).IsEqual(20);

            float expectedFraction = (BasicComboRules.EchoFractionBase
                    + BasicComboRules.EchoFractionSlope * (20f / player.MaximumHP))
                * StoryDifficultyTuning.GetRallyEchoMultiplier(
                    StoryDifficultyTuning.CurrentStoryDifficulty);
            float expectedEcho = 20f * expectedFraction;
            AssertThat(player.EchoPool)
                .OverrideFailureMessage("An environmental hit must stash its Rally echo like any hit.")
                .IsEqualApprox(expectedEcho, 0.05f);
            AssertThat(player.CurrentUltimateMeter)
                .OverrideFailureMessage("The victim meter must earn the permanent (non-echo) portion at the Story rate.")
                .IsEqualApprox((20f - expectedEcho) * UltimateMeter.PointsPerDamageTaken * player.StoryMeterGainFactor, 0.02f);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void CyclicHazardsAndSpikesRouteThroughTheEnvironmentalChokepoint() {
        // V7.3 follow-up: the last two direct environmental callers
        // (StoryCyclicHazard.ApplyToPlayer and the base level builder's
        // hazard spikes) migrated to the chokepoint — a hit from either must
        // accrue Rally echo and victim meter like every environmental source.
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        var hazard = new StoryCyclicHazard { Name = "ChokepointCyclicHazard", Damage = 12 };
        tree.Root.AddChild(hazard);
        try {
            hazard.ForcePhase(HazardPhase.Active, 5f);
            AssertThat(hazard.ApplyToPlayer(player)).IsTrue();
            AssertThat(player.EchoPool > 0f)
                .OverrideFailureMessage("A cyclic hazard hit must stash its Rally echo (chokepoint path).")
                .IsTrue();
            AssertThat(player.CurrentUltimateMeter > 0f)
                .OverrideFailureMessage("A cyclic hazard hit must accrue victim meter (chokepoint path).")
                .IsTrue();

            float echoAfterHazard = player.EchoPool;
            AssertThat(FTT.Environment.StoryLevelControllerBase.ApplySpikeDamage(player, 15)).IsEqual(15);
            AssertThat(player.EchoPool > echoAfterHazard)
                .OverrideFailureMessage("A spike hit must stash its Rally echo (chokepoint path).")
                .IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            hazard.Free();
        }
    }

    [TestCase]
    public void StageHazardDamageIsFrameRateIndependent() {
        // The old (int)(Damage * dt) dealt ZERO at 60 FPS (15/60 truncates)
        // and different totals at different frame rates. The V7.3 per-body
        // accumulator banks fractions: the same window at any tick rate pays
        // the same whole-HP total.
        AssertThat((int)(15f * (1f / 60f)))
            .OverrideFailureMessage("Precondition: the old truncation dealt nothing per 60 FPS frame.")
            .IsEqual(0);

        int TotalOver(float seconds, int steps) {
            float owed = 0f;
            float dt = seconds / steps;
            int total = 0;
            for (int step = 0; step < steps; step++) {
                total += StageHazard.AccumulateDamage(ref owed, 15f, dt);
            }
            return total;
        }

        int at60 = TotalOver(3f, 180);
        int at30 = TotalOver(3f, 90);
        int at144 = TotalOver(3f, 432);
        AssertThat(at60)
            .OverrideFailureMessage("Three active seconds at 15 damage/s must deal 45 HP at 60 FPS.")
            .IsEqual(45);
        AssertThat(at30)
            .OverrideFailureMessage("The same window at 30 FPS must deal the same total.")
            .IsEqual(45);
        AssertThat(at144)
            .OverrideFailureMessage("The same window at 144 FPS must deal the same total.")
            .IsEqual(45);
    }

    // ---- CM-BLOCK (decision 7.4): a raised block absorbs Story hazard ticks ----
    // PlayerController.ApplyEnvironmentalHit, in the D01/D02b order: the D04
    // Defy window, then Aegis and the barrier, then a raised front-facing block
    // as a Hazard-class hit (one charge, scaled shieldstun, no HP, echo or
    // meter), then HP. Every block below is ORDINARY (raised without a press,
    // so the perfect-block window never opens).

    [TestCase]
    public void AFrontFacingBlockAbsorbsACyclicHazardTickForOneChargeWithNoHPKnockbackEchoOrMeter() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = NewHero(tree);
        var hazard = new StoryCyclicHazard { Name = "CmBlockFrontHazard", Damage = 12 };
        tree.Root.AddChild(hazard);
        try {
            BlockSystem block = RaiseOrdinaryBlock(player, facingRight: true);
            hazard.GlobalPosition = player.GlobalPosition + new Vector2(40f, 0f);
            hazard.ForcePhase(HazardPhase.Active, 5f);
            int hp = player.CurrentHP;
            int charges = block.CurrentCharges;

            AssertThat(hazard.ApplyToPlayer(player)).IsTrue();
            AssertThat(player.CurrentHP)
                .OverrideFailureMessage("A blocked hazard tick deals no HP.")
                .IsEqual(hp);
            AssertThat(block.CurrentCharges)
                .OverrideFailureMessage("A hazard tick is Hazard class: exactly one charge (D03c).")
                .IsEqual(charges - 1);
            AssertThat(block.IsInShieldStun)
                .OverrideFailureMessage("An ordinary hazard block sets the shieldstun its damage earns.")
                .IsTrue();
            AssertThat(player.CurrentState).IsEqual(CharacterState.Blocking);
            AssertThat(player.Velocity)
                .OverrideFailureMessage("A blocked tick carries no knockback.")
                .IsEqual(Vector2.Zero);
            AssertThat(player.EchoPool).IsEqual(0f);
            AssertThat(player.CurrentUltimateMeter).IsEqual(0f);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            hazard.Free();
        }
    }

    [TestCase]
    public void AHazardTickFromBehindTheStanceLandsInFull() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = NewHero(tree);
        var hazard = new StoryCyclicHazard { Name = "CmBlockRearHazard", Damage = 12 };
        tree.Root.AddChild(hazard);
        try {
            // Facing left, with the hazard on the right: behind the guard.
            BlockSystem block = RaiseOrdinaryBlock(player, facingRight: false);
            hazard.GlobalPosition = player.GlobalPosition + new Vector2(40f, 0f);
            hazard.ForcePhase(HazardPhase.Active, 5f);
            int hp = player.CurrentHP;
            int charges = block.CurrentCharges;

            AssertThat(hazard.ApplyToPlayer(player)).IsTrue();
            AssertThat(player.CurrentHP).IsEqual(hp - 12);
            AssertThat(block.CurrentCharges)
                .OverrideFailureMessage("A tick from behind never reaches the block.")
                .IsEqual(charges);
            AssertThat(player.Velocity.X < 0f)
                .OverrideFailureMessage("A landed tick keeps its knockback (away from the hazard).")
                .IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            hazard.Free();
        }
    }

    [TestCase]
    public void TemporalAegisAbsorbsAHazardTickBeforeTheBlockSpendsACharge() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = NewHero(tree);
        var hazard = new StoryCyclicHazard { Name = "CmBlockAegisHazard", Damage = 12 };
        tree.Root.AddChild(hazard);
        try {
            BlockSystem block = RaiseOrdinaryBlock(player, facingRight: true);
            player.GrantTemporalAegis();
            hazard.GlobalPosition = player.GlobalPosition + new Vector2(40f, 0f);
            hazard.ForcePhase(HazardPhase.Active, 5f);
            int hp = player.CurrentHP;
            int charges = block.CurrentCharges;

            AssertThat(hazard.ApplyToPlayer(player)).IsTrue();
            AssertThat(player.HasTemporalAegis)
                .OverrideFailureMessage("D02b: Aegis sits before block and absorbs the tick.")
                .IsFalse();
            AssertThat(player.CurrentHP).IsEqual(hp);
            AssertThat(block.CurrentCharges)
                .OverrideFailureMessage("A tick Aegis absorbed whole never reaches the block.")
                .IsEqual(charges);
            AssertThat(block.IsInShieldStun).IsFalse();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            hazard.Free();
        }
    }

    /// <summary>
    /// CM-FIXB-STORY (findings B7 / B21; D01: hit eligibility and invulnerability
    /// come before every layer). A hero inside its post-rewind protection is
    /// immune to a hazard tick, so a raised block must not answer it: no charge,
    /// no shieldstun and no shatter from a tick the body is immune to — and,
    /// D01's order again, no Temporal Aegis spent on it either. The sim refuses
    /// an invulnerable fighter first too (<c>ResolveFighterHit</c>).
    /// </summary>
    [TestCase]
    public void APostRewindProtectedHeroSpendsNoChargeAegisOrGuardOnAHazardTick() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = NewHero(tree);
        var hazard = new StoryCyclicHazard { Name = "CmFixbProtectedHazard", Damage = 12 };
        tree.Root.AddChild(hazard);
        try {
            BlockSystem block = RaiseOrdinaryBlock(player, facingRight: true);
            // One charge left: an ordinary block of this tick would shatter.
            block.DepleteCharges(block.CurrentCharges - 1);
            AssertThat(block.CurrentCharges).IsEqual(1);
            player.ArmRecoveryProtection();
            AssertThat(player.IsPostRewindInvulnerable).IsTrue();
            hazard.GlobalPosition = player.GlobalPosition + new Vector2(40f, 0f);
            hazard.ForcePhase(HazardPhase.Active, 5f);
            int hp = player.CurrentHP;

            AssertThat(hazard.ApplyToPlayer(player)).IsTrue();
            AssertThat(player.CurrentHP).IsEqual(hp);
            AssertThat(block.CurrentCharges)
                .OverrideFailureMessage("An invulnerable hero's raised block must not spend a charge on a hazard tick.")
                .IsEqual(1);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("A tick the body is immune to must not shatter the guard.")
                .IsEqual(CharacterState.Blocking);
            AssertThat(block.IsInShieldStun).IsFalse();
            AssertThat(block.LockoutRemainingSeconds).IsEqual(0f);

            // D01's order: the refusal comes before Aegis and the barrier as well.
            player.GrantTemporalAegis();
            PlayerController.EnvironmentalHitResult result = player.ApplyEnvironmentalHit(
                12, player.GlobalPosition + new Vector2(40f, 0f), blockable: true, out int applied);
            AssertThat((int)result).IsEqual((int)PlayerController.EnvironmentalHitResult.Protected);
            AssertThat(applied).IsEqual(0);
            AssertThat(player.HasTemporalAegis)
                .OverrideFailureMessage("A hazard tick the body is immune to must not spend Temporal Aegis.")
                .IsTrue();
            AssertThat(block.CurrentCharges).IsEqual(1);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            hazard.Free();
        }
    }

    [TestCase]
    public void DrowningIsAPenaltyNotAHitAndIgnoresARaisedBlock() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = NewHero(tree);
        var water = new RisingWaterZone {
            Name = "CmBlockWater",
            WaterID = "test.cm_block.water",
            StepHeights = new[] { 0f },
            AutoAdvanceSeconds = 0f,
            DrownGraceSeconds = 0.5f,
            DrownTickSeconds = 0.5f,
            DrownDamage = 5
        };
        tree.Root.AddChild(water);
        try {
            water.GlobalPosition = Vector2.Zero;
            BlockSystem block = RaiseOrdinaryBlock(player, facingRight: true);
            player.GlobalPosition = new Vector2(0f, 100f);
            water.AddPlayer(player);
            int hp = player.CurrentHP;
            int charges = block.CurrentCharges;

            for (int tick = 0; tick < 20; tick++) water.TickSubmersion(0.1f);
            AssertThat(player.CurrentHP < hp)
                .OverrideFailureMessage("Drowning is unblockable: the raised guard does not stop it.")
                .IsTrue();
            AssertThat(block.CurrentCharges)
                .OverrideFailureMessage("A drown tick never spends a charge.")
                .IsEqual(charges);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            water.Free();
            player.Free();
        }
    }

    [TestCase]
    public void ABlockedDischargeStillDrainsMeterAndDebrisDuringDefySpendsNothing() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PackedScene extractorScene = ResourceLoader.Load<PackedScene>("res://scenes/templates/ChronalExtractorTemplate.tscn");
        var extractor = extractorScene.Instantiate<ChronalExtractor>();
        tree.Root.AddChild(extractor);
        PlayerController player = NewHero(tree);
        FallingDebris debris = null;
        try {
            player.RestoreStoryCheckpoint(Vector2.Zero, player.MaximumHP, 50f);
            BlockSystem block = RaiseOrdinaryBlock(player, facingRight: true);
            extractor.GlobalPosition = player.GlobalPosition + new Vector2(60f, 0f);
            int hp = player.CurrentHP;
            int charges = block.CurrentCharges;

            extractor.ApplyDischargeToPlayer(player);
            AssertThat(player.CurrentHP).IsEqual(hp);
            AssertThat(block.CurrentCharges).IsEqual(charges - 1);
            AssertThat(player.Velocity)
                .OverrideFailureMessage("A blocked discharge carries no knockback.")
                .IsEqual(Vector2.Zero);
            AssertThat(player.CurrentUltimateMeter)
                .OverrideFailureMessage("D03c: the discharge's meter drain is not a hit and still applies.")
                .IsEqualApprox(50f - extractor.UltimateDrain, 0.01f);

            // Debris during the Defy startup: rejected before the block.
            block = RaiseOrdinaryBlock(player, facingRight: true);
            FireDefy(player);
            int chargesInDefy = block.CurrentCharges;
            debris = new FallingDebris { Name = "CmBlockDebris" };
            tree.Root.AddChild(debris);
            debris.Launch(new Vector2(0f, 500f));
            debris.GlobalPosition = player.GlobalPosition + new Vector2(40f, -20f);
            int hpInDefy = player.CurrentHP;
            AssertThat(debris.ApplyTo(player)).IsEqual(0);
            AssertThat(player.CurrentHP).IsEqual(hpInDefy);
            AssertThat(block.CurrentCharges)
                .OverrideFailureMessage("Debris rejected by the Defy window spends no charge.")
                .IsEqual(chargesInDefy);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            if (debris != null && GodotObject.IsInstanceValid(debris)) debris.Free();
            player.Free();
            extractor.Free();
        }
    }

    // ---- Harness -------------------------------------------------------------

    /// <summary>A hero with no Story progression, at the origin, with an unspent Defy.</summary>
    private static PlayerController NewHero(SceneTree tree) {
        PlayerController player = CharacterFactory.CreateCharacter(
            "einstein", 0, applyStoryProgression: false, applyLegacyUnlockLocks: false);
        tree.Root.AddChild(player);
        player.GlobalPosition = Vector2.Zero;
        player.Velocity = Vector2.Zero;
        return player;
    }

    /// <summary>
    /// The stance up with no press (StartBlock + the state), so the perfect-block
    /// window stays shut and every block is ordinary.
    /// </summary>
    private static BlockSystem RaiseOrdinaryBlock(PlayerController player, bool facingRight) {
        player.IsFacingRight = facingRight;
        BlockSystem block = player.GetNode<BlockSystem>("BlockSystem");
        block.StartBlock();
        player.TransitionTo(CharacterState.Blocking);
        player.Velocity = Vector2.Zero;
        AssertThat(block.IsBlocking).IsTrue();
        AssertThat(player.IsPerfectBlockWindowOpen).IsFalse();
        return block;
    }

    /// <summary>Puts the hero inside the active Defy's invulnerable startup (frame 1).</summary>
    private static void FireDefy(PlayerController player) {
        player.SeedDefyFrameForTest(1);
        AssertThat(player.IsDefyProtected).IsTrue();
    }

    /// <summary>Advances the controller's own physics loop for N frames.</summary>
    private static void HoldInput(PlayerController player, GameplayButtons buttons, int frames) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        for (int frame = 0; frame < frames; frame++) player._PhysicsProcess(1.0 / 60.0);
    }

    /// <summary>An impulse-free, stun-free test hit so state stays Idle.</summary>
    private static HitPayload Hit(float damage) => new() {
        AttackerIndex = 1,
        TargetIndex = 0,
        AttackID = "test.hit",
        HitboxID = "primary",
        AttackClass = AttackClass.Basic,
        Damage = damage,
        Knockback = Vector2.Zero,
        HitstunDuration = 0f,
        HitOrigin = new Vector2(-20f, 0f),
        AttackerFacingRight = true
    };
}
