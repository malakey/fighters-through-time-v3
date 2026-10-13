using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using Godot;

namespace FTT.Enemies {

    /// <summary>
    /// The Story engagement director (gameplay update 2026-10-12, request 6,
    /// workstream GU-F): a per-target registry of the mobs engaged with the
    /// campaign hero, which decides who holds the target's few melee slots, how
    /// many ranged attacks may fly at once, and where the waiters stand. The
    /// numbers are <see cref="StoryEngagementRules"/>; the per-mob behaviour is
    /// <c>EnemyController.Engagement.cs</c>.
    ///
    /// <para><b>No state of its own that can go stale.</b> The registry stores
    /// instance ids, never references, resolved with
    /// <see cref="GodotObject.InstanceFromId"/> and pruned on every access: an
    /// invalid, freed, queued, out-of-tree, pool-parked or physics-off body, a
    /// mob that is dead, patrolling or returning, or one that now chases another
    /// target leaves the group, and a dropped mob's own engagement state is
    /// cleared. Slot and permit counts are recomputed from the live members' own
    /// flags on every query, so a missed <see cref="Leave"/> can never leak a
    /// slot.</para>
    ///
    /// <para><b>No clock of its own.</b> Every decision is made inside a member's
    /// own unfrozen tick from current member state, and every counter (waited,
    /// rest, stale) lives on the mob, so a frozen or hitstopped mob neither
    /// ticks nor requests, and directly stepped tests stay deterministic. Ties
    /// break on instance id.</para>
    ///
    /// <para>Story only: never persisted, snapshotted or hashed, and nothing in
    /// <c>scripts/FighterSim/</c> names it. Only the campaign hero is a target —
    /// a <see cref="PlayerController.IsStoryHostile"/> body (the Level 13 Mirror
    /// clone) never gets a group. Bosses are not <c>EnemyController</c>s and
    /// never join; their summons do.</para>
    /// </summary>
    public static class StoryEngagementDirector {

        // targetId -> member instance ids, in join order.
        private static readonly Dictionary<ulong, List<ulong>> Groups = new();

        // Scratch buffers: every query runs on the main thread inside one
        // member's tick, so one shared set never interleaves.
        private static readonly List<EnemyController> Members = new();
        private static readonly List<StoryEngagementRules.Candidate> Candidates = new();
        private static readonly List<ulong> Grants = new();
        private static readonly List<ulong> DeadTargets = new();

        /// <summary>
        /// Test seam: when set, every gate answers as if no director existed and
        /// no mob joins (a lone-mob control run compares against it).
        /// </summary>
        internal static bool BypassForTest { get; set; }

        /// <summary>The Story difficulty the table is read at.</summary>
        public static Difficulty CurrentDifficulty => StoryDifficultyTuning.CurrentStoryDifficulty;

        /// <summary>
        /// Puts <paramref name="mob"/> in <paramref name="target"/>'s group as a
        /// waiter, unless it is already there (its own flags say so: the
        /// director clears them whenever it drops a member). Leaves any group it
        /// held for another target first. Returns false when the director does
        /// not apply (bypassed, no valid target, or a Story-hostile target).
        /// </summary>
        internal static bool Join(EnemyController mob, PlayerController target) {
            if (BypassForTest || mob == null || !GodotObject.IsInstanceValid(target) || target.IsStoryHostile) return false;
            ulong targetId = target.GetInstanceId();
            if (mob.EngagementRole != StoryEngagementRules.EngagementRole.None && mob.EngagementTargetId == targetId) {
                return true;
            }
            Leave(mob);
            if (!Groups.TryGetValue(targetId, out List<ulong> ids)) {
                PruneDeadTargets();
                ids = new List<ulong>();
                Groups[targetId] = ids;
            }
            ulong mobId = mob.GetInstanceId();
            if (!ids.Contains(mobId)) ids.Add(mobId);
            mob.BeginEngagement(targetId);
            return true;
        }

