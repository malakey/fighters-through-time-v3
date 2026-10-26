using System;
using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;

namespace FTT.UI {

    /// <summary>
    /// UR-MENU. The pure rules behind the pulp cover's Continue sticker (UI_SPEC 3.7): which save is the
    /// "most recent", and the chapter number and short place name printed on the sticker. No scene access.
    /// </summary>
    public static class MainMenuContinue {

        /// <summary>
        /// The index of the save with the greatest <c>LastSavedTimestamp</c>, or -1 when no slot holds a
        /// save. A save whose timestamp does not parse still counts (it sorts before every parsed one); an
        /// exact tie goes to the lower slot.
        /// </summary>
        public static int PickMostRecent(IReadOnlyList<StorySaveData> slots) {
            int best = -1;
            DateTimeOffset bestTime = DateTimeOffset.MinValue;
            if (slots == null) return best;
            for (int slot = 0; slot < slots.Count; slot++) {
                StorySaveData save = slots[slot];
                if (save == null) continue;
                DateTimeOffset time = DateTimeOffset.TryParse(save.LastSavedTimestamp, out DateTimeOffset parsed)
                    ? parsed
                    : DateTimeOffset.MinValue;
                if (best < 0 || time > bestTime) {
                    best = slot;
                    bestTime = time;
                }
            }
            return best;
        }

        /// <summary>
        /// The campaign slot the save is parked on, resolved by scene path over the sixteen route slots
        /// (the same lookup <c>StoryManager.ResumeCampaign</c> performs). An unknown path reads as the Tutorial.
        /// </summary>
        public static CampaignLevel LevelOf(StorySaveData save) {
            if (save == null) return CampaignLevel.Tutorial;
            for (int index = 0; index <= (int)CampaignLevel.Alexandria; index++) {
                var level = (CampaignLevel)index;
                if (StoryManager.GetLevelScenePath(level) == save.CurrentLevelID) return level;
            }
            return CampaignLevel.Tutorial;
        }

        /// <summary>The chapter number: the level's campaign number, 0 to 15.</summary>
        public static int ChapterNumber(StorySaveData save) => (int)LevelOf(save);

        /// <summary>The translation key of the level's hub mission name.</summary>
        public static string PlaceKey(StorySaveData save) => HubWorldController.CampaignLevelNameKey(LevelOf(save));

        /// <summary>
        /// The short place line: the mission name up to its subtitle ("Pompeii 79 AD — Eruption of Vesuvius"
        /// gives "Pompeii 79 AD").
        /// </summary>
        public static string ShortPlace(string missionName) {
            if (string.IsNullOrWhiteSpace(missionName)) return "";
            foreach (string separator in new[] { " — ", " – ", " - " }) {
                int cut = missionName.IndexOf(separator, StringComparison.Ordinal);
                if (cut > 0) return missionName.Substring(0, cut).Trim();
            }
            return missionName.Trim();
        }
    }
}
