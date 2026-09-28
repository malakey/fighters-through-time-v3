namespace FTT.Core {

    /// <summary>
    /// Package 12 W6 (G14). The engine-side half of lowering the campaign
    /// difficulty from the hub: it reads the live session/save/story state into
    /// <see cref="CampaignDifficultyRules.Evaluate"/> and, when allowed, commits
    /// one step down to the active save and the session.
    ///
    /// <para><b>Nothing is retroactive.</b> The session difficulty is what every
    /// fresh level entry reads — <see cref="StoryManager.BeginLevelRun"/> refills
    /// the rewind pool from it and mints Act III anchor charges from it, and
    /// <see cref="StoryDifficultyTuning.CurrentStoryDifficulty"/> scales enemies,
    /// drops and the Integrity multiplier from it — so the new tier reaches the
    /// next level entry and nothing else. Committed Integrity, rewards and
    /// completed levels are never touched. A parked mid-level attempt refuses the
    /// change outright, because resuming it would apply the new tier mid-level.</para>
    /// </summary>
    public static class CampaignDifficultyService {

        /// <summary>Evaluates a lowering request against the live campaign state.</summary>
        public static CampaignDifficultyRules.Verdict EvaluateActive(bool atHub) {
            StorySaveData save = ActiveSave(out _);
            if (save == null) {
                return CampaignDifficultyRules.Evaluate(false, Difficulty.Normal, atHub, false, false, false);
            }
            bool parked = !string.IsNullOrWhiteSpace(save.LastCheckpointID)
                || StoryManager.Instance?.HasPendingTimelineRestart == true;
            bool insideGauntlet = parked && StoryManager.Instance?.IsInActIII == true;
            return CampaignDifficultyRules.Evaluate(
                hasCampaign: true,
                current: save.Difficulty,
                atHub: atHub,
                attemptParkedMidLevel: parked,
                insideActIIIGauntlet: insideGauntlet,
                campaignCompleted: save.IsCompleted);
        }

        /// <summary>
        /// Lowers the active campaign one step and persists it. Returns the verdict;
        /// only <see cref="CampaignDifficultyRules.Verdict.Allowed"/> changed anything.
        /// </summary>
        public static CampaignDifficultyRules.Verdict TryLowerActiveCampaign(bool atHub) {
            CampaignDifficultyRules.Verdict verdict = EvaluateActive(atHub);
            if (verdict != CampaignDifficultyRules.Verdict.Allowed) return verdict;
            StorySaveData save = ActiveSave(out int slot);
            if (!CampaignDifficultyRules.LowerOneStep(save)) return CampaignDifficultyRules.Verdict.AlreadyEasiest;
            if (GameManager.Instance != null) {
                SessionData session = GameManager.Instance.CurrentSession;
                session.Difficulty = save.Difficulty;
                GameManager.Instance.CurrentSession = session;
            }
            SaveManager.Instance?.SaveStorySlot(slot);
            return verdict;
        }

        private static StorySaveData ActiveSave(out int slot) {
            slot = GameManager.Instance?.CurrentSession.ActiveSaveSlot ?? -1;
            SaveManager manager = SaveManager.Instance;
            if (manager == null || slot < 0 || slot >= manager.SaveSlots.Length) return null;
            return manager.SaveSlots[slot];
        }

        /// <summary>Translation key for a tier's name.</summary>
        public static string DifficultyNameKey(Difficulty difficulty) => difficulty switch {
            Difficulty.Easy => "difficulty_easy",
            Difficulty.Hard => "difficulty_hard",
            _ => "difficulty_normal"
        };
    }
}
