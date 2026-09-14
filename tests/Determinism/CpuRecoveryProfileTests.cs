using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 11 A9b — the nine per-character F19 recovery profiles from
/// <c>docs/design-contracts/CPU_RECOVERY.md</c>. One case per character: the
/// profile is linked to that character's <b>canonical authored movement ability
/// ID</b>, it plans with the numbers the normalized Fighter loadout actually
/// carries, it presses that ability off-stage, and it respects the row's named
/// exclusion by never pressing a Special.
/// </summary>
/// <remarks>
/// <para>
/// These load the authored <c>.tres</c> through <see cref="AuthoredResources"/>
/// deliberately: the contract asks for profiles "linked to canonical ability
/// identifiers", and the only way to prove a link is to read the identifier the
/// game ships. That is why the suite requires the Godot runtime.
/// </para>
/// <para>
/// <b>No profile approves an optional mobility Special.</b> Eight characters have
/// no candidate at all, and Pocahontas's Spirit Strike — the one candidate the
/// contract names — is rejected because the shipped simulation translates the
/// caster only inside the movement-ability path, so its "forced diagonal-up dash"
/// is unimplemented. The contract's own answer applies: "Reject missing or
/// unvalidated optional Special mappings; fall back to verified jumps/movement."
/// </para>
/// </remarks>
[TestSuite]
[RequireGodotRuntime]
public class CpuRecoveryProfileTests {
    private const int Ticks = 600;

    [TestCase]
    public void EinsteinPlansRelativityWarpAndNeverTreatsRelativityRiftAsRecovery() =>
        AssertProfile(
            "einstein",
            movementAbilityID: "einstein_relativity_warp",
            expectedMovementKind: CpuRecoveryProfile.MovementKindWarp,
            excludedAbilityID: "einstein_relativity_rift",
            excludedSlot: 2);

    [TestCase]
    public void JoanPlansAscendantWingsWithNoStoryWingsRefresh() =>
        AssertProfile(
            "joan",
            movementAbilityID: "joan_ascendant_wings",
            expectedMovementKind: CpuRecoveryProfile.MovementKindGlide,
            excludedAbilityID: null,
            excludedSlot: 0);

    [TestCase]
    public void LeonardoPlansOrnithopterFlightAndNeverStepsOnHisTurret() =>
        AssertProfile(
            "leonardo",
            movementAbilityID: "leonardo_ornithopter_flight",
            expectedMovementKind: CpuRecoveryProfile.MovementKindGlide,
            excludedAbilityID: "leonardo_clockwork_turret",
            excludedSlot: 2);

    [TestCase]
    public void TeslaPlansLightningBlinkAndNeverLorentzPulse() =>
        AssertProfile(
            "tesla",
            movementAbilityID: "tesla_lightning_blink",
            expectedMovementKind: CpuRecoveryProfile.MovementKindBlink,
            excludedAbilityID: "tesla_lorentz_pulse",
            excludedSlot: 2);

    [TestCase]
    public void ShakespearePlansProsperosFlightWithNoRetiredTeleport() =>
        AssertProfile(
            "shakespeare",
            movementAbilityID: "shakespeare_prosperos_flight",
            expectedMovementKind: CpuRecoveryProfile.MovementKindGlide,
            excludedAbilityID: null,
            excludedSlot: 0);

    [TestCase]
    public void MozartPlansSonataDriftAsAVerticalPopOnly() {
        CpuRecoveryProfile profile = AssertProfile(
            "mozart",
            movementAbilityID: "mozart_sonata_drift",
            expectedMovementKind: CpuRecoveryProfile.MovementKindFloat,
            excludedAbilityID: null,
            excludedSlot: 0);
        // The contract's Mozart row is about height, not distance: the apex staff
        // platform is a by-product, and the planner must not treat it as
        // permanent geometry. The Float kind carries no horizontal travel at all,
        // which is what keeps that honest.
        AssertThat(profile.MovementHorizontalReach).IsEqual(FP64.Zero);
        AssertThat(profile.MovementLift > FP64.Zero).IsTrue();
    }

