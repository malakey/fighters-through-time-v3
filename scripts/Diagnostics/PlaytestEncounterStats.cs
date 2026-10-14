using System;
using System.Collections.Generic;
using FTT.Combat;
using FTT.Core;

namespace FTT.Diagnostics {

    // =====================================================================
    // GU-BOT (gameplay update 2026-10-12, plan §5): the bots' telemetry for
    // the two systems that update added — encounter locks (GU-E, user request
    // 3) and the Story engagement director (GU-F, request 6). The Godot-free
    // half: PlaytestTelemetry feeds these from the live world, and they read
    // nothing but their arguments, so they are tested without a scene.
    // Diagnostics only; nothing under scripts/FighterSim/ may reference it.
    // =====================================================================

    /// <summary>
    /// One telemetry frame of the hero's crowd (plan §5 GU-BOT, the
    /// <c>mob_engagement</c> block). Every count is over living
    /// <c>EnemyController</c> mobs (bosses never hold a slot).
    /// </summary>
    public struct EngagementFrameSample {
        /// <summary>Mobs whose rotation is on the hero (a slot or a wait role), plus mobs attacking near it.</summary>
        public int Engaged;
        /// <summary>Mobs in <c>EnemyState.Attacking</c> within <see cref="MobEngagementTracker.NearPixels"/> of the hero.</summary>
        public int AttackingNear;
        /// <summary>Of those, the ones whose attack in flight is melee-slot class (MeleeStrike, ChargeDash, AreaPulse, the engage blink).</summary>
        public int MeleeAttackingNear;
        /// <summary>
        /// FIX-ENEMY (2026-10-13): of the melee attackers near the hero, the ones
        /// holding no melee slot — retaliation, which the director never gates
        /// (the V7.4 pressure exit of a struck mob whose stun ends in reach, and
        /// the Armored Recovery answer). Every gated melee commit holds a slot.
        /// </summary>
        public int MeleeRetaliatingNear;
        /// <summary>Mobs holding one of the hero's melee slots.</summary>
        public int Holders;
        /// <summary>Mobs attacking the hero with a ranged-permit attack in flight (any distance).</summary>
        public int RangedInFlight;
    }

    /// <summary>
    /// The <c>mob_engagement</c> block: how many mobs attack the hero at once,
    /// how the melee slots rotate, how far the waiters stand, and how many
    /// ranged permits are in use — the measure of "they should wait their turn"
    /// (user request 6; acceptance in ledger <c>USER-ENEMY-TURN-TAKING</c>: the
    /// concurrent melee attackers near the hero stay within the difficulty's
    /// slot total). Means are over <see cref="EngagedFrames"/> only, so the
    /// quiet stretches between fights do not dilute them.
    /// </summary>
    public sealed class MobEngagementTracker {
        /// <summary>"Near the hero": a mob within this many pixels of the hero's pivot (the plan's 250 px).</summary>
        public const float NearPixels = 250f;

        /// <summary>The difficulty's melee slot total (E1 / N2 / H3) and ranged permits, for the report's context.</summary>
        public int SlotTotal;
        public int PermitTotal;

        public int EngagedFrames { get; private set; }
        public int MaxAttackingNear { get; private set; }
        public int MaxMeleeAttackingNear { get; private set; }
        /// <summary>FIX-ENEMY: the most retaliating melee attackers near the hero in one frame.</summary>
        public int MaxMeleeRetaliatingNear { get; private set; }
        public int MaxHolders { get; private set; }
        public int MaxRangedInFlight { get; private set; }
        public long SumAttackingNear { get; private set; }
        public long SumMeleeAttackingNear { get; private set; }
        public long SumHolders { get; private set; }
        public long SumRangedInFlight { get; private set; }
        /// <summary>Engaged frames with more melee attackers near the hero than the slot total (retaliation is never gated, so not always a fault).</summary>
        public int FramesMeleeOverSlots { get; private set; }
        /// <summary>
        /// FIX-ENEMY: engaged frames with more <i>gated</i> melee attackers near the
        /// hero (melee attackers less the retaliating ones) than the slot total —
        /// the turn-taking invariant itself; should stay 0. The difference from
        /// <see cref="FramesMeleeOverSlots"/> is retaliation.
        /// </summary>
        public int FramesGatedMeleeOverSlots { get; private set; }
        /// <summary>Engaged frames with more slot holders than the slot total (the director's own invariant; should stay 0).</summary>
        public int FramesHoldersOverSlots { get; private set; }
        /// <summary>Engaged frames with more ranged attacks in flight than the permit total.</summary>
        public int FramesRangedOverPermits { get; private set; }
        /// <summary>A mob took a melee slot (from a wait role or on joining).</summary>
        public int SlotGrants { get; private set; }
        /// <summary>A slot holder became a waiter while still engaged on the hero (a rotation or a stale give-up).</summary>
        public int SlotHandoffs { get; private set; }