        /// <summary>
        /// Takes <paramref name="mob"/> out of its group and clears its
        /// engagement state. Safe to call on a mob that never joined.
        /// </summary>
        internal static void Leave(EnemyController mob) {
            if (mob == null) return;
            ulong targetId = mob.EngagementTargetId;
            if (targetId != 0 && Groups.TryGetValue(targetId, out List<ulong> ids)) {
                ids.Remove(mob.GetInstanceId());
                if (ids.Count == 0) Groups.Remove(targetId);
            }
            mob.ClearEngagementState();
        }

        /// <summary>
        /// True when <paramref name="mob"/> (a waiter that wants a melee slot) is
        /// one of the candidates <see cref="StoryEngagementRules.SelectGrants"/>
        /// hands a free slot to right now. The caller takes the slot.
        /// <paramref name="sideOverride"/> (±1) ranks <paramref name="mob"/> as
        /// if it stood on that side — the engage blink asks for the side it
        /// lands on; 0 uses the side it stands on.
        /// </summary>
        internal static bool IsGrantedMeleeSlot(EnemyController mob, int sideOverride = 0) {
            if (!CollectMembers(mob.EngagementTargetId)) return false;
            CountHolders(out int left, out int right);
            Difficulty difficulty = CurrentDifficulty;
            if (left + right >= StoryEngagementRules.MeleeSlotsTotal(difficulty)) return false;
            Candidates.Clear();
            foreach (EnemyController member in Members) {
                if (!member.IsMeleeSlotCandidate) continue;
                int side = member == mob && sideOverride != 0 ? sideOverride : member.EngagementSide;
                Candidates.Add(new StoryEngagementRules.Candidate(
                    member.GetInstanceId(), member.IsEngagementElite, member.EngagementRested,
                    side, member.EngagementWaitedFrames, member.EngagementGapPixels));
            }
            StoryEngagementRules.SelectGrants(Candidates, left, right, difficulty, Grants);
            return Grants.Contains(mob.GetInstanceId());
        }

        /// <summary>
        /// The engage-blink test for a slot holder: true when
        /// <paramref name="side"/> stays inside its per-side cap once
        /// <paramref name="holder"/>'s slot moves there (the other holders
        /// counted, the holder itself not).
        /// </summary>
        internal static bool SideHasRoomForHolder(EnemyController holder, int side) {
            if (!CollectMembers(holder.EngagementTargetId)) return true;
            int onSide = 0;
            int others = 0;
            foreach (EnemyController member in Members) {
                if (member == holder || member.EngagementRole != StoryEngagementRules.EngagementRole.Slot) continue;
                others++;
                if (member.EngagementHolderSide == (side < 0 ? -1 : 1)) onSide++;
            }
            return StoryEngagementRules.SideHasFreeSlot(onSide, others, CurrentDifficulty);
        }