    [TestCase]
    public void CleopatraPlansDesertMirageAndNeverSandstormVortex() =>
        AssertProfile(
            "cleopatra",
            movementAbilityID: "cleopatra_desert_mirage",
            expectedMovementKind: CpuRecoveryProfile.MovementKindTeleport,
            excludedAbilityID: "cleopatra_sandstorm_vortex",
            excludedSlot: 2);

    [TestCase]
    public void LincolnPlansRailChargeHorizontallyAndTakesHisHeightFromJumps() {
        CpuRecoveryProfile profile = AssertProfile(
            "lincoln",
            movementAbilityID: "lincoln_rail_charge",
            expectedMovementKind: CpuRecoveryProfile.MovementKindDash,
            excludedAbilityID: null,
            excludedSlot: 0);
        // "Horizontal armored travel; obtain necessary height through legal
        // jumps. Armor does not prevent damage or pit KOs."
        AssertThat(profile.MovementProvidesLift).IsFalse();
        AssertThat(profile.MovementLift).IsEqual(FP64.Zero);
        AssertThat(profile.JumpApexHeight > FP64.Zero).IsTrue();
    }

    [TestCase]
    public void PocahontasPlansBreezeGlideWithItsJumpResetAndRejectsSpiritStrike() {
        CpuRecoveryProfile profile = AssertProfile(
            "pocahontas",
            movementAbilityID: "pocahontas_breeze_glide",
            expectedMovementKind: CpuRecoveryProfile.MovementKindGlide,
            excludedAbilityID: "pocahontas_vine_snare",
            excludedSlot: 2);
        // "the baseline double-jump reset; the reset can supply another legal
        // jump within the same plan."
        AssertThat(profile.MovementResetsJump).IsTrue();
        // The one named candidate, rejected after validation: Spirit Strike is
        // authored as a Melee execution and the simulation translates the caster
        // only inside ApplyMovement, so the design's forced diagonal-up dash does
        // not exist to plan against.
        AbilityData spiritStrike = AuthoredResources.Load<AbilityData>(
            "res://resources/Abilities/pocahontas/special_1.tres");
        AssertThat(spiritStrike.AbilityID).IsEqual("pocahontas_spirit_strike");
        AssertThat(spiritStrike.ExecutionType).IsEqual(AbilityExecutionType.Melee);
        AssertThat(profile.MobilitySpecial).IsEqual(CpuMobilitySpecialSlot.None);
    }