        /// <summary>Engaged frames by how many mobs attacked near the hero (0, 1, 2, …).</summary>
        public readonly SortedDictionary<int, int> AttackingNearHistogram = new();
        private readonly SortedDictionary<int, int> _waiterGaps = new();
        private readonly SortedDictionary<int, int> _frontWaiterGaps = new();
        private readonly Dictionary<ulong, (int Generation, StoryEngagementRules.EngagementRole Role)> _roles = new();

        /// <summary>Books one telemetry frame; a frame with nothing engaged is not counted.</summary>
        public void SampleFrame(in EngagementFrameSample sample) {
            if (sample.Engaged <= 0 && sample.AttackingNear <= 0) return;
            EngagedFrames++;
            SumAttackingNear += sample.AttackingNear;
            SumMeleeAttackingNear += sample.MeleeAttackingNear;
            SumHolders += sample.Holders;
            SumRangedInFlight += sample.RangedInFlight;
            MaxAttackingNear = Math.Max(MaxAttackingNear, sample.AttackingNear);
            MaxMeleeAttackingNear = Math.Max(MaxMeleeAttackingNear, sample.MeleeAttackingNear);
            MaxMeleeRetaliatingNear = Math.Max(MaxMeleeRetaliatingNear, sample.MeleeRetaliatingNear);
            MaxHolders = Math.Max(MaxHolders, sample.Holders);
            MaxRangedInFlight = Math.Max(MaxRangedInFlight, sample.RangedInFlight);
            if (SlotTotal > 0 && sample.MeleeAttackingNear > SlotTotal) FramesMeleeOverSlots++;
            if (SlotTotal > 0 && sample.MeleeAttackingNear - sample.MeleeRetaliatingNear > SlotTotal) FramesGatedMeleeOverSlots++;
            if (SlotTotal > 0 && sample.Holders > SlotTotal) FramesHoldersOverSlots++;
            if (PermitTotal > 0 && sample.RangedInFlight > PermitTotal) FramesRangedOverPermits++;
            AttackingNearHistogram[sample.AttackingNear] = AttackingNearHistogram.GetValueOrDefault(sample.AttackingNear) + 1;
        }

        /// <summary>
        /// A mob's role this frame (on the hero; <c>None</c> when it is not in the
        /// hero's rotation). A slot taken from anything else is a grant; a slot
        /// given up for a wait role is a hand-off. A new pool generation of the
        /// same instance is a new body.
        /// </summary>
        public void NoteRole(ulong mobId, int generation, StoryEngagementRules.EngagementRole role) {
            StoryEngagementRules.EngagementRole previous = StoryEngagementRules.EngagementRole.None;
            if (_roles.TryGetValue(mobId, out var known) && known.Generation == generation) previous = known.Role;
            if (role == StoryEngagementRules.EngagementRole.Slot && previous != StoryEngagementRules.EngagementRole.Slot) SlotGrants++;
            if (previous == StoryEngagementRules.EngagementRole.Slot && role == StoryEngagementRules.EngagementRole.Waiting) SlotHandoffs++;
            _roles[mobId] = (generation, role);
        }

        /// <summary>
        /// A ringing waiter's horizontal gap to its aim, pivot to pivot.
        /// <paramref name="front"/>: it is the nearest waiter on its side this frame
        /// (the rank-0 gap the director's table sets: E200 / N170 / H150).
        /// </summary>
        public void NoteWaiterGap(float gapPixels, bool front) {
            int gap = (int)MathF.Round(gapPixels);
            _waiterGaps[gap] = _waiterGaps.GetValueOrDefault(gap) + 1;
            if (front) _frontWaiterGaps[gap] = _frontWaiterGaps.GetValueOrDefault(gap) + 1;
        }

