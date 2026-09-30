using xpTURN.Klotho.Deterministic.Math;

namespace FTT.FighterSim {

    /// <summary>
    /// Which ability slot, if any, the F19 recovery planner is allowed to treat
    /// as a mobility option beyond the character's movement ability.
    /// </summary>
    /// <remarks>
    /// <c>CPU_RECOVERY.md</c>: "Reject missing or unvalidated optional Special
    /// mappings; fall back to verified jumps/movement instead of treating a slot
    /// number as a capability." That is why this is an explicit approval and not
    /// a slot index the planner infers.
    /// </remarks>
    public enum CpuMobilitySpecialSlot {
        /// <summary>No Special is a validated recovery tool for this character.</summary>
        None = 0,
        SpecialOne = 1,
        SpecialTwo = 2
    }

    /// <summary>
    /// The per-character F19 recovery profile: which movement ability the planner
    /// steers with, how that ability actually travels in the shipped simulation,
    /// and which optional mobility Special (if any) has been validated.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Code-owned, never a resource.</b> Fighter Mode stays competitively
    /// normalized and no Story tuning may reach the CPU —
    /// <see cref="CpuBandTuning"/> is the precedent. What lives here is the AI's
    /// <i>planning policy</i>; every number it plans against
    /// (<see cref="MovementDistance"/>, <see cref="JumpSpeed"/>,
    /// <see cref="MoveSpeed"/>, cooldowns, durations) is copied straight out of
    /// the normalized <see cref="FighterLoadout"/> the human player's fighter was
    /// built from, so there is no second canonical value anywhere.
    /// </para>
    /// <para>
    /// <b>Canonical ability identifiers.</b> The profile keys off
    /// <see cref="FighterCharacterID"/>, which <c>FighterLoadoutFactory</c>
    /// resolves from the authored <c>CharacterData.CharacterID</c>; the ability
    /// each row names is that character's authored <c>movement.tres</c>
    /// (<c>einstein_relativity_warp</c>, <c>joan_ascendant_wings</c>,
    /// <c>leonardo_ornithopter_flight</c>, <c>lincoln_rail_charge</c>,
    /// <c>cleopatra_desert_mirage</c>, <c>tesla_lightning_blink</c>,
    /// <c>shakespeare_prosperos_flight</c>, <c>mozart_sonata_drift</c>,
    /// <c>tubman_north_star_leap</c>). The planner never hardcodes one of those
    /// strings as a capability: it reads <see cref="MovementKind"/>, which is the
    /// authored <c>MovementType</c> the simulation itself switches on.
    /// </para>
    /// <para>
    /// <b>Every named exclusion in <c>CPU_RECOVERY.md</c> is honoured by
    /// construction.</b> Relativity Rift, Lorentz Pulse, Sandstorm Vortex, Vine
    /// Snare, the Leonardo turret and the Joan/Lincoln Story perks are all
    /// Specials or Story-only upgrades, and <c>CPU_RECOVERY.md</c> names
    /// Tubman's Conductor's Call and Foresight as "not recovery tools";
    /// <see cref="MobilitySpecial"/> is <see cref="CpuMobilitySpecialSlot.None"/>
    /// for every character since the Package 13 roster swap retired the one
    /// approved candidate (Pocahontas's Spirit Strike) — see
    /// <see cref="MobilitySpecialFor"/>.
    /// </para>
    /// </remarks>
    public readonly struct CpuRecoveryProfile {
        /// <summary>The authored <c>MovementType</c> ordinals the simulation switches on.</summary>
        public const int MovementKindBlink = 0;
        public const int MovementKindGlide = 1;
        public const int MovementKindDash = 2;
        public const int MovementKindTeleport = 3;
        public const int MovementKindWarp = 4;
        /// <summary>
        /// Mozart's Sonata Drift. Since Package 13 W7b (M04) a rising glissando of
        /// the authored distance along the held stick, so it plans as directional.
        /// </summary>
        public const int MovementKindFloat = 5;
        /// <summary>Package 13 W7a (A08): Prospero's single gust burst along facing, then a normal fall.</summary>
        public const int MovementKindGust = 6;
        /// <summary>Package 13 W7b (A08): Joan's rising slash-leap (the held Wing-Dive descends, so the planner never holds it).</summary>
        public const int MovementKindWingDive = 10;
        /// <summary>Package 13 W7b (C03): Cleopatra's 8-direction sand rush.</summary>
        public const int MovementKindSandRush = 11;

        /// <summary>
        /// True when the profile was built from a real loadout. A CPU constructed
        /// without one (the Story Mirror Paradox adapter) plans with universal
        /// movement only, which is all a campaign level ever needs — its
        /// observation carries no stage bounds, so recovery never runs at all.
        /// </summary>
        public bool HasKit { get; init; }
        public FighterCharacterID Character { get; init; }

        /// <summary>Authored <c>MovementType</c>; see the MovementKind constants.</summary>
        public int MovementKind { get; init; }
        /// <summary>
        /// <c>true</c> when the ability translates along the held stick
        /// (Blink/Teleport/Warp) rather than along current facing. The planner
        /// must aim the stick before the edge for these and must face the stage
        /// for the others — getting that backwards throws the fighter further out.
        /// </summary>
        public bool MovementIsDirectional { get; init; }
        /// <summary>Normalized translation distance in world units (Blink/Teleport/Warp).</summary>
        public FP64 MovementDistance { get; init; }
        /// <summary>Normalized launch speed in world units per second (Glide/Dash/Float).</summary>
        public FP64 MovementSpeed { get; init; }
        /// <summary>
        /// Package 13 W7b (A08): the Ascendant Wings rise's length — the
        /// movement ability's authored startup + active + recovery frames.
        /// </summary>
        public int MovementRiseFrames { get; init; }
        /// <summary>Authored float/glide duration in frames.</summary>
        public int MovementDurationFrames { get; init; }
        public int MovementCooldownFrames { get; init; }
        /// <summary>Non-zero when the ability refunds the air-jump budget (no shipped kit since the Package 13 roster swap).</summary>
        public bool MovementResetsJump { get; init; }
        /// <summary>
        /// Package 13 W5: how much farther than the normal capture box the
        /// movement ability snaps to a ledge — Tubman's North Star Leap
        /// (<see cref="FighterKitMotion.LeapLedgeSnapBonus"/>, 0.5 units), zero
        /// for everyone else. The sim applies the snap itself; the planner uses
        /// it to credit the leap with that much extra reach toward an edge.
        /// </summary>
        public FP64 MovementLedgeSnapBonus { get; init; }
        /// <summary>Normalized jump launch speed, world units per second.</summary>
        public FP64 JumpSpeed { get; init; }
        /// <summary>Normalized ground run speed, world units per second.</summary>
        public FP64 MoveSpeed { get; init; }
        public FP64 AirControl { get; init; }
        public int MaxJumpCount { get; init; }
        /// <summary>The validated optional mobility Special; <see cref="CpuMobilitySpecialSlot.None"/> for the whole roster since Package 13.</summary>
        public CpuMobilitySpecialSlot MobilitySpecial { get; init; }

        /// <summary>
        /// The movement ability lifts the fighter (glide boost, Sonata Drift pop,
        /// an upward-aimed warp). A purely horizontal option — Lincoln's Rail
        /// Charge — cannot supply height, which is exactly why the contract tells
        /// him to "obtain necessary height through legal jumps".
        /// </summary>
        public bool MovementProvidesLift =>
            MovementKind is MovementKindGlide or MovementKindGust or MovementKindWingDive
            || MovementIsDirectional;

        /// <summary>
        /// Vertical gain one air jump buys, <c>v² / 2g</c> against the
        /// simulation's own gravity. Pure planning arithmetic over normalized
        /// numbers; nothing here is a second tuning constant.
        /// </summary>
        public FP64 JumpApexHeight {
            get {
                FP64 gravity = -FighterMovementSystem.GravityPerSecondSquared;
                if (gravity <= FP64.Zero || JumpSpeed <= FP64.Zero) return FP64.Zero;
                return JumpSpeed * JumpSpeed / (FP64.FromInt(2) * gravity);
            }
        }

        /// <summary>
        /// Horizontal ground the fighter covers across one full air jump at full
        /// drift. Used to decide whether a landing or ledge is plausibly
        /// reachable before committing an action.
        /// </summary>
        public FP64 JumpHorizontalReach {
            get {
                FP64 gravity = -FighterMovementSystem.GravityPerSecondSquared;
                if (gravity <= FP64.Zero) return FP64.Zero;
                return MoveSpeed * AirControl * (FP64.FromInt(2) * JumpSpeed / gravity);
            }
        }

        /// <summary>
        /// Horizontal ground the movement ability covers. Directional abilities
        /// translate by their authored distance; a glide/dash carries its launch
        /// speed for the authored duration; Sonata Drift's pure vertical pop
        /// contributes none.
        /// </summary>
        public FP64 MovementHorizontalReach {
            get {
                if (MovementIsDirectional) return MovementDistance + MovementLedgeSnapBonus;
                // A08: the gust carries its authored forward distance, no glide.
                if (MovementKind == MovementKindGust) return MovementDistance;
                // A08: the Wing-Dive's rise is vertical; its forward travel is a
                // descent the planner never takes offstage.
                if (MovementKind == MovementKindWingDive) return FP64.Zero;
                FP64 seconds = FP64.FromInt(MovementDurationFrames)
                    / FP64.FromInt(FighterSimulation.TickRate);
                return MovementSpeed * seconds;
            }
        }

        /// <summary>
        /// Vertical gain the movement ability buys. Glide and Float launch at
        /// half their authored speed (the simulation's <c>speed / 2</c>) and then
        /// coast under reduced gravity; a directional ability translates by its
        /// authored distance when aimed upward; a pure dash buys nothing.
        /// </summary>
        public FP64 MovementLift {
            get {
                if (MovementIsDirectional) return MovementDistance;
                // A08: the gust's rise is the shared kit-motion rulebook value.
                if (MovementKind == MovementKindGust) {
                    return FP64.FromDouble(FTT.Combat.KitMotionRules.ProsperoGustRiseUnits);
                }
                // A08: the slash-leap climbs at the authored speed for the whole
                // cast (no gravity, no glide) — FighterKitMotion.WingRise.
                if (MovementKind == MovementKindWingDive) {
                    return MovementSpeed * FP64.FromInt(MovementRiseFrames)
                        / FP64.FromInt(FighterSimulation.TickRate);
                }
                if (MovementKind == MovementKindGlide) {
                    FP64 launch = MovementSpeed > FP64.Zero ? MovementSpeed : FP64.FromInt(8);
                    FP64 gravity = -FighterMovementSystem.GravityPerSecondSquared;
                    if (gravity <= FP64.Zero) return FP64.Zero;
                    launch /= FP64.FromInt(2);
                    return launch * launch / (FP64.FromInt(2) * gravity);
                }
                return FP64.Zero;
            }
        }

        /// <summary>
        /// Builds the profile for a normalized Fighter loadout. Every number is
        /// read from <paramref name="loadout"/>; the only thing this method
        /// decides is <i>policy</i> — which optional Special the planner may use.
        /// </summary>
        public static CpuRecoveryProfile FromLoadout(in FighterLoadout loadout) {
            int kind = loadout.AbilityModes.MovementType;
            var character = (FighterCharacterID)loadout.CharacterID;
            return new CpuRecoveryProfile {
                HasKit = true,
                Character = character,
                MovementKind = kind,
                // Package 13 W7b: the sand rush (C03) and the glissando (M04)
                // travel along the held stick like the blink.
                MovementIsDirectional =
                    kind is MovementKindBlink or MovementKindTeleport or MovementKindWarp
                        or MovementKindSandRush or MovementKindFloat,
                MovementDistance = loadout.AbilityModes.MovementDistance > FP64.Zero
                    ? loadout.AbilityModes.MovementDistance
                    : FP64.FromInt(2),
                MovementSpeed = loadout.AbilityModes.MovementSpeed > FP64.Zero
                    ? loadout.AbilityModes.MovementSpeed
                    : FP64.FromInt(8),
                MovementDurationFrames = loadout.AbilityModes.MovementDurationFrames > 0
                    ? loadout.AbilityModes.MovementDurationFrames
                    : 180,
                MovementCooldownFrames = loadout.AbilityModes.MovementCooldownFrames,
                MovementRiseFrames = loadout.AbilityModes.MovementHit.TotalFrames,
                MovementResetsJump = loadout.AbilityModes.MovementResetsJump != 0,
                MovementLedgeSnapBonus = character == FighterCharacterID.Tubman
                    ? FighterKitMotion.LeapLedgeSnapBonus
                    : FP64.Zero,
                JumpSpeed = loadout.JumpSpeed,
                MoveSpeed = loadout.MoveSpeed,
                AirControl = loadout.AirControl,
                MaxJumpCount = loadout.MaxJumpCount,
                MobilitySpecial = MobilitySpecialFor(character)
            };
        }

        /// <summary>
        /// The validated optional mobility Special per character: none.
        /// </summary>
        /// <remarks>
        /// <para>
        /// No character has a candidate: the contract names Relativity Rift,
        /// Lorentz Pulse, Sandstorm Vortex, the Leonardo turret, the retired
        /// Shakespeare teleport, the Joan/Lincoln Story perks and — since the
        /// 2026-09-29 roster swap — Tubman's Conductor's Call and Foresight
        /// explicitly as <i>not</i> recovery tools, and Mozart's staff platform
        /// is a product of his movement ability rather than a Special.
        /// </para>
        /// <para>
        /// <b>Package 13 W5.</b> Pocahontas's Spirit Strike was the one approved
        /// entry (Package 12 W4, GAP-10b); it left the roster with her. The
        /// planner's mobility-Special branch and
        /// <c>CpuBandTuning.RecoveryMobilitySpecialPercent</c> survive, dormant,
        /// so a future kit can be approved here after the validation
        /// <c>CPU_RECOVERY.md</c> demands, without re-plumbing the planner.
        /// </para>
        /// </remarks>
        public static CpuMobilitySpecialSlot MobilitySpecialFor(FighterCharacterID character) =>
            CpuMobilitySpecialSlot.None;
    }
}