    /// <summary>
    /// The shared per-character assertion: canonical ability link, normalized
    /// planning numbers, an actual off-stage movement-ability press on all three
    /// bands, and never a Special on any of them.
    /// </summary>
    private static CpuRecoveryProfile AssertProfile(
        string characterID,
        string movementAbilityID,
        int expectedMovementKind,
        string excludedAbilityID,
        int excludedSlot) {
        CharacterData data = AuthoredResources.Load<CharacterData>(
            $"res://resources/Characters/{characterID}_data.tres");
        AssertThat(data).IsNotNull();
        AssertThat(data.MovementAbility.AbilityID)
            .OverrideFailureMessage($"{characterID}'s authored movement ability changed identity.")
            .IsEqual(movementAbilityID);
        if (excludedAbilityID != null) {
            AbilityData excluded = excludedSlot == 2 ? data.SpecialAttackTwo : data.SpecialAttackOne;
            AssertThat(excluded.AbilityID)
                .OverrideFailureMessage($"{characterID}'s excluded ability moved slot.")
                .IsEqual(excludedAbilityID);
        }

        FighterLoadout loadout = FighterLoadoutFactory.FromCharacterData(data);
        CpuRecoveryProfile profile = CpuRecoveryProfile.FromLoadout(in loadout);
        AssertThat(profile.HasKit).IsTrue();
        AssertThat(profile.MovementKind).IsEqual(expectedMovementKind);
        // Every planning number is the loadout's, not a second copy.
        AssertThat(profile.JumpSpeed).IsEqual(loadout.JumpSpeed);
        AssertThat(profile.MoveSpeed).IsEqual(loadout.MoveSpeed);
        AssertThat(profile.MaxJumpCount).IsEqual(loadout.MaxJumpCount);
        AssertThat(profile.MovementCooldownFrames)
            .IsEqual(loadout.AbilityModes.MovementCooldownFrames);
        // No character approves an optional mobility Special today.
        AssertThat(profile.MobilitySpecial)
            .OverrideFailureMessage($"{characterID} must not treat a Special slot as a capability.")
            .IsEqual(CpuMobilitySpecialSlot.None);

        foreach (CpuDifficulty band in new[] {
                     CpuDifficulty.Easy, CpuDifficulty.Normal, CpuDifficulty.Hard }) {
            var cpu = new FighterCpuController(band, 8821, null, null, null, profile);
            CpuDecisionObservation observation = OverTheParisPit();
            PlayerInputFrame previous = default;
            int movementEdges = 0;
            int specialEdges = 0;
            for (uint tick = 0; tick < Ticks; tick++) {
                PlayerInputFrame frame = cpu.Sample(tick, in observation, in previous);
                previous = frame;
                if (frame.IsPressed(GameplayButtons.MovementAbility)) movementEdges++;
                if (frame.IsPressed(GameplayButtons.Special1)
                    || frame.IsPressed(GameplayButtons.Special2)) specialEdges++;
            }
            AssertThat(movementEdges > 0)
                .OverrideFailureMessage($"{characterID} on {band} never attempted its movement ability.")
                .IsTrue();
            AssertThat(specialEdges)
                .OverrideFailureMessage($"{characterID} on {band} cast a Special to recover.")
                .IsEqual(0);
        }
        return profile;
    }

    /// <summary>
    /// Unsupported over Paris's central courtyard pit with no jumps left, so the
    /// movement ability is the only tool the planner has. Values come from
    /// <c>FighterStageGeometry.Paris</c>: walls ±9, blast zone −5, a 5.0-wide
    /// pit bounded at ∓2.5.
    /// </summary>
    private static CpuDecisionObservation OverTheParisPit() {
        FighterStageGeometry paris = FighterStageGeometry.ForStage("paris_bastille");
        return new CpuDecisionObservation {
            SelfPositionXRaw = FP64.Zero.RawValue,
            SelfPositionYRaw = FP64.FromDouble(-0.5).RawValue,
            SelfVelocityYRaw = FP64.FromInt(-5).RawValue,
            TargetPositionXRaw = FP64.FromInt(4).RawValue,
            Stocks = 3,
            IsGrounded = 0,
            RemainingJumps = 0,
            SelfCurrentHP = 100,
            SelfMaxHP = 100,
            TargetCurrentHP = 100,
            TargetMaxHP = 100,
            HasStageBounds = 1,
            LeftWallRaw = paris.LeftWall.RawValue,
            RightWallRaw = paris.RightWall.RawValue,
            CeilingRaw = paris.Ceiling.RawValue,
            BottomBlastZoneRaw = paris.BottomBlastZone.RawValue,
            PlatformCount = paris.Platforms.Length,
            HasFloorSegments = 1,
            HasFloorSupportUnderSelf = 0,
            LaunchTrajectoryCrossesGap = 1,
            HasFloorEdgeLeft = 1,
            NearestFloorEdgeLeftXRaw = FP64.FromDouble(-2.5).RawValue,
            HasFloorEdgeRight = 1,
            NearestFloorEdgeRightXRaw = FP64.FromDouble(2.5).RawValue,
            CurrentGapWidthRaw = FP64.FromInt(5).RawValue
        };
    }
}