        /// <summary>
        /// A ringing waiter this far from its aim is well off its ring (the widest
        /// rank-0 gap is 200 px): still walking in from across a wide area (an
        /// engage-time wave spawned at the far end walks in at the waiter speed),
        /// or held off it by a ledge, a wall or a barrier. Logged with positions
        /// so a level author can tell which; a gap that does not shrink across the
        /// log is a stuck waiter.
        /// </summary>
        public const float FarWaiterGapPixels = 600f;
        /// <summary>At most this many far-waiter samples are logged, one per mob per <see cref="FarWaiterSampleFrames"/>.</summary>
        public const int FarWaiterLogLimit = 60;
        public const int FarWaiterSampleFrames = 60;

        /// <summary>Far-waiter samples: (frame, enemy id, x, y, gap).</summary>
        public readonly List<(int Frame, string Enemy, int X, int Y, int Gap)> FarWaiters = new();
        public int FarWaiterSamples { get; private set; }

        /// <summary>Counts every far-waiter sample and logs at most one per mob per sample window.</summary>
        public void NoteFarWaiter(int frame, string enemy, float x, float y, float gapPixels) {
            FarWaiterSamples++;
            if (FarWaiters.Count >= FarWaiterLogLimit || frame % FarWaiterSampleFrames != 0) return;
            FarWaiters.Add((frame, enemy ?? "", (int)MathF.Round(x), (int)MathF.Round(y), (int)MathF.Round(gapPixels)));
        }

        public int WaiterGapSamples => Count(_waiterGaps);
        public int WaiterGapMedian => HistogramMedian(_waiterGaps);
        public int FrontWaiterGapMedian => HistogramMedian(_frontWaiterGaps);

        /// <summary>The report's waiter-gap histograms are bucketed to this many pixels (each key is its bucket's low edge).</summary>
        public const int GapBucketPixels = 8;

        /// <summary>Every ringing waiter's gap, bucketed (for pooling across runs: compare_runs.py).</summary>
        public SortedDictionary<int, int> WaiterGapBuckets => Bucket(_waiterGaps);

        /// <summary>The front waiter's gap per side, bucketed.</summary>
        public SortedDictionary<int, int> FrontWaiterGapBuckets => Bucket(_frontWaiterGaps);

        private static SortedDictionary<int, int> Bucket(SortedDictionary<int, int> histogram) {
            var buckets = new SortedDictionary<int, int>();
            foreach (KeyValuePair<int, int> entry in histogram) {
                int key = (int)MathF.Floor(entry.Key / (float)GapBucketPixels) * GapBucketPixels;
                buckets[key] = buckets.GetValueOrDefault(key) + entry.Value;
            }
            return buckets;
        }

        /// <summary>A mean over the engaged frames, two decimals; −1 with none.</summary>
        public double MeanOverEngaged(long sum) => EngagedFrames > 0 ? Math.Round((double)sum / EngagedFrames, 2) : -1.0;

        /// <summary>The lower median of a value histogram (the report's <c>sorted[n / 2]</c> convention); −1 when empty.</summary>
        public static int HistogramMedian(SortedDictionary<int, int> histogram) {
            int total = Count(histogram);
            if (total == 0) return -1;
            int index = total / 2;
            foreach (KeyValuePair<int, int> entry in histogram) {
                if (index < entry.Value) return entry.Key;
                index -= entry.Value;
            }
            return -1;
        }

        private static int Count(SortedDictionary<int, int> histogram) {
            int total = 0;
            foreach (int count in histogram.Values) total += count;
            return total;
        }
    }

    /// <summary>One frame's reading of an encounter lock (the telemetry copies it off the node).</summary>
    public struct LockObservation {
        public string LockID;
        /// <summary>
        /// The lock node's instance ID (0 = unknown, read as the same instance). A
        /// scene reload — an Act III Anchor Snap reloads the level and the run
        /// goes on — rebuilds every lock as a new node under the same LockID.
        /// </summary>
        public ulong InstanceId;
        public EncounterLockState State;
        public int Planned;
        public int Registered;
        public int Living;
        public int Remaining;
        public int Engages;
        public int Reverts;
        public int StallRelocations;
        public int Recycled;
        public bool ForceCleared;
        public bool RestoredCleared;
        public double EngagedSeconds;
        /// <summary>The lock's living hero stands inside its area (the lock's own containment test).</summary>
        public bool HeroInside;
        public bool HeroAlive;
    }

