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
        /// The visible Story view in world pixels at the gameplay update's 1.5
        /// camera zoom (1920 / 1.5 x 1080 / 1.5). A lock's camera rect is never
        /// narrower than this, and its area is never narrower than the view's width.
        /// <b>TODO (orchestrator):</b> point both at <c>StoryCameraRules.ViewWorldSize</c>
        /// once GU-B's camera constant is merged; until then this is the only copy.
        /// </summary>
        public const float ViewWorldWidth = 1280f;

        /// <summary>See <see cref="ViewWorldWidth"/>.</summary>
        public const float ViewWorldHeight = 720f;

        /// <summary>
        /// <i>Provisional.</i> The camera eases into and out of a lock rect over
        /// this damping time (the follow rig's own damper semantics: 99 % of the
        /// gap closed) instead of snapping.
        /// </summary>
        public const float CameraEaseSeconds = 0.6f;

        /// <summary><i>Provisional.</i> How long the HUD's GO prompt stays up after a clear.</summary>
        public const float GoPromptSeconds = 3f;

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
        /// its area (an Echo Step, a death-rewind landing, a rift snap, a scripted
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

        /// <summary>The stall counter after one sample: it grows while stalled and resets otherwise.</summary>
        public static int NextStallFrames(int current, bool stalled) => stalled ? Math.Max(0, current) + 1 : 0;

        /// <summary>True once the stall counter reaches <see cref="StallFrames"/>: relocate to the anchor.</summary>
        public static bool ShouldRelocate(int stallFrames) => stallFrames >= StallFrames;

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

        /// <summary>
        /// The GO prompt's arrow glyph — the non-colour channel the prompt reads
        /// by, beside its translated text. Empty for <see cref="EncounterGoDirection.None"/>.
        /// </summary>
        public static string GoGlyph(EncounterGoDirection direction) => direction switch {
            EncounterGoDirection.Right => "→",
            EncounterGoDirection.Left => "←",
            EncounterGoDirection.Up => "↑",
            EncounterGoDirection.Down => "↓",
            _ => ""
        };
    }
}
