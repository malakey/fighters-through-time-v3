using System;
using FTT.Core;
using Godot;

namespace FTT.Environment {

    /// <summary>
    /// Package 12 W8 (GAP-05, N01 with D12(a)): the one statement of the
    /// persisted <b>AwaitingSeal</b> rules, shared by
    /// <see cref="StoryLevelControllerBase"/> (Levels 2–15 and every Level 4A)
    /// and Florence's own controller, which predates the base.
    ///
    /// <para>AwaitingSeal lives in <c>attemptState.sealReadiness</c>
    /// (<see cref="StorySealReadiness"/>): the committed boss-defeated fact, the
    /// stable sealing-anchor ID and whether the boss's physical pickup is still
    /// uncollected. The defeat commits it once as an F10 critical event; the
    /// completion commit clears it; a load in between reconstructs a defeated
    /// boss, a ready anchor and at most one pending pickup.</para>
    /// </summary>
    public static class SealReadiness {

        /// <summary>The live attempt's readiness record, or null outside a live attempt.</summary>
        public static StorySealReadiness Current {
            get {
                StoryManager story = StoryManager.Instance;
                return story?.HasLiveAttempt == true ? story.CurrentAttempt.SealReadiness : null;
            }
        }

        /// <summary>
        /// True when this load resumes an attempt already awaiting its seal: the
        /// attempt record committed the defeat for <paramref name="levelID"/>, and
        /// the active save is parked in <paramref name="level"/>'s scene at a
        /// checkpoint (the same guard the checkpoint resume uses).
        /// </summary>
        public static bool IsPreSealLoad(string levelID, CampaignLevel level) {
            StoryManager story = StoryManager.Instance;
            if (story == null || !story.HasLiveAttempt || SaveManager.Instance == null
                || GameManager.Instance == null) return false;
            StorySealReadiness readiness = story.CurrentAttempt.SealReadiness;
            if (readiness == null || !readiness.BossDefeated) return false;
            if (!string.Equals(story.CurrentAttempt.LevelID, levelID, StringComparison.Ordinal)) return false;
            int slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            if (slot < 0 || slot >= SaveManager.Instance.SaveSlots.Length) return false;
            StorySaveData save = SaveManager.Instance.SaveSlots[slot];
            if (save == null || string.IsNullOrWhiteSpace(save.LastCheckpointID)) return false;
            return save.CurrentLevelID == StoryManager.GetLevelScenePath(level);
        }

        /// <summary>
        /// The boss defeat's once-only commit: defeat fact, anchor ID, pending
        /// pickup. Never completion, a deposit or an unlock.
        /// </summary>
        public static void CommitDefeat(string sealingAnchorID) {
            StoryManager story = StoryManager.Instance;
            if (story == null || !story.HasLiveAttempt) return;
            StorySealReadiness readiness = story.CurrentAttempt.SealReadiness ??= new StorySealReadiness();
            if (readiness.BossDefeated) return;
            readiness.BossDefeated = true;
            readiness.SealingAnchorID = sealingAnchorID ?? "";
            LevelRewardLedger ledger = LevelRewardDirectory.EnsureCompiled();
            readiness.PendingBossPickup = ledger != null
                && !string.IsNullOrEmpty(ledger.BossSourceID)
                && !LevelRewardDirectory.IsClaimed(ledger.BossSourceID);
            story.CurrentAttempt.Bump();
            story.CommitCriticalEvent();
        }

        /// <summary>The boss pickup was collected: the pending flag clears, readiness stays.</summary>
        public static void NoteBossPickupCollected() {
            StorySealReadiness readiness = Current;
            if (readiness == null || !readiness.BossDefeated || !readiness.PendingBossPickup) return;
            readiness.PendingBossPickup = false;
            StoryManager.Instance.CurrentAttempt.Bump();
        }

        /// <summary>AwaitingSeal ends inside the completion commit.</summary>
        public static void ClearOnCompletion() {
            StoryManager story = StoryManager.Instance;
            if (story?.HasLiveAttempt != true || story.CurrentAttempt.SealReadiness?.BossDefeated != true) return;
            story.CurrentAttempt.SealReadiness = new StorySealReadiness();
            story.CurrentAttempt.Bump();
        }

        /// <summary>
        /// F05 + N01: an uncollected boss reward survives a pre-seal load exactly
        /// once. Claims persist and issues do not, so the reload re-issues the
        /// same source and spawns its single Large pickup; a claimed source
        /// spawns nothing and clears the pending flag.
        /// </summary>
        public static ChronalDustPickup RestorePendingBossPickup(Vector2 position, Node parent) {
            StorySealReadiness readiness = Current;
            if (readiness == null || !readiness.PendingBossPickup) return null;
            LevelRewardLedger ledger = LevelRewardDirectory.EnsureCompiled();
            if (ledger == null || string.IsNullOrEmpty(ledger.BossSourceID)) return null;
            if (LevelRewardDirectory.IsClaimed(ledger.BossSourceID)) {
                readiness.PendingBossPickup = false;
                StoryManager.Instance.CurrentAttempt.Bump();
                return null;
            }
            if (!LevelRewardDirectory.TryIssueBossAward(out string sourceID, out int amount) || amount <= 0) return null;
            return StoryDropSystem.SpawnDustAward(amount, position, parent, DustAwardSource.Boss, sourceID);
        }
    }
}
