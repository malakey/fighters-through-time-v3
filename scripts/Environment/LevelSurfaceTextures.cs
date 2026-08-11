using Godot;

namespace FTT.Environment {

    /// <summary>
    /// Optional per-level texture set for the <see cref="StoryLevelControllerBase"/>
    /// graybox builders (Package 10 asset replacement). A null set - the default -
    /// keeps the original flat ColorRect visuals, and a level opts in surface by
    /// surface; a builder call that passes an explicit fill colour also keeps that
    /// authored colour rather than the texture.
    /// </summary>
    public sealed class LevelSurfaceTextures {

        public sealed class Entry {
            public Texture2D Texture;
            public int MarginLeft = 24;
            public int MarginRight = 24;
            public int MarginTop = 8;
            public int MarginBottom = 8;
            public bool TileHorizontal = true;
            public bool TileVertical;
        }

        public Entry Floor;
        public Entry Platform;
        public Entry OneWay;
        public Entry Wall;
    }
}