    /// <summary>
    /// One instance of an encounter lock over a run (the <c>locks</c> block): the
    /// lock as built, or as a scene reload rebuilt it (FIX-BOT, 2026-10-13).
    /// </summary>
    public sealed class LockRecord {
        public string LockID = "";
        /// <summary>1 for the lock as first built, 2 for its first rebuild by a scene reload, and so on.</summary>
        public int Instance = 1;
        /// <summary>The lock node this record reads (0 while unknown).</summary>
        public ulong InstanceId { get; internal set; }
        public int FirstSeenFrame = -1;
        /// <summary>The first frame it was seen Engaged.</summary>
        public int EngageFrame = -1;
        /// <summary>The frame it was first seen Cleared (−1 while it never cleared in this run).</summary>
        public int ClearFrame = -1;
        public string State = "armed";
        public int Planned;
        public int Registered;
        public int Living;
        public int MaxLiving;
        public int Engages;
        public int Reverts;
        public int StallRelocations;
        public int Recycled;
        public bool ForceCleared;
        public bool RestoredCleared;
        public double EngagedSeconds;
        /// <summary>The hero (alive) went from inside the area to outside it while the lock held living members.</summary>
        public int LeftWithMembersAlive;
        public readonly List<int> LeftFrames = new();
        /// <summary>Members' own relocations (fall net + stall watchdog) and fall-net catches, summed over members.</summary>
        public int MemberRelocations;
        public int FallRecoveries;
        /// <summary>Members the hero's damage killed (their record reached 0 HP).</summary>
        public int Killed;

        internal bool LastHeroInside;
        internal bool SeenInside;
        /// <summary>Telemetry-side: (instance, generation) → the member's record and its relocation high-water marks.</summary>
        internal readonly Dictionary<(ulong Id, int Generation), MemberTrack> Members = new();

        internal sealed class MemberTrack {
            public EnemyRecord Record;
            public int Relocations;
            public int FallRecoveries;
        }

        /// <summary>Frames from the first engage to the clear; −1 when either is missing.</summary>
        public int FramesToClear => EngageFrame >= 0 && ClearFrame >= 0 ? ClearFrame - EngageFrame : -1;

        public static string StateName(EncounterLockState state) => state switch {
            EncounterLockState.Engaged => "engaged",
            EncounterLockState.Cleared => "cleared",
            _ => "armed"
        };
    }

    /// <summary>
    /// The <c>locks</c> block: per lock, when it engaged and cleared, what it
    /// planned and what was killed, its reverts, relocations and force-clear,
    /// and every time the hero left its area with members still alive — the
    /// 2026-10-12 meaning of "passed" (user request 3: "you no longer have the
    /// option of jumping past them").
    ///
    /// <para><b>One record per lock instance</b> (FIX-BOT, 2026-10-13). An Act III
    /// Anchor Snap reloads the scene and the run goes on: every lock is rebuilt
    /// as a new node under the same ID — Armed with fresh members on a slotless
    /// bot run, Cleared (restored) from a save's baseline. Keyed by ID alone, the
    /// rebuilt node's counters overwrote the first one's, its members' kills
    /// stacked onto one plan (killed &gt; planned), a restored rebuild relabelled a
    /// lock cleared live as "restored", and a hero respawning outside booked a
    /// leave. Each instance now keeps its own record; the totals count distinct
    /// lock IDs (<see cref="LockCount"/>, <see cref="ClearedLocks"/>) beside the
    /// rebuilds (<see cref="Reloads"/>).</para>
    /// </summary>
    public sealed class LockTracker {
        /// <summary>LockID → the record of its current (latest) instance.</summary>
        private readonly Dictionary<string, LockRecord> _byId = new();
        /// <summary>Every instance record, in the order first seen.</summary>
        public readonly List<LockRecord> Records = new();

