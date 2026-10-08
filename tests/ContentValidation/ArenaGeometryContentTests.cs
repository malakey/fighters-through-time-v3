using System;
using System.Collections.Generic;
using System.Reflection;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Playtest pass 2026-10-04, workstream ARENA — the campaign as it is really
/// built (each authored scene entered into the tree once per session, so the
/// code-built geometry exists), checked for the defects the Story bots found.
///
/// <para><b>P5 — gates cannot be jumped.</b> Every registered gate column (a
/// shield barrier's column, a sealed door's or a rockfall's) must top out above
/// the highest point the roster can reach from any standable surface within
/// <see cref="MaxApproachPixels"/> of it: the best double jump in the roster
/// (PlayerController's closed form, at the level's lowest gravity scale), with
/// <see cref="JumpHeadroom"/> for Story jump modifiers, plus
/// <see cref="MovementAllowancePixels"/> for a movement ability, from the top of
/// any floor, platform, wall or solid breakable (an Extractor is a step stool).
/// The bots double-jumped Orléans Gate B off the battlement Extractor.</para>
///
/// <para><b>P5, generalised (A1).</b> The gate list is not a hand list: every
/// sealed door (<see cref="StoryGateRules.DoorGroup"/>, which <c>BuildDoor</c>
/// joins) and every forcefield barrier must stand under a registered column that
/// covers its whole width at its top, and that column must clear the reach test.
/// The first audit's hand list missed Level 4's courtyard gate and Level 11's
/// breastwork earth (and the Level 11 lane roofs), all of which stopped at
/// y 0.</para>
///
/// <para><b>P5 — no holes.</b> The Orléans ground runs unbroken from the west
/// wall to the court wall (a 60 px gap at 8900–8960 dropped the hero out of the
/// world).</para>
///
/// <para><b>G5 — no crawlspaces.</b> No standable surface sits less than a hero's
/// height (plus a margin) under a solid ceiling: Level 4's top tower step was
/// 62 px under the walkway and wedged the 64 px hero.</para>
///
/// <para><b>P1 — bosses keep to their arenas.</b> Every campaign boss spawns
/// dormant inside an authored arena rect wide enough for its ranged band; the
/// pre-boss anchor, the absence beat and the pre-boss dialogue trigger all stand
/// outside it, so the reveal (arena entry) never contends with them.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ArenaGeometryContentTests {

    private const float MaxApproachPixels = 1000f;
    private const float JumpHeadroom = 1.25f;
    /// <summary>The longest movement-ability relocation in the roster: 4 units (Relativity Warp, North Star Leap).</summary>
    private const float MovementAllowancePixels = 4f * 60f;
    /// <summary>Half the checkpoint prompt's 60 px width: a hero striking the anchor stands within it.</summary>
    private const float PreBossAnchorClearancePixels = 30f;
    /// <summary>
    /// A pre-boss anchor this close west of an arena stands in its antechamber
    /// (Florence's, 80 px out), so the walk from it into the arena must be open.
    /// Farther anchors sit in earlier rooms behind their own puzzle doors (Levels
    /// 3 and 8, ~500 px out), which the gate checks cover.
    /// </summary>
    private const float AnchorApproachPixels = 200f;

    /// <summary>
    /// Levels that author gates, and how many columns each must register. A
    /// secondary pin: <see cref="EveryPuzzleGateStandsUnderAColumnNoHeroCanClear"/>
    /// enumerates the gates themselves, so a new gate fails there even if this
    /// table is not updated.
    /// </summary>
    private static readonly Dictionary<int, int> ExpectedGateColumns = new() {
        [1] = 1, [2] = 2, [3] = 1, [4] = 1, [6] = 1, [8] = 1, [9] = 1, [11] = 3, [12] = 1
    };

    /// <summary>
    /// Gates in Levels 1–15: the doors of L1, L3, L4, L6, L8, L9 and L11 (the
    /// breastwork), and the forcefield barriers of L2 (A, B) and L11 (Alpha, Bravo).
    /// </summary>
    private const int MinimumGateCount = 11;

    /// <summary>A column's foot sits within this of its gate's top edge.</summary>
    private const float GateFootTolerance = 40f;

    /// <summary>The Story hero's body (CharacterFactory: 40 x 64).</summary>
    private const float HeroHeightPixels = 64f;
    private const float HeroWidthPixels = 40f;
    /// <summary>Spare headroom a hero standing under a ceiling needs beyond its own height (float noise, the body's safe margin).</summary>
    private const float HeadroomMarginPixels = 4f;
    /// <summary>
    /// <c>StoryLevelControllerBase.BuildOneWayPlatform</c>'s <c>OneWayCollisionMargin</c>:
    /// a body this far into a one-way's top while falling is pushed up onto it.
    /// </summary>
    private const float OneWayLandingMarginPixels = 12f;

    private static List<LevelSnapshot> _snapshots;

    // === P5: gates ========================================================

    [TestCase]
    public void EveryGateColumnRisesBeyondTheRostersReachFromEveryNearbySurface() {
        var issues = new List<string>();
        foreach (LevelSnapshot level in Snapshots()) {
            if (level.LoadError != null) { issues.Add($"{level.Label}: {level.LoadError}"); continue; }
            float reach = ReachPixels(level.LowestGravityScale);
            foreach (Rect2 column in level.GateColumns) AddReachIssues(level, column, reach, issues);
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// A1 — the gate list is the build's own: every door that joins
    /// <see cref="StoryGateRules.DoorGroup"/> (all of <c>BuildDoor</c>'s, and
    /// Florence's workshop door) and every <see cref="ForcefieldBarrier"/> must
    /// stand under a registered gate column that covers its whole width with its
    /// foot at the gate's top edge, and that column must rise beyond the reach of
    /// every standable surface near it — the roster's best double jump at the
    /// level's lowest gravity with headroom, plus the highest movement-ability
    /// rise, from any floor, platform, wall or solid breakable (an Extractor or a
    /// generator is a step stool). Level 4's courtyard gate (top y 140, nothing
    /// above) and Level 11's breastwork earth (stopping at y 0) passed the old
    /// hand-listed audit and were both jumpable.
    /// </summary>
    [TestCase]
    public void EveryPuzzleGateStandsUnderAColumnNoHeroCanClear() {
        var issues = new List<string>();
        int gates = 0;
        foreach (LevelSnapshot level in Snapshots()) {
            if (level.LoadError != null) { issues.Add($"{level.Label}: {level.LoadError}"); continue; }
            float reach = ReachPixels(level.LowestGravityScale);
            foreach ((string name, Rect2 gate) in level.Gates) {
                gates++;
                bool covered = false;
                foreach (Rect2 column in level.GateColumns) {
                    if (!CoversGate(column, gate)) continue;
                    covered = true;
                    AddReachIssues(level, column, reach, issues);
                    break;
                }
                if (!covered) {
                    issues.Add($"{level.Label}: the gate '{name}' {gate} has no gate column over its whole " +
                        $"width with its foot at y {gate.Position.Y} (BuildGateColumn) — it can be jumped");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        AssertThat(gates).IsGreaterEqual(MinimumGateCount);
    }

    /// <summary>
    /// A column narrower than (or offset from) the gate below it leaves the gate's
    /// top as a ledge with open sky: Orléans Gate B's 48 px barrier is centred on
    /// its x while the old 20 px arch was anchored at its left edge, and the
    /// barrier's west 24 px at y 232 was a perch. Every gate (door or barrier)
    /// whose top sits at a column's foot and overlaps it must lie inside the
    /// column's span. Only gates are checked: a floor or ceiling that runs into a
    /// column at its foot (Level 8's corridor ceiling meets the vault column) is a
    /// wall junction, not a lip.
    /// </summary>
    [TestCase]
    public void NoGateLeavesALipBesideTheColumnAboveIt() {
        var issues = new List<string>();
        int gatesSeen = 0;
        foreach (LevelSnapshot level in Snapshots()) {
            if (level.LoadError != null) { issues.Add($"{level.Label}: {level.LoadError}"); continue; }
            foreach (Rect2 column in level.GateColumns) {
                foreach ((string name, Rect2 gate) in level.Gates) {
                    float overlap = Mathf.Min(gate.End.X, column.End.X) - Mathf.Max(gate.Position.X, column.Position.X);
                    if (overlap <= 0.5f) continue;
                    if (Mathf.Abs(gate.Position.Y - column.End.Y) > GateFootTolerance) continue;
                    gatesSeen++;
                    float westLip = column.Position.X - gate.Position.X;
                    float eastLip = gate.End.X - column.End.X;
                    if (westLip > 0.5f || eastLip > 0.5f) {
                        issues.Add($"{level.Label}: the gate '{name}' {gate} sticks out of its column {column} " +
                            $"(west lip {Mathf.Max(0f, westLip):0} px, east lip {Mathf.Max(0f, eastLip):0} px) at y {gate.Position.Y}");
                    }
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        // Every door and barrier listed at MinimumGateCount; the L12 curtain is a
        // wall (no gate) whose slot keeps its lower segment 300 px below the foot.
        AssertThat(gatesSeen).IsGreaterEqual(MinimumGateCount);
    }

    [TestCase]
    public void EveryGatedLevelRegistersItsGateColumns() {
        var issues = new List<string>();
        foreach (LevelSnapshot level in Snapshots()) {
            int expected = ExpectedGateColumns.TryGetValue(level.Index, out int count) ? count : 0;
            if (level.GateColumns.Count != expected) {
                issues.Add($"{level.Label}: {level.GateColumns.Count} gate columns (expected {expected})");
            }
            foreach (Rect2 column in level.GateColumns) {
                if (column.Position.Y > StoryGateRules.ColumnTopY + 0.5f) {
                    issues.Add($"{level.Label}: column {column} does not rise to StoryGateRules.ColumnTopY");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    // === P5: the Orléans floor hole =====================================

    [TestCase]
    public void TheOrleansGroundRunsUnbrokenFromTheWestWallToTheCourtWall() {
        LevelSnapshot orleans = Snapshots().Find(level => level.Index == (int)CampaignLevel.Orleans);
        AssertObject(orleans).IsNotNull();
        var spans = new List<(float Left, float Right)>(orleans.Floors);
        spans.Sort((a, b) => a.Left.CompareTo(b.Left));
        float covered = 0f;
        foreach ((float left, float right) in spans) {
            AssertThat(left <= covered + 0.5f)
                .OverrideFailureMessage($"The Orléans ground has a hole from {covered} to {left}.").IsTrue();
            covered = Mathf.Max(covered, right);
        }
        AssertFloat(covered).IsGreaterEqual(10540f);
    }

    // === G5: crawlspaces ==================================================

    /// <summary>
    /// G5 — no standable surface (a floor, platform, one-way, wall top or solid
    /// breakable) a hero can get onto may leave it less than
    /// <see cref="HeroHeightPixels"/> + <see cref="HeadroomMarginPixels"/> under a
    /// solid ceiling, wherever the two overlap by at least a hero's width.
    ///
    /// <para>"Can get onto" is the wedge: a <b>one-way</b> surface is landed on
    /// from below by any hero whose feet rise to within its
    /// <see cref="OneWayLandingMarginPixels"/> collision margin before its head
    /// meets the ceiling, so a one-way with headroom from 52 to 68 px pushes that
    /// hero up into the ceiling and wedges it — Level 4's top tower step (5200/380)
    /// sat 62 px under the walkway's underside. A <b>solid</b> surface is only
    /// entered from the side, which the ceiling itself blocks below 64 px, so a
    /// lower solid pocket is unreachable rather than a trap (the Level 1 gear
    /// platforms under the print-shop shelves, stacked ledges with a few pixels
    /// between them); from 64 px up it must clear the margin. A ceiling resting on
    /// the surface (a column on its gate, a wall on its floor) is contiguous, not a
    /// crawlspace.</para>
    /// </summary>
    [TestCase]
    public void NoStandableSurfaceIsWedgedUnderASolidCeiling() {
        var issues = new List<string>();
        int oneWaysChecked = 0;
        foreach (LevelSnapshot level in Snapshots()) {
            if (level.LoadError != null) { issues.Add($"{level.Label}: {level.LoadError}"); continue; }
            foreach (Rect2 surface in level.Standables) {
                bool oneWay = level.OneWays.Contains(surface);
                if (oneWay) oneWaysChecked++;
                float enterable = oneWay ? HeroHeightPixels - OneWayLandingMarginPixels : HeroHeightPixels;
                foreach (Rect2 ceiling in level.Solids) {
                    if (ceiling == surface) continue;
                    float overlap = Mathf.Min(surface.End.X, ceiling.End.X) - Mathf.Max(surface.Position.X, ceiling.Position.X);
                    if (overlap < HeroWidthPixels) continue;
                    float headroom = surface.Position.Y - ceiling.End.Y;
                    if (headroom < enterable || headroom >= HeroHeightPixels + HeadroomMarginPixels) continue;
                    issues.Add($"{level.Label}: the {(oneWay ? "one-way" : "solid")} surface {surface} has " +
                        $"{headroom:0} px under the solid {ceiling} (a {HeroHeightPixels:0} px hero who can get " +
                        $"there needs {HeroHeightPixels + HeadroomMarginPixels:0})");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        AssertThat(oneWaysChecked).IsGreater(0);
    }

    /// <summary>
    /// G5 — the Bastille tower still climbs for every hero: the top step stands
    /// west of the walkway (never under it), and both the hop onto it and the hop
    /// from it onto the walkway are within the roster's weakest single jump
    /// (Lincoln's).
    /// </summary>
    [TestCase]
    public void TheParisTowerTopStepClearsTheWalkwayWithinTheWeakestSingleJump() {
        LevelSnapshot paris = Snapshots().Find(level => level.Index == (int)CampaignLevel.Paris);
        AssertObject(paris).IsNotNull();
        AssertString(paris.LoadError ?? "").IsEqual("");
        Rect2? step = StandableAt(paris, new Vector2(Level04Controller.TowerTopStepX, Level04Controller.TowerTopStepY));
        Rect2? below = StandableAt(paris, new Vector2(4950f, 480f));
        Rect2? walkway = StandableAt(paris, new Vector2(5540f, 300f));
        AssertThat(step.HasValue && below.HasValue && walkway.HasValue)
            .OverrideFailureMessage("The Paris tower's top step, the step below it or the walkway is missing.")
            .IsTrue();
        float single = WeakestSingleJumpPixels();
        AssertFloat(step.Value.End.X).IsLessEqual(walkway.Value.Position.X);
        AssertFloat(below.Value.Position.Y - step.Value.Position.Y).IsLessEqual(single);
        AssertFloat(step.Value.Position.Y - walkway.Value.Position.Y).IsLessEqual(single);
    }

    // === P1: arenas ========================================================

    [TestCase]
    public void EveryCampaignBossWaitsDormantInsideAnArenaThatFitsItsRangedBand() {
        var issues = new List<string>();
        foreach (LevelSnapshot level in Snapshots()) {
            if (level.LoadError != null) { issues.Add($"{level.Label}: {level.LoadError}"); continue; }
            if (level.Index == (int)CampaignLevel.ChronalVoid) continue; // the Mirror, below
            if (level.Encounters.Count == 0) { issues.Add($"{level.Label}: no boss encounter"); continue; }
            foreach (EncounterSnapshot encounter in level.Encounters) {
                Rect2 arena = encounter.Arena;
                if (arena.Size.X <= 0f || arena.Size.Y <= 0f) { issues.Add($"{level.Label}: '{encounter.Name}' has no arena"); continue; }
                if (!arena.HasPoint(encounter.Spawn)) issues.Add($"{level.Label}: the boss spawns at {encounter.Spawn}, outside {arena}");
                if (arena.Size.X <= encounter.RangedBandPixels) {
                    issues.Add($"{level.Label}: the {arena.Size.X} px arena does not fit the {encounter.RangedBandPixels} px ranged band");
                }
                if (encounter.Members.Count == 0) issues.Add($"{level.Label}: '{encounter.Name}' spawned no body");
                foreach ((bool engaged, Rect2 leash, float x) in encounter.Members) {
                    if (engaged) issues.Add($"{level.Label}: a body of '{encounter.Name}' is engaged at load");
                    if (leash != arena) issues.Add($"{level.Label}: a body of '{encounter.Name}' is leashed to {leash}, not {arena}");
                    if (x < arena.Position.X || x > arena.End.X) issues.Add($"{level.Label}: a body stands at x {x}, outside its leash");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// A4 — one canonical arena edge. Where a level's arena starts a fixed
    /// distance in front of its boss (ARENA-WEST-EDGE-AT-OLD-REVEAL-LINE), the
    /// level declares <c>BossSpawnX</c> and <c>BossArenaApproachPixels</c>, builds
    /// the encounter at that spawn, and derives the arena's west edge from the
    /// two — the encounter has no reveal radius of its own any more. A spawn edit
    /// therefore moves the edge with it instead of silently diverging.
    /// </summary>
    [TestCase]
    public void EveryDerivedArenaEdgeStandsItsApproachInFrontOfTheSpawnTheEncounterUses() {
        var issues = new List<string>();
        int derived = 0;
        foreach (LevelSnapshot level in Snapshots()) {
            if (level.LoadError != null) { issues.Add($"{level.Label}: {level.LoadError}"); continue; }
            if (!level.BossSpawnConstX.HasValue) continue;
            derived++;
            if (!level.BossArenaApproachConst.HasValue) {
                issues.Add($"{level.Label}: BossSpawnX without BossArenaApproachPixels");
                continue;
            }
            float spawnX = level.BossSpawnConstX.Value;
            float west = spawnX - level.BossArenaApproachConst.Value;
            if (level.Encounters.Count == 0) issues.Add($"{level.Label}: no boss encounter");
            foreach (EncounterSnapshot encounter in level.Encounters) {
                if (Mathf.Abs(encounter.Spawn.X - spawnX) > 0.5f) {
                    issues.Add($"{level.Label}: '{encounter.Name}' spawns at x {encounter.Spawn.X}, not BossSpawnX {spawnX}");
                }
                if (Mathf.Abs(encounter.Arena.Position.X - west) > 0.5f) {
                    issues.Add($"{level.Label}: the arena starts at {encounter.Arena.Position.X}, not BossSpawnX - " +
                        $"BossArenaApproachPixels = {west}");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        // Levels 2, 3, 5–12, 14 and 15 (Florence's edge is its barricade face,
        // Paris's the courtyard entry, and the Mirror's a fixed rect).
        AssertThat(derived).IsEqual(12);
    }

    /// <summary>
    /// The reveal fires on arena entry, so every arena has to be enterable: at its
    /// west threshold no solid may fill the whole hero-height clearance above the
    /// arena floor while standing taller than the roster's weakest double jump
    /// (a step, a ramp or a low ledge is fine; a wall is not). Florence's west
    /// wall used to run the full height over the floor, so its boss could never
    /// be reached (before P1 it was fought through the wall from the radius
    /// reveal).
    /// </summary>
    [TestCase]
    public void EveryBossArenaCanBeEnteredOverItsWestThreshold() {
        const float heroHeight = 64f;
        float hop = WeakestJumpReachPixels();
        var issues = new List<string>();
        foreach (LevelSnapshot level in Snapshots()) {
            if (level.LoadError != null) { issues.Add($"{level.Label}: {level.LoadError}"); continue; }
            var arenas = new List<(string Name, Rect2 Arena)>();
            foreach (EncounterSnapshot encounter in level.Encounters) arenas.Add((encounter.Name, encounter.Arena));
            if (level.MirrorArena.Size.X > 0f) arenas.Add(("Mirror", level.MirrorArena));
            foreach ((string name, Rect2 arena) in arenas) {
                if (arena.Size.X <= 0f) continue;
                float insideX = arena.Position.X + 30f;
                float ground = float.NaN;
                foreach (Rect2 solid in level.Solids) {
                    // Floors and platforms are thin; walls and columns are not.
                    if (solid.Size.Y >= 200f || solid.Position.X > insideX || solid.End.X < insideX) continue;
                    if (solid.Position.Y < arena.Position.Y || solid.Position.Y > arena.End.Y) continue;
                    if (float.IsNaN(ground) || solid.Position.Y > ground) ground = solid.Position.Y;
                }
                if (float.IsNaN(ground)) { issues.Add($"{level.Label}: no floor under the '{name}' arena threshold"); continue; }
                float bandLeft = arena.Position.X - 150f;
                // The walk in from a pre-boss breather in the arena's antechamber
                // has to be open as well: Florence's last checkpoint stands west
                // of its arena edge, just inside the arena wall's doorway.
                foreach (Vector2 anchor in level.PreBossAnchors) {
                    float approach = arena.Position.X - anchor.X;
                    if (approach > 0f && approach <= AnchorApproachPixels) {
                        bandLeft = Mathf.Min(bandLeft, anchor.X - 150f);
                    }
                }
                float bandRight = arena.Position.X + 30f;
                float bandTop = ground - heroHeight;
                float bandBottom = ground - 2f;
                foreach (Rect2 solid in level.Solids) {
                    if (solid.End.X <= bandLeft || solid.Position.X >= bandRight) continue;
                    bool fillsClearance = solid.Position.Y <= bandTop && solid.End.Y >= bandBottom;
                    if (fillsClearance && ground - solid.Position.Y > hop) {
                        issues.Add($"{level.Label}: {solid} walls off the '{name}' arena threshold " +
                            $"(x {bandLeft}..{bandRight}) from the floor at {ground}; its top is " +
                            $"{ground - solid.Position.Y:0} px up and the weakest double jump rises {hop:0}");
                    }
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void ThePreBossBreathersAndTheirBeatsStandOutsideTheArenaReveal() {
        var issues = new List<string>();
        int florenceAnchors = 0;
        foreach (LevelSnapshot level in Snapshots()) {
            if (level.LoadError != null) continue;
            if (level.Index == (int)CampaignLevel.Florence) florenceAnchors = level.PreBossAnchors.Count;
            var arenas = new List<Rect2>();
            foreach (EncounterSnapshot encounter in level.Encounters) arenas.Add(encounter.Arena);
            if (level.MirrorArena.Size.X > 0f) arenas.Add(level.MirrorArena);
            foreach (Rect2 arena in arenas) {
                foreach (Vector2 anchor in level.PreBossAnchors) {
                    // A hero anywhere on the checkpoint's prompt — striking it —
                    // must still be outside, or the reveal fires first.
                    var prompt = new Rect2(anchor.X - PreBossAnchorClearancePixels, anchor.Y - 1f,
                        2f * PreBossAnchorClearancePixels, 2f);
                    if (arena.Intersects(prompt, includeBorders: true)) {
                        issues.Add($"{level.Label}: the pre-boss anchor at {anchor} is inside (or within " +
                            $"{PreBossAnchorClearancePixels} px of) the arena {arena}");
                    }
                }
                foreach (Vector2 beat in level.PreRevealBeats) {
                    if (arena.HasPoint(beat)) issues.Add($"{level.Label}: a pre-boss beat trigger at {beat} is inside the arena {arena}");
                }
            }
        }
        // Florence predates checkpoint roles; its boss-room checkpoint is
        // captured by ID, so the check above is not vacuous for Level 1.
        if (florenceAnchors != 1) issues.Add($"Level01: expected the boss-room checkpoint, captured {florenceAnchors}");
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void TheMirrorParadoxIsLeashedToItsArena() {
        LevelSnapshot voidLevel = Snapshots().Find(level => level.Index == (int)CampaignLevel.ChronalVoid);
        AssertObject(voidLevel).IsNotNull();
        AssertString(voidLevel.LoadError ?? "").IsEqual("");
        AssertThat(voidLevel.MirrorArena).IsEqual(Level13Controller.MirrorArenaBounds);
        AssertThat(voidLevel.MirrorLeash).IsEqual(Level13Controller.MirrorArenaBounds);
        AssertThat(voidLevel.MirrorArena.HasPoint(voidLevel.MirrorSpawn)).IsTrue();
    }

    // === G3: moving-platform endpoints stay out of the level geometry ======

    /// <summary>
    /// G3 (2026-10-04): every waypoint a <see cref="PathMovingPlatform"/> stops at
    /// (and every <see cref="TrapdoorPlatform"/> leaf) must either stand clear of
    /// the static geometry or sink into it with its top flush with the solid's
    /// top — a walk-on stop, whose deck reads as part of the floor. What is
    /// refused is the half-sunk stop: the Level 12 spire lift's bottom stop sat
    /// 16 px inside the regolith with its top 16 px proud of it (deck top 1884 in
    /// a floor whose top is 1900), a step that is neither floor nor deck.
    /// Descending onto a hero standing in its footprint, that deck drove the hero
    /// into the floor until the floor's depenetration flipped downward and the
    /// hero fell out of the world; the crush itself is now refused by
    /// <see cref="PathMovingPlatform.IsHeldByBodyBelow"/> for every stop shape
    /// (<c>LiftDescentPhysicsTests</c> plays a flush stop and a resting one), so
    /// the flush walk-on stops (the Level 12 spire lift and the Level 14 cargo
    /// lift) keep their no-step boarding.
    /// </summary>
    [TestCase]
    public void NoMovingPlatformEndpointOverlapsStaticGeometry() {
        var issues = new List<string>();
        int endpointsSeen = 0;
        foreach (LevelSnapshot level in Snapshots()) {
            if (level.LoadError != null) { issues.Add($"{level.Label}: {level.LoadError}"); continue; }
            foreach ((string mover, Rect2 endpoint) in level.MoverEndpoints) {
                endpointsSeen++;
                foreach (Rect2 solid in level.StaticGeometry) {
                    Rect2 overlap = endpoint.Intersection(solid);
                    if (IsFlushWalkOnStop(endpoint, solid)) continue;
                    if (overlap.Size.X > 0.5f && overlap.Size.Y > 0.5f) {
                        issues.Add($"{level.Label}: '{mover}' stops at {endpoint}, {overlap.Size.Y:0} px " +
                            $"inside the static {solid}");
                    }
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        // L7's three skiffs, L12's four platforms, L13's four shards, L14's lift
        // (two stops each) and L10's five trapdoors.
        AssertThat(endpointsSeen).IsGreaterEqual(29);
    }

    /// <summary>
    /// A stop sunk into a solid with its top on the solid's top (within half a
    /// pixel) and no deeper than the solid: the deck is level with the floor
    /// around it, so the hero walks on and off with no step.
    /// </summary>
    private static bool IsFlushWalkOnStop(Rect2 endpoint, Rect2 solid) =>
        Mathf.Abs(endpoint.Position.Y - solid.Position.Y) <= 0.5f
        && endpoint.End.Y <= solid.End.Y + 0.5f;

    [TestCase]
    public void TheEndpointContractRefusesAHalfSunkStopAndAllowsAFlushOrClearOne() {
        // The floor of a 40 px graybox floor at y 1900, and the 240 x 32 template deck.
        var floor = new Rect2(6200f, 1900f, 2800f, 40f);
        static Rect2 Deck(float centreY) => new(6930f, centreY - 16f, 240f, 32f);
        // The shipped half-sunk spire stop (centre 1900: top 16 px proud, 16 px inside).
        AssertThat(IsFlushWalkOnStop(Deck(1900f), floor)).IsFalse();
        AssertThat(Deck(1900f).Intersection(floor).Size.Y).IsGreater(0.5f);
        // A flush walk-on stop (centre 1916: top on the floor's top).
        AssertThat(IsFlushWalkOnStop(Deck(1916f), floor)).IsTrue();
        // A deck resting on the floor (centre 1884) does not overlap it at all.
        AssertThat(Deck(1884f).Intersection(floor).Size.Y).IsLessEqual(0.5f);
        // A deck flush on top but deeper than a thin solid is not a walk-on stop.
        AssertThat(IsFlushWalkOnStop(Deck(1916f), new Rect2(6200f, 1900f, 2800f, 20f))).IsFalse();
    }

    // === G6: an Extractor never leaves an unusable strip of deck ===========

    /// <summary>
    /// G6 (2026-10-04): an Extractor standing on (or sunk into) a deck must leave
    /// either nothing or at least a hero's width of that deck on each side. The
    /// Level 9 watchtower machine was sunk through the upper walkway's west end and
    /// left a 14 px strip exactly where the tower climb arrives, walling the walk
    /// off (the radar-mast machine did the same to its gantry with 34 px). A
    /// pedestal — a perch no wider than the machine plus a hero on each side, built
    /// to hold it — is exempt: there the narrow rims are the design.
    /// </summary>
    [TestCase]
    public void NoExtractorLeavesASliverOfTheDeckItStandsOn() {
        const float heroWidth = 40f;
        var issues = new List<string>();
        int extractorsOnDecks = 0;
        foreach (LevelSnapshot level in Snapshots()) {
            if (level.LoadError != null) { issues.Add($"{level.Label}: {level.LoadError}"); continue; }
            foreach ((string id, Rect2 body) in level.Extractors) {
                foreach (Rect2 deck in level.Standables) {
                    if (deck.Encloses(body) && body.Encloses(deck)) continue; // the extractor itself
                    if (deck.Size.Y >= 200f) continue;                        // a wall, not a deck
                    float overlap = Mathf.Min(deck.End.X, body.End.X) - Mathf.Max(deck.Position.X, body.Position.X);
                    if (overlap <= 0.5f) continue;
                    // Standing on it: the deck's top lies within the machine's height.
                    if (deck.Position.Y <= body.Position.Y || deck.Position.Y > body.End.Y + 0.5f) continue;
                    extractorsOnDecks++;
                    if (deck.Size.X < body.Size.X + 2f * heroWidth) continue; // a pedestal
                    float west = body.Position.X - deck.Position.X;
                    float east = deck.End.X - body.End.X;
                    foreach ((string side, float strip) in new[] { ("west", west), ("east", east) }) {
                        if (strip > 0.5f && strip < heroWidth) {
                            issues.Add($"{level.Label}: '{id}' {body} leaves a {strip:0} px {side} strip of " +
                                $"the deck {deck}");
                        }
                    }
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        AssertThat(extractorsOnDecks).IsGreater(10);
    }

    // === The single capture pass ===========================================

    private sealed class EncounterSnapshot {
        public string Name;
        public Rect2 Arena;
        public Vector2 Spawn;
        public float RangedBandPixels;
        public readonly List<(bool Engaged, Rect2 Leash, float X)> Members = new();
    }

    private sealed class LevelSnapshot {
        public string Label;
        public int Index;
        public string LoadError;
        public float LowestGravityScale = 1f;
        public readonly List<Rect2> GateColumns = new();
        /// <summary>Every sealed door and forcefield barrier, by node name (A1).</summary>
        public readonly List<(string Name, Rect2 Rect)> Gates = new();
        /// <summary>The controller's public <c>BossSpawnX</c> / <c>BossArenaApproachPixels</c> constants, when it has them (A4).</summary>
        public float? BossSpawnConstX;
        public float? BossArenaApproachConst;
        public readonly List<Rect2> Standables = new();
        /// <summary>The standables that also block sideways (one-way platforms excluded).</summary>
        public readonly List<Rect2> Solids = new();
        /// <summary>The one-way platforms among the standables (G5).</summary>
        public readonly List<Rect2> OneWays = new();
        public readonly List<(float Left, float Right)> Floors = new();
        public readonly List<EncounterSnapshot> Encounters = new();
        public readonly List<Vector2> PreBossAnchors = new();
        public readonly List<Vector2> PreRevealBeats = new();
        /// <summary>Static level geometry only: no moving platform, trapdoor or rotating body (G3).</summary>
        public readonly List<Rect2> StaticGeometry = new();
        /// <summary>Every stop of every moving platform, and every trapdoor leaf (G3).</summary>
        public readonly List<(string Mover, Rect2 Endpoint)> MoverEndpoints = new();
        /// <summary>Every Chronal Extractor body (G6).</summary>
        public readonly List<(string ID, Rect2 Body)> Extractors = new();
        public Rect2 MirrorArena;
        public Rect2 MirrorLeash;
        public Vector2 MirrorSpawn;
    }

    private static List<LevelSnapshot> Snapshots() {
        if (_snapshots != null) return _snapshots;
        var snapshots = new List<LevelSnapshot>();
        for (int index = 1; index <= 15; index++) {
            snapshots.Add(Capture($"Level{index:00}", StoryManager.GetLevelScenePath((CampaignLevel)index), index));
        }
        _snapshots = snapshots;
        return snapshots;
    }

    /// <summary>
    /// Builds one authored scene in the runner tree, copies everything the cases
    /// need into plain values, and hands every shared singleton back (session
    /// slot -1 so no save is touched, character, difficulty, pooled enemies, the
    /// pause flag).
    /// </summary>
    private static LevelSnapshot Capture(string label, string scenePath, int index) {
        var snapshot = new LevelSnapshot { Label = label, Index = index };
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        string originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
        Difficulty originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
        Node level = null;
        try {
            GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            GameManager.Instance.CurrentSession.Difficulty = Difficulty.Normal;

            PackedScene scene = ResourceLoader.Load<PackedScene>(scenePath);
            if (scene == null) { snapshot.LoadError = $"scene '{scenePath}' did not load"; return snapshot; }
            level = scene.Instantiate();
            level.Name = $"{label}_ArenaGeometryTest";
            tree.Root.AddChild(level);
            snapshot.BossSpawnConstX = PublicConstant(level.GetType(), "BossSpawnX");
            snapshot.BossArenaApproachConst = PublicConstant(level.GetType(), "BossArenaApproachPixels");

            if (level is StoryLevelControllerBase story) {
                foreach (StaticBody2D column in story.GateColumns) {
                    if (GodotObject.IsInstanceValid(column)) AddShapes(column, snapshot.GateColumns);
                }
                if (story.AbsenceTrigger != null && GodotObject.IsInstanceValid(story.AbsenceTrigger)) {
                    snapshot.PreRevealBeats.Add(story.AbsenceTrigger.GlobalPosition);
                }
            } else if (level is Level01Controller florence && florence.WorkshopDoorColumn != null) {
                AddShapes(florence.WorkshopDoorColumn, snapshot.GateColumns);
            }
            Collect(level, snapshot);
        } catch (Exception exception) {
            snapshot.LoadError = $"capture threw {exception.GetType().Name}: {exception.Message}";
        } finally {
            PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
            if (level != null && GodotObject.IsInstanceValid(level)) {
                level.GetParent()?.RemoveChild(level);
                level.Free();
            }
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = originalCharacter;
            GameManager.Instance.CurrentSession.Difficulty = originalDifficulty;
            tree.Paused = originalPaused;
        }
        return snapshot;
    }

    private static void Collect(Node node, LevelSnapshot snapshot) {
        switch (node) {
            case BossEncounterController boss:
                var encounter = new EncounterSnapshot {
                    Name = boss.Name,
                    Arena = boss.ArenaBounds,
                    Spawn = boss.GlobalPosition + boss.SpawnOffset,
                    RangedBandPixels = (boss.Data?.RangedRangeThreshold ?? 0f) * 60f
                };
                foreach (BossController body in boss.Members) {
                    if (body != null && GodotObject.IsInstanceValid(body)) {
                        encounter.Members.Add((body.IsEngaged, body.ArenaBounds, body.GlobalPosition.X));
                    }
                }
                snapshot.Encounters.Add(encounter);
                break;
            case MirrorParadoxEncounterController mirror:
                snapshot.MirrorArena = mirror.ArenaBounds;
                snapshot.MirrorLeash = mirror.Mirror?.ArenaBounds ?? default;
                snapshot.MirrorSpawn = mirror.GlobalPosition + mirror.SpawnOffset;
                break;
            // Florence's hand-built checkpoints carry no role; its boss-room
            // checkpoint is the one the reveal must not overtake.
            case CheckpointTrigger checkpoint when checkpoint.Role == CheckpointRole.PreBoss
                || checkpoint.CheckpointID == Level01Controller.BossRoomCheckpointID:
                snapshot.PreBossAnchors.Add(checkpoint.GlobalPosition);
                break;
            case GravityFieldZone field:
                snapshot.LowestGravityScale = Mathf.Min(snapshot.LowestGravityScale, Mathf.Max(0.05f, field.GravityScale));
                if (field.CycleScales != null) {
                    foreach (float scale in field.CycleScales) {
                        snapshot.LowestGravityScale = Mathf.Min(snapshot.LowestGravityScale, Mathf.Max(0.05f, scale));
                    }
                }
                break;
            case Area2D area when area.Name == "PreBossTrigger":
                snapshot.PreRevealBeats.Add(area.GlobalPosition);
                break;
            // A1: the gates themselves, from the build, never a hand list.
            case StaticBody2D gate when gate is ForcefieldBarrier || gate.IsInGroup(StoryGateRules.DoorGroup):
                var gateRects = new List<Rect2>();
                AddShapes(gate, gateRects);
                foreach (Rect2 rect in gateRects) snapshot.Gates.Add((gate.Name.ToString(), rect));
                break;
            case PathMovingPlatform mover when mover.Waypoints is { Length: > 0 }:
                // Captured synchronously after _Ready, so no tick has moved it yet;
                // each stop is the authored origin plus the waypoint offset.
                Node2D moverParent = mover.GetParent() as Node2D;
                foreach (Vector2 waypoint in mover.Waypoints) {
                    Vector2 stop = moverParent != null
                        ? moverParent.GlobalTransform * (mover.AuthoredOrigin + waypoint)
                        : mover.AuthoredOrigin + waypoint;
                    var rects = new List<Rect2>();
                    AddShapes(mover, rects);
                    foreach (Rect2 rect in rects) {
                        snapshot.MoverEndpoints.Add((mover.PlatformID,
                            new Rect2(rect.Position + (stop - mover.GlobalPosition), rect.Size)));
                    }
                }
                break;
            case TrapdoorPlatform trapdoor:
                var leaves = new List<Rect2>();
                AddShapes(trapdoor, leaves, includeDisabled: true);
                foreach (Rect2 leaf in leaves) snapshot.MoverEndpoints.Add((trapdoor.TrapdoorID, leaf));
                break;
            case ChronalExtractor extractor:
                var bodies = new List<Rect2>();
                AddShapes(extractor, bodies);
                foreach (Rect2 extractorBody in bodies) snapshot.Extractors.Add((extractor.ObjectID, extractorBody));
                break;
        }

        bool oneWay = node is OneWayPlatform;
        bool solid = !oneWay && node is PhysicsBody2D body2D && (body2D.CollisionLayer & CollisionLayers.Environment) != 0;
        if (oneWay || (solid && node is StaticBody2D and not AnimatableBody2D)) {
            AddShapes((CollisionObject2D)node, snapshot.StaticGeometry);
        }
        if (oneWay || solid) {
            int before = snapshot.Standables.Count;
            AddShapes((CollisionObject2D)node, snapshot.Standables);
            for (int index = before; index < snapshot.Standables.Count; index++) {
                (solid ? snapshot.Solids : snapshot.OneWays).Add(snapshot.Standables[index]);
            }
            if (solid && node.Name.ToString().StartsWith("Floor_", StringComparison.Ordinal)) {
                for (int index = before; index < snapshot.Standables.Count; index++) {
                    Rect2 floor = snapshot.Standables[index];
                    snapshot.Floors.Add((floor.Position.X, floor.End.X));
                }
            }
        }

        Godot.Collections.Array<Node> children = node.GetChildren();
        using var childrenLifetime = children.AsDisposable();
        foreach (Node child in children) Collect(child, snapshot);
    }

    /// <summary>True when <paramref name="column"/> spans the whole gate with its foot at the gate's top.</summary>
    private static bool CoversGate(Rect2 column, Rect2 gate) =>
        column.Position.X <= gate.Position.X + 0.5f
        && column.End.X >= gate.End.X - 0.5f
        && Mathf.Abs(column.End.Y - gate.Position.Y) <= GateFootTolerance;

    /// <summary>
    /// Every standable surface within <see cref="MaxApproachPixels"/> of
    /// <paramref name="column"/> from which a hero's <paramref name="reach"/>
    /// rises past the column's top.
    /// </summary>
    private static void AddReachIssues(LevelSnapshot level, Rect2 column, float reach, List<string> issues) {
        foreach (Rect2 solid in level.Standables) {
            // No column top (y −2400) is ever stood on: it is out of every
            // reach by this very test, so it is no launch point either.
            if (level.GateColumns.Contains(solid)) continue;
            float gap = Mathf.Max(0f, Mathf.Max(solid.Position.X - column.End.X, column.Position.X - solid.End.X));
            if (gap > MaxApproachPixels) continue;
            float apex = solid.Position.Y - reach;
            if (column.Position.Y >= apex) {
                issues.Add($"{level.Label}: the gate column {column} tops out at {column.Position.Y}, " +
                    $"but a hero on {solid} reaches {apex:0}");
            }
        }
    }

    /// <summary>The standable whose top edge is the surface at <paramref name="point"/> (its centre line), if any.</summary>
    private static Rect2? StandableAt(LevelSnapshot level, Vector2 point) {
        foreach (Rect2 surface in level.Standables) {
            if (point.X >= surface.Position.X && point.X <= surface.End.X
                && point.Y >= surface.Position.Y - 0.5f && point.Y <= surface.End.Y + 0.5f) {
                return surface;
            }
        }
        return null;
    }

    /// <summary>A public constant (or static field) of a level controller, as a float, or null.</summary>
    private static float? PublicConstant(Type type, string name) {
        FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        if (field == null) return null;
        object value = field.IsLiteral ? field.GetRawConstantValue() : field.GetValue(null);
        return value is float number ? number : null;
    }

    /// <summary>Every enabled rectangle shape of a body, as a global axis-aligned box.</summary>
    private static void AddShapes(CollisionObject2D body, List<Rect2> into, bool includeDisabled = false) {
        Godot.Collections.Array<Node> shapes = body.GetChildren();
        using var shapesLifetime = shapes.AsDisposable();
        foreach (Node child in shapes) {
            if (child is not CollisionShape2D { Shape: RectangleShape2D rect } shape) continue;
            if (shape.Disabled && !includeDisabled) continue;
            Transform2D transform = shape.GlobalTransform;
            Vector2 half = rect.Size / 2f;
            Vector2 a = transform * new Vector2(-half.X, -half.Y);
            Vector2 b = transform * new Vector2(half.X, -half.Y);
            Vector2 c = transform * new Vector2(half.X, half.Y);
            Vector2 d = transform * new Vector2(-half.X, half.Y);
            float minX = Mathf.Min(Mathf.Min(a.X, b.X), Mathf.Min(c.X, d.X));
            float maxX = Mathf.Max(Mathf.Max(a.X, b.X), Mathf.Max(c.X, d.X));
            float minY = Mathf.Min(Mathf.Min(a.Y, b.Y), Mathf.Min(c.Y, d.Y));
            float maxY = Mathf.Max(Mathf.Max(a.Y, b.Y), Mathf.Max(c.Y, d.Y));
            into.Add(new Rect2(minX, minY, maxX - minX, maxY - minY));
        }
    }

    /// <summary>
    /// The roster's best multi-jump rise at <paramref name="gravityScale"/>, with
    /// headroom, plus one movement-ability relocation. PlayerController's closed
    /// form (the SecretCachePlacementTests / Level 12 form): launch =
    /// MaxJumpForce × 54 px/s, gravity = 18 × (0.8 + 0.4 × Weight) × 60 px/s²,
    /// rise = v² / 2a, maximised over the manifest roster.
    /// </summary>
    private static float ReachPixels(float gravityScale) {
        float best = 0f;
        foreach (string id in CharacterRoster.IDs) {
            var data = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{id}_data.tres");
            float launch = data.MaxJumpForce * 54f;
            float acceleration = 18f * (0.8f + 0.4f * data.Weight) * 60f * Mathf.Max(0.05f, gravityScale);
            float rise = launch * launch / (2f * acceleration);
            best = Mathf.Max(best, rise * Mathf.Max(1, data.MaxJumpCount));
        }
        return best * JumpHeadroom + MovementAllowancePixels;
    }

    /// <summary>
    /// The roster's weakest full multi-jump rise at normal gravity, with no
    /// headroom and no movement ability (Level 1 has none yet): what every hero
    /// is guaranteed to clear.
    /// </summary>
    private static float WeakestJumpReachPixels() {
        float worst = float.MaxValue;
        foreach (string id in CharacterRoster.IDs) {
            var data = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{id}_data.tres");
            float launch = data.MaxJumpForce * 54f;
            float acceleration = 18f * (0.8f + 0.4f * data.Weight) * 60f;
            worst = Mathf.Min(worst, launch * launch / (2f * acceleration) * Mathf.Max(1, data.MaxJumpCount));
        }
        return worst;
    }

    /// <summary>The roster's weakest full single-jump rise at normal gravity (Lincoln's), no headroom.</summary>
    private static float WeakestSingleJumpPixels() {
        float worst = float.MaxValue;
        foreach (string id in CharacterRoster.IDs) {
            var data = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{id}_data.tres");
            float launch = data.MaxJumpForce * 54f;
            float acceleration = 18f * (0.8f + 0.4f * data.Weight) * 60f;
            worst = Mathf.Min(worst, launch * launch / (2f * acceleration));
        }
        return worst;
    }
}
