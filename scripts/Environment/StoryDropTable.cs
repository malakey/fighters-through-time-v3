using FTT.Core;
using Godot;
using System;

namespace FTT.Environment {

    [GlobalClass]
    public partial class StoryDropTable : Resource {
        public const string DefaultPath = "res://resources/Drops/story_drop_table.tres";

        [Export] public StoryDropProfile[] Profiles = Array.Empty<StoryDropProfile>();

        public StoryDropProfile GetProfile(Difficulty difficulty) {
            foreach (StoryDropProfile profile in Profiles ?? Array.Empty<StoryDropProfile>()) {
                if (profile != null && profile.Difficulty == difficulty) return profile;
            }
            return null;
        }

        public static StoryDropTable LoadDefault() => FTT.Core.AuthoredResources.Load<StoryDropTable>(DefaultPath);
    }
}
