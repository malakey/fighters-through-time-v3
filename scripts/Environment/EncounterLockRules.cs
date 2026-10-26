using System;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>When a lock wave spawns (gameplay update 2026-10-12, GU-E, plan §2.3).</summary>
    public enum EncounterWaveTrigger {
        /// <summary>Pre-placed: spawned dormant when the lock arms at level build.</summary>
        OnBuild = 0,
        /// <summary>Spawned the first time the lock engages (and, after a revert, on the next engage).</summary>
        OnEngage = 1,
        /// <summary>Spawned while engaged once at most <c>AliveAtMost</c> registered members are still alive.</summary>
        WhenAliveAtMost = 2,
        /// <summary>Spawned while engaged once every member spawned so far is dead or removed.</summary>
        AfterPreviousCleared = 3
    }

    /// <summary>
    /// The one statement of the Story <b>encounter lock</b> (user request 3 of
    /// 2026-10-12, ledger <c>USER-ENCOUNTER-LOCKS</c>; the agent-chosen numbers are
    /// <c>PROVISIONAL-ENCOUNTER-LOCK-RULES</c>): an authored area the hero must
    /// clear of enemies (and, for a puzzle room, solve) before the way forward
    /// opens. "You no longer have the option of jumping past them."
    ///
    /// <para>Pure and engine-free on purpose — floats, ints, bools and the
    /// <see cref="EncounterLockState"/> / <see cref="Difficulty"/> enums only, no
    /// Godot type and no <c>GameManager</c> read — so <c>EncounterLockRulesTests</c>
    /// pins it with no Godot runtime (CLAUDE.md failure signature 7). The engine
    /// half is <see cref="EncounterLock"/> (the node), <see cref="EncounterLockBarrier"/>
    /// and the member hooks in <c>EnemyController.Encounter.cs</c>.</para>
    ///
    /// <para>Story only: nothing under <c>scripts/FighterSim/</c> may name it.</para>
    /// </summary>
    public static class EncounterLockRules {

        // === Engagement ======================================================

        /// <summary>
        /// The hero engages a lock only once its body is this far inside the
        /// area horizontally, beyond its own half-width (plan §2.3), so a hero
        /// brushing the entry edge does not shut the door behind itself.
        /// </summary>
        public const float EngageInsetPixels = 24f;

        // === Members =========================================================

        /// <summary>
        /// A member whose feet end a frame this far below its area's bottom edge
        /// has left the world and is returned to its authored spawn point. The
        /// same four-unit margin the boss arena net uses
        /// (<c>BossController.ArenaFallNetMarginPixels</c>), stated once there.
        /// </summary>
        public const float FallNetMarginPixels = FTT.Enemies.BossController.ArenaFallNetMarginPixels;

        /// <summary>
        /// <i>Provisional.</i> A member that stays stalled — off any surface, or
        /// standing still where the hero cannot reach it — for this many of the
        /// lock's own unfrozen engaged ticks (10 s) is relocated to its spawn
        /// anchor. Never killed: a kill would pay its dust for nothing.
        /// </summary>
        public const int StallFrames = 600;

        /// <summary>
        /// <i>Provisional.</i> Movement under this many pixels in one tick counts as
        /// standing still for the stall watchdog.
        /// </summary>
        public const float StallMovePixels = 4f;

        /// <summary>
        /// <i>Provisional.</i> A ground member whose feet are more than this far
        /// above or below the hero's counts as unreachable for the stall watchdog:
        /// ground mobs never jump, and this is about the slowest hero's double jump.
        /// </summary>
        public const float StallVerticalGapPixels = 200f;

        /// <summary>
        /// <i>Provisional</i> (FIX-ENEMY, 2026-10-13). The stall watchdog sends a
        /// stalled member back to its anchor only when it stands farther than this
        /// from it: a member already on its anchor (or within float noise and a
        /// step of it) is stalled on that spot, and returning it there loops.
        /// </summary>
        public const float StallAnchorTolerancePixels = 16f;

        /// <summary>
        /// <i>Provisional</i> (FIX-ENEMY, 2026-10-13). How far above the hero's
        /// feet an engaged flying member's feet — the bottom of its hurtbox on
        /// every roster body — may be held in any state but an attack or a stun.
        /// Every hero's single jump with its air strike reaches past this line (a
        /// double jump far past it), so a flyer can never hover out of reach and
        /// hold its lock shut. Pinned against the nine heroes' integrated arcs and
        /// air-strike boxes by <c>EncounterMemberReachTests</c>. Story only.
        /// </summary>
        public const float FlyerHoverCapPixels = 160f;

        /// <summary>
        /// <i>Provisional.</i> A lock engaged for longer than this with no living
        /// registered member left, and every required puzzle and breakable done,
        /// force-clears and logs: the backstop for a wave that can never spawn.
        /// It never bypasses a puzzle or a breakable the hero can still finish.
        /// </summary>
        public const float HardCapSeconds = 300f;

        // === Barriers ========================================================

        /// <summary><i>Provisional.</i> A lock barrier's default width: a wall, narrower than the 40 px hero.</summary>
        public const float DefaultBarrierWidthPixels = 20f;

        /// <summary>
        /// A barrier's inner face stands at most this far outside its area's edge,
        /// so the hero's pivot can never be outside the area while it is between
        /// the barrier and the area (the 40 px hero's half-width).
        /// </summary>
        public const float BarrierEdgeTolerancePixels = 20f;

        // === Camera and presentation =========================================

        /// <summary>
        /// The visible Story view in world pixels at the Story camera zoom
        /// (<see cref="StoryCameraRules.ViewZoom"/>: 1920 / 1.5 x 1080 / 1.5 = 1280 x 720).
        /// A lock's camera rect is never narrower than this, and its area is never
        /// narrower than the view's width. Compile-time constants derived from the
        /// camera's own, so the zoom has one canonical value and this rulebook stays
        /// engine-free.
        /// </summary>
        public const float ViewWorldWidth = StoryCameraRules.ReferenceViewportWidth / StoryCameraRules.ViewZoom;

        /// <summary>See <see cref="ViewWorldWidth"/>.</summary>
        public const float ViewWorldHeight = StoryCameraRules.ReferenceViewportHeight / StoryCameraRules.ViewZoom;

        // === Content rules (EncounterLockContentTests) =======================

        /// <summary>An area is never narrower than the visible view (plan §2.3).</summary>
        public const float MinAreaWidthPixels = ViewWorldWidth;

        /// <summary>
        /// <i>Provisional.</i> An area is at most one authored camera room wide
        /// (3200 px). The plan's rule said "about 2600 px", but its own Level 2
        /// worked example names 2900 px and 3070 px rooms; one room is the cap.
        /// </summary>
        public const float MaxAreaWidthPixels = 3200f;

        // === Rules ===========================================================

        /// <summary>
        /// True when a hero whose pivot is at (<paramref name="heroX"/>,
        /// <paramref name="heroFeetY"/>) stands inside an area spanning
        /// [<paramref name="minX"/>, <paramref name="maxX"/>] x
        /// [<paramref name="minY"/>, <paramref name="maxY"/>], edges included. The
        /// containment an <see cref="EncounterLockState.Engaged"/> lock polls every
        /// physics tick.
        /// </summary>
        public static bool IsInsideArea(float heroX, float heroFeetY, float minX, float maxX, float minY, float maxY) =>
            heroX >= minX && heroX <= maxX && heroFeetY >= minY && heroFeetY <= maxY;

        /// <summary>
        /// The engage rule: an Armed lock engages when the living hero's body is
        /// at least its half-width plus <see cref="EngageInsetPixels"/> inside the
        /// area horizontally and inside it vertically, and nothing holds the world
        /// (no Time Freeze, no recovery hold or boss suspension, no
        /// gameplay-pausing dialogue).
        /// </summary>
        public static bool CanEngage(
            float heroX, float heroHalfWidth, float heroFeetY,
            float minX, float maxX, float minY, float maxY,
            bool heroAlive, bool timeFrozen, bool worldHeld, bool gameplayPaused) {
            if (!heroAlive || timeFrozen || worldHeld || gameplayPaused) return false;
            float inset = Math.Max(0f, heroHalfWidth) + EngageInsetPixels;
            if (maxX - minX <= 2f * inset) return false;
            return heroX >= minX + inset && heroX <= maxX - inset
                && heroFeetY >= minY && heroFeetY <= maxY;
        }

        /// <summary>
        /// Revert-to-Armed: an Engaged lock whose living hero ends a tick outside
        /// its area (a death-rewind landing, a rift snap, a scripted
        /// move) goes back to Armed. A dead hero is not judged — the rewind
        /// playback moves it along its path — until it lives again.
        /// </summary>
        public static bool ShouldRevertToArmed(EncounterLockState state, bool heroAlive, bool heroInsideArea) =>
            state == EncounterLockState.Engaged && heroAlive && !heroInsideArea;

        /// <summary>
        /// The clear predicate: every spawned member dead or removed, every wave
        /// spawned, every required puzzle solved (live or persisted) and every
        /// required breakable destroyed. Easy's smaller prefix is already in the
        /// spawned count, so nothing Easy never spawned is waited for.
        /// </summary>
        public static bool IsClearPredicateMet(
            int livingMembers, bool allWavesSpawned, bool puzzlesSolved, bool breakablesDestroyed) =>
            livingMembers <= 0 && allWavesSpawned && puzzlesSolved && breakablesDestroyed;

        /// <summary>
        /// Whether the next unspawned wave (all earlier waves already spawned)
        /// spawns this tick. Nothing spawns while the world is held. OnBuild
        /// waves are the lock's arming, not a tick decision.
        /// </summary>
        public static bool ShouldSpawnWave(
            EncounterWaveTrigger trigger, int aliveAtMost, EncounterLockState state,
            bool earlierWavesSpawned, int livingMembers, bool held) {
            if (held || state != EncounterLockState.Engaged || !earlierWavesSpawned) return false;
            return trigger switch {
                EncounterWaveTrigger.OnBuild => true,
                EncounterWaveTrigger.OnEngage => true,
                EncounterWaveTrigger.WhenAliveAtMost => livingMembers <= Math.Max(0, aliveAtMost),
                EncounterWaveTrigger.AfterPreviousCleared => livingMembers <= 0,
                _ => false
            };
        }

        /// <summary>
        /// How many of a wave's <paramref name="tableLength"/> authored spawns
        /// appear on <paramref name="difficulty"/>: the per-row
        /// <see cref="StoryDifficultyTuning.ScaleEncounterCount"/> prefix of the
        /// <paramref name="authoredCount"/> (the manifest row's <c>@n</c>, or the
        /// table length when it carries none), never more than the table holds.
        /// An unscaled wave (<c>@0</c>) always spawns the whole table.
        /// </summary>
        public static int PlannedSpawnCount(int tableLength, int authoredCount, bool scaled, Difficulty difficulty) {
            int length = Math.Max(0, tableLength);
            if (!scaled) return length;
            int authored = authoredCount > 0 ? authoredCount : length;
            return Math.Min(StoryDifficultyTuning.ScaleEncounterCount(authored, difficulty), length);
        }

        /// <summary>
        /// One axis of a lock's camera rect: the area's span clipped to
        /// [<paramref name="clipMin"/>, <paramref name="clipMax"/>] (the level),
        /// then, when narrower than <paramref name="minimumSize"/> (the visible
        /// view), grown symmetrically about its centre and shifted back inside the
        /// clip where the growth runs off one end. A clip no larger than the
        /// minimum is returned whole.
        /// </summary>
        public static (float Min, float Max) ExpandCameraSpan(
            float min, float max, float minimumSize, float clipMin, float clipMax) {
            if (max < min) (min, max) = (max, min);
            if (clipMax < clipMin) (clipMin, clipMax) = (clipMax, clipMin);
            if (minimumSize >= clipMax - clipMin) return (clipMin, clipMax);
            min = Math.Clamp(min, clipMin, clipMax);
            max = Math.Clamp(max, clipMin, clipMax);
            if (max - min >= minimumSize) return (min, max);
            float centre = (min + max) * 0.5f;
            min = centre - minimumSize * 0.5f;
            max = centre + minimumSize * 0.5f;
            if (min < clipMin) {
                max += clipMin - min;
                min = clipMin;
            }
            if (max > clipMax) {
                min -= max - clipMax;
                max = clipMax;
            }
            return (min, max);
        }

        /// <summary>
        /// The x-clamp a member takes after every move: its pivot kept at least
        /// its half-width inside the area, so no part of its body crosses a
        /// barrier the area is bounded by. A body wider than the area sits on its
        /// centre.
        /// </summary>
        public static float ClampMemberX(float x, float minX, float maxX, float halfWidth) {
            float low = minX + Math.Max(0f, halfWidth);
            float high = maxX - Math.Max(0f, halfWidth);
            if (high < low) return (minX + maxX) * 0.5f;
            return Math.Clamp(x, low, high);
        }

        /// <summary>The y a member's feet must pass for the fall net: the area's bottom plus <see cref="FallNetMarginPixels"/>.</summary>
        public static float FallNetLineY(float areaBottomY) => areaBottomY + FallNetMarginPixels;

        /// <summary>True when a member's feet are below its area's fall net.</summary>
        public static bool IsBelowFallNet(float feetY, float areaBottomY) => feetY > FallNetLineY(areaBottomY);

        /// <summary>
        /// One stall-watchdog sample. A member counts as stalled on a tick when it
        /// is a ground mob off any floor and not in a knockback flight
        /// (<paramref name="inFlight"/>: stunned, thrown, launched), or when it is
        /// a melee chaser that moved less than <see cref="StallMovePixels"/> while
        /// the hero is more than <see cref="StallVerticalGapPixels"/> above or
        /// below it — standing on a post the hero cannot reach, or under one the
        /// hero stands on. A member taking its turn, waiting on the same floor or
        /// attacking is never stalled, and neither is a <paramref name="ranged"/>
        /// mob holding still: shooting from a post is its job.
        /// </summary>
        public static bool IsStalledSample(
            bool flying, bool onFloor, bool inFlight, bool chasing, bool ranged,
            float movedPixels, float verticalGapToHero) {
            if (!flying && !onFloor && !inFlight) return true;
            return chasing && !ranged && movedPixels < StallMovePixels
                && Math.Abs(verticalGapToHero) > StallVerticalGapPixels;
        }

        /// <summary>
        /// FIX-ENEMY (2026-10-13): the flyer's stall sample, beside
        /// <see cref="IsStalledSample"/>. An engaged flyer whose feet stay above
        /// <see cref="FlyerHoverLineY"/> while it moves under
        /// <see cref="StallMovePixels"/>, out of any knockback flight, is stalled
        /// whatever its attack: the settle cannot bring it down (it rests on a
        /// solid platform over the hero — Level 3's drone on the midway's lamp
        /// gantry), so shooting from there is not its job. An attack only pauses
        /// the settle; its frames count, and the settle's own descent resets the
        /// count.
        /// </summary>
        public static bool IsFlyerStalledOutOfReach(bool flying, bool inFlight, float movedPixels, float feetY, float heroFeetY) =>
            flying && !inFlight && movedPixels < StallMovePixels && feetY < FlyerHoverLineY(heroFeetY) - StallMovePixels;

        /// <summary>The stall counter after one sample: it grows while stalled and resets otherwise.</summary>
        public static int NextStallFrames(int current, bool stalled) => stalled ? Math.Max(0, current) + 1 : 0;

        /// <summary>True once the stall counter reaches <see cref="StallFrames"/>: relocate the member.</summary>
        public static bool ShouldRelocate(int stallFrames) => stallFrames >= StallFrames;

        /// <summary>
        /// FIX-ENEMY (2026-10-13): where a stalled member goes. Back to its
        /// anchor, as before, when that is a move — the member stands more than
        /// <see cref="StallAnchorTolerancePixels"/> from it — and the anchor is
        /// not itself a post over the hero (its feet no more than
        /// <see cref="StallVerticalGapPixels"/> above the hero's;
        /// <paramref name="anchorFeetGapToHero"/> is anchor minus hero, negative
        /// up). Otherwise — it is stalled on its own anchor, or the anchor is a
        /// post above the hero (Level 10's gallery tier) — it is put down beside
        /// the hero instead: a watchdog that returns a member to the post it is
        /// stalled on loops forever. An anchor below a hero standing on a ledge
        /// keeps the old return (the hero can come down to it).
        /// </summary>
        public static bool RelocatesBesideHero(float distanceToAnchor, float anchorFeetGapToHero) =>
            distanceToAnchor <= StallAnchorTolerancePixels
            || anchorFeetGapToHero < -StallVerticalGapPixels;

        // === Members' reach (FIX-ENEMY, 2026-10-13) ===========================

        /// <summary>The highest feet line an engaged flyer is held at: <see cref="FlyerHoverCapPixels"/> above the hero's feet.</summary>
        public static float FlyerHoverLineY(float heroFeetY) => heroFeetY - FlyerHoverCapPixels;

        /// <summary>
        /// The flyer settle: the vertical velocity (px/s, positive down) an
        /// engaged flyer whose feet are at <paramref name="feetY"/> takes this
        /// tick. Above <see cref="FlyerHoverLineY"/> it descends toward the line
        /// at up to <paramref name="maxSpeedPixels"/> (its own chase speed) and
        /// lands on it exactly; at or below the line its own
        /// <paramref name="velocityY"/> stands. The cap never lifts a flyer and
        /// never slows a faster descent of its own.
        /// </summary>
        public static float FlyerSettleVelocityY(float feetY, float heroFeetY, float velocityY, float maxSpeedPixels, float dt) {
            float line = FlyerHoverLineY(heroFeetY);
            if (feetY >= line || dt <= 0f) return velocityY;
            float descent = Math.Min(Math.Max(0f, maxSpeedPixels), (line - feetY) / dt);
            return Math.Max(velocityY, descent);
        }

        /// <summary>
        /// The top of a flying member's band (the x-clamp and the fall net bound
        /// the rest): its whole body stays below the top of its lock's camera
        /// rect <paramref name="viewTopY"/>, so its feet never sit higher than
        /// that line plus the body's <paramref name="headHeight"/> (feet to the
        /// top of its body). Whatever carries a flyer up — a throw, a write —
        /// stops at the edge of the frame the lock holds the camera to.
        /// </summary>
        public static float ClampFlyerFeetY(float feetY, float viewTopY, float headHeight) =>
            Math.Max(feetY, viewTopY + Math.Max(0f, headHeight));

        /// <summary>
        /// A melee ground member looks for a way down to a target whose feet are
        /// more than the chase's ledge probe depth
        /// (<see cref="FTT.Combat.StoryEnemyCombatRules.LedgeProbeDepthPixels"/>)
        /// below its own (<paramref name="targetDropPixels"/>, positive down):
        /// a drop it would otherwise never walk off, so it would never come to
        /// the hero.
        /// </summary>
        public static bool SeeksWayDown(float targetDropPixels) =>
            targetDropPixels > FTT.Combat.StoryEnemyCombatRules.LedgeProbeDepthPixels;

        /// <summary>
        /// A step off a ledge whose probe point is <paramref name="stepX"/> lands
        /// inside the area: the body's half-width either side of it stays inside
        /// [<paramref name="minX"/>, <paramref name="maxX"/>].
        /// </summary>
        public static bool StepOffLandsInArea(float stepX, float minX, float maxX, float halfWidth) =>
            stepX - Math.Max(0f, halfWidth) >= minX && stepX + Math.Max(0f, halfWidth) <= maxX;

        /// <summary>
        /// The hard cap: an Engaged lock past <see cref="HardCapSeconds"/> with no
        /// living registered member, whose required puzzles and breakables are
        /// done, force-clears. A pending puzzle or breakable is the hero's to
        /// finish, so the cap never opens it.
        /// </summary>
        public static bool ShouldForceClear(
            EncounterLockState state, double engagedSeconds, int livingMembers,
            bool puzzlesSolved, bool breakablesDestroyed) =>
            state == EncounterLockState.Engaged
            && engagedSeconds > HardCapSeconds
            && livingMembers <= 0
            && puzzlesSolved
            && breakablesDestroyed;

        /// <summary>
        /// The HUD's "Enemies left" count: the living registered members plus
        /// every member a wave has yet to spawn. 0 once Cleared.
        /// </summary>
        public static int RemainingCount(EncounterLockState state, int livingMembers, int unspawnedPlanned) =>
            state == EncounterLockState.Cleared ? 0 : Math.Max(0, livingMembers) + Math.Max(0, unspawnedPlanned);
    }
}