        public LockRecord Observe(int frame, in LockObservation o) {
            string id = o.LockID ?? "";
            _byId.TryGetValue(id, out LockRecord record);
            if (record == null || (o.InstanceId != 0 && record.InstanceId != 0 && o.InstanceId != record.InstanceId)) {
                // A new lock, or the same lock rebuilt by a scene reload: its own
                // record, starting with the hero outside (a respawn is no leave).
                record = new LockRecord {
                    LockID = id, FirstSeenFrame = frame, InstanceId = o.InstanceId, Instance = (record?.Instance ?? 0) + 1
                };
                _byId[id] = record;
                Records.Add(record);
            } else if (record.InstanceId == 0) {
                record.InstanceId = o.InstanceId;
            }
            if (o.State == EncounterLockState.Engaged && record.EngageFrame < 0) record.EngageFrame = frame;
            // A lock restored Cleared from a resume baseline never engaged in this run;
            // its clear frame is when it was first seen.
            if (o.State == EncounterLockState.Cleared && record.ClearFrame < 0) record.ClearFrame = frame;
            bool left = record.SeenInside && record.LastHeroInside && !o.HeroInside && o.HeroAlive
                && o.Living > 0 && o.State != EncounterLockState.Cleared;
            if (left) {
                record.LeftWithMembersAlive++;
                if (record.LeftFrames.Count < 50) record.LeftFrames.Add(frame);
            }
            // A dead hero is not judged (the lock does not judge it either): keep
            // the last living reading, so a rewind landing outside counts as a leave.
            if (o.HeroAlive) {
                record.LastHeroInside = o.HeroInside;
                record.SeenInside |= o.HeroInside;
            }
            record.State = LockRecord.StateName(o.State);
            record.Planned = o.Planned;
            record.Registered = o.Registered;
            record.Living = o.Living;
            record.MaxLiving = Math.Max(record.MaxLiving, o.Living);
            record.Engages = o.Engages;
            record.Reverts = o.Reverts;
            record.StallRelocations = o.StallRelocations;
            record.Recycled = o.Recycled;
            record.ForceCleared = o.ForceCleared;
            record.RestoredCleared = o.RestoredCleared;
            record.EngagedSeconds = o.EngagedSeconds;
            return record;
        }

        /// <summary>
        /// A member seen this frame: keeps its record (the telemetry's enemy
        /// lifetime) and the high-water marks of its relocation counters, which
        /// restart at every pool spawn.
        /// </summary>
        public void NoteMember(string lockID, ulong mobId, int generation, EnemyRecord record, int relocations, int fallRecoveries) =>
            NoteMember(Current(lockID), mobId, generation, record, relocations, fallRecoveries);

        /// <summary>A member of one lock instance (the record <see cref="Observe"/> returned for it this frame).</summary>
        public void NoteMember(LockRecord lockRecord, ulong mobId, int generation, EnemyRecord record, int relocations, int fallRecoveries) {
            if (lockRecord == null) return;
            var key = (mobId, generation);
            if (!lockRecord.Members.TryGetValue(key, out LockRecord.MemberTrack track)) {
                track = new LockRecord.MemberTrack();
                lockRecord.Members[key] = track;
            }
            if (record != null) track.Record ??= record;
            track.Relocations = Math.Max(track.Relocations, relocations);
            track.FallRecoveries = Math.Max(track.FallRecoveries, fallRecoveries);
        }

        /// <summary>Folds the members' tracks into each record's member totals (call before reporting).</summary>
        public void Summarize() {
            foreach (LockRecord record in Records) {
                int killed = 0, relocations = 0, falls = 0;
                foreach (LockRecord.MemberTrack track in record.Members.Values) {
                    relocations += track.Relocations;
                    falls += track.FallRecoveries;
                    if (track.Record != null && track.Record.KilledFrame >= 0 && track.Record.LastSeenHP <= 0) killed++;
                }
                record.Killed = killed;
                record.MemberRelocations = relocations;
                record.FallRecoveries = falls;
            }
        }

        public int LeftWithMembersAlive {
            get {
                int total = 0;
                foreach (LockRecord record in Records) total += record.LeftWithMembersAlive;
                return total;
            }
        }

        /// <summary>The record of a lock's current instance, or null for a lock never seen.</summary>
        public LockRecord Current(string lockID) => _byId.TryGetValue(lockID ?? "", out LockRecord record) ? record : null;

        /// <summary>Distinct locks seen (a lock rebuilt by a reload counts once).</summary>
        public int LockCount => _byId.Count;

        /// <summary>Lock instances a scene reload rebuilt (records beyond each lock's first).</summary>
        public int Reloads => Records.Count - _byId.Count;

        /// <summary>Distinct locks with an instance seen Engaged.</summary>
        public int EngagedLocks => DistinctLocks(record => record.EngageFrame >= 0);

        /// <summary>Distinct locks with an instance seen Cleared (live or restored): a lock a reload re-armed after the hero cleared it still counts.</summary>
        public int ClearedLocks => DistinctLocks(record => record.ClearFrame >= 0);

        private int DistinctLocks(Func<LockRecord, bool> predicate) {
            var ids = new HashSet<string>();
            foreach (LockRecord record in Records) if (predicate(record)) ids.Add(record.LockID);
            return ids.Count;
        }
    }
}