        /// <summary>
        /// The hand-off test: true when a rested waiter that wants a melee slot
        /// could take <paramref name="holder"/>'s slot once it is released (the
        /// per-side cap included).
        /// </summary>
        internal static bool HasEligibleWaiterFor(EnemyController holder) {
            if (!CollectMembers(holder.EngagementTargetId)) return false;
            CountHolders(out int left, out int right);
            Difficulty difficulty = CurrentDifficulty;
            int holderSide = holder.EngagementHolderSide;
            foreach (EnemyController member in Members) {
                if (member == holder || !member.IsMeleeSlotCandidate || !member.EngagementRested) continue;
                if (StoryEngagementRules.CanTakeFreedSlot(member.EngagementSide, holderSide, left, right, difficulty)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// True while fewer of <paramref name="mob"/>'s fellow members are mid
        /// ranged attack than the difficulty's ranged permits. A permit is
        /// stateless: a member in <c>Attacking</c> whose attack in flight (or
        /// about to start, during its reaction delay) is ranged-class.
        /// </summary>
        internal static bool IsRangedPermitFree(EnemyController mob) {
            if (!CollectMembers(mob.EngagementTargetId)) return true;
            int inUse = 0;
            foreach (EnemyController member in Members) {
                if (member == mob || member.CurrentState != EnemyState.Attacking) continue;
                if (member.EngagementInFlightClass == StoryEngagementRules.EngagementClass.RangedPermit) inUse++;
            }
            return inUse < StoryEngagementRules.RangedPermits(CurrentDifficulty);
        }

        /// <summary>
        /// Ranged fairness: true when another member in Chase has been refused a
        /// ranged commit for longer than <paramref name="mob"/> (ties: the lower
        /// instance id), so the next free permit is its, not this one's.
        /// </summary>
        internal static bool HasLongerRangedWaiter(EnemyController mob) {
            if (!CollectMembers(mob.EngagementTargetId)) return false;
            int own = mob.EngagementRangedRefusedFrames;
            ulong ownId = mob.GetInstanceId();
            foreach (EnemyController member in Members) {
                if (member == mob || member.CurrentState != EnemyState.Chase) continue;
                if (StoryEngagementRules.RangedWaiterOutranks(
                        member.EngagementRangedRefusedFrames, member.GetInstanceId(), own, ownId)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// <paramref name="mob"/>'s rank among the waiters ringing its side
        /// (0 = nearest the target): ordered by gap, ties by instance id, with
        /// waiters still crossing over from the other side queued behind them.
        /// −1 when it is not a ringing waiter.
        /// </summary>
        internal static int WaitRank(EnemyController mob) {
            if (!mob.IsRingingWaiter || !CollectMembers(mob.EngagementTargetId)) return -1;
            int side = mob.EngagementRingSide;
            int rank = 0;
            foreach (EnemyController member in Members) {
                if (member == mob || !member.IsRingingWaiter || member.EngagementRingSide != side) continue;
                if (RanksBefore(member, mob)) rank++;
            }
            return rank;
        }

        /// <summary>
        /// The ringing waiters on <paramref name="side"/> of
        /// <paramref name="mob"/>'s target, <paramref name="mob"/> included.
        /// </summary>
        internal static int RingingWaitersOn(EnemyController mob, int side) {
            if (!CollectMembers(mob.EngagementTargetId)) return 0;
            int count = 0;
            foreach (EnemyController member in Members) {
                if (member.IsRingingWaiter && member.EngagementRingSide == side) count++;
            }
            return count;
        }

        /// <summary>
        /// Per side of <paramref name="mob"/>'s target: the melee slot holders,
        /// and the engaged melee presence (holders plus ringing waiters, a
        /// crossing waiter counted on the side it is crossing to).
        /// </summary>
        internal static void CountSides(EnemyController mob, out int holdersLeft, out int holdersRight,
            out int presenceLeft, out int presenceRight) {
            holdersLeft = holdersRight = presenceLeft = presenceRight = 0;
            if (!CollectMembers(mob.EngagementTargetId)) return;
            CountHolders(out holdersLeft, out holdersRight);
            presenceLeft = holdersLeft;
            presenceRight = holdersRight;
            foreach (EnemyController member in Members) {
                if (!member.IsRingingWaiter) continue;
                if (member.EngagementRingSide < 0) presenceLeft++;
                else presenceRight++;
            }
        }

        /// <summary>Live members engaged with <paramref name="target"/>. Test seam.</summary>
        internal static int MemberCountFor(PlayerController target) =>
            GodotObject.IsInstanceValid(target) && CollectMembers(target.GetInstanceId()) ? Members.Count : 0;

        /// <summary>Melee slots held on <paramref name="target"/>, both sides. Test seam.</summary>
        internal static int HolderCountFor(PlayerController target) {
            if (!GodotObject.IsInstanceValid(target) || !CollectMembers(target.GetInstanceId())) return 0;
            CountHolders(out int left, out int right);
            return left + right;
        }

        /// <summary>
        /// Test seam: empties the registry, clearing every still-valid member's
        /// engagement state so no flag outlives its group.
        /// </summary>
        internal static void ResetForTest() {
            foreach (List<ulong> ids in Groups.Values) {
                foreach (ulong id in ids) {
                    if (GodotObject.InstanceFromId(id) is EnemyController member && GodotObject.IsInstanceValid(member)) {
                        member.ClearEngagementState();
                    }
                }
            }
            Groups.Clear();
            Members.Clear();
            Candidates.Clear();
            Grants.Clear();
        }

        // === Internals ===

        private static bool RanksBefore(EnemyController other, EnemyController mob) {
            if (other.IsEngagementCrossing != mob.IsEngagementCrossing) return !other.IsEngagementCrossing;
            float otherGap = other.EngagementGapPixels;
            float mobGap = mob.EngagementGapPixels;
            if (otherGap != mobGap) return otherGap < mobGap;
            return other.GetInstanceId() < mob.GetInstanceId();
        }

        private static void CountHolders(out int left, out int right) {
            left = right = 0;
            foreach (EnemyController member in Members) {
                if (member.EngagementRole != StoryEngagementRules.EngagementRole.Slot) continue;
                // An engage blink in flight counts on the side it lands on.
                if (member.EngagementHolderSide < 0) left++;
                else right++;
            }
        }

        /// <summary>
        /// Prunes <paramref name="targetId"/>'s group and fills
        /// <see cref="Members"/> with its live members in join order. A group
        /// whose target is gone is dropped, its members' state cleared. Returns
        /// false when no live group exists.
        /// </summary>
        private static bool CollectMembers(ulong targetId) {
            Members.Clear();
            if (targetId == 0 || !Groups.TryGetValue(targetId, out List<ulong> ids)) return false;
            if (!IsLiveTarget(targetId)) {
                DropGroup(targetId, ids);
                return false;
            }
            for (int index = ids.Count - 1; index >= 0; index--) {
                var member = GodotObject.InstanceFromId(ids[index]) as EnemyController;
                if (member == null || !GodotObject.IsInstanceValid(member)) {
                    ids.RemoveAt(index);
                    continue;
                }
                if (member.EngagementTargetId != targetId) {
                    // It chases another target now; that group owns its flags.
                    ids.RemoveAt(index);
                    continue;
                }
                if (!IsLiveMember(member)) {
                    ids.RemoveAt(index);
                    member.ClearEngagementState();
                }
            }
            if (ids.Count == 0) {
                Groups.Remove(targetId);
                return false;
            }
            foreach (ulong id in ids) Members.Add((EnemyController)GodotObject.InstanceFromId(id));
            return true;
        }

        private static bool IsLiveTarget(ulong targetId) =>
            GodotObject.InstanceFromId(targetId) is PlayerController target
            && GodotObject.IsInstanceValid(target)
            && !target.IsQueuedForDeletion()
            && target.IsInsideTree()
            && !target.IsStoryHostile;

        private static bool IsLiveMember(EnemyController member) {
            if (member.IsQueuedForDeletion() || !member.IsInsideTree() || !member.IsPhysicsProcessing()) return false;
            // A pool-parked body sits in the tree with processing disabled; a
            // paused tree stops everyone and is no reason to leave.
            if (!member.CanProcess() && !member.GetTree().Paused) return false;
            if (member.EngagementRole == StoryEngagementRules.EngagementRole.None) return false;
            return member.CurrentState is not (EnemyState.Dead or EnemyState.Patrol
                or EnemyState.Returning or EnemyState.Idle);
        }

        private static void DropGroup(ulong targetId, List<ulong> ids) {
            foreach (ulong id in ids) {
                if (GodotObject.InstanceFromId(id) is EnemyController member
                    && GodotObject.IsInstanceValid(member)
                    && member.EngagementTargetId == targetId) {
                    member.ClearEngagementState();
                }
            }
            Groups.Remove(targetId);
        }

        /// <summary>Drops every group whose target is gone, so a level unload never leaves one behind.</summary>
        private static void PruneDeadTargets() {
            DeadTargets.Clear();
            foreach (ulong targetId in Groups.Keys) {
                if (!IsLiveTarget(targetId)) DeadTargets.Add(targetId);
            }
            foreach (ulong targetId in DeadTargets) DropGroup(targetId, Groups[targetId]);
            DeadTargets.Clear();
        }
    }
}
