using Godot;

namespace FTT.Environment {

    public partial class OneWayPlatform : StaticBody2D {
        public override void _Ready() {
            AddToGroup("OneWayPlatform");
        }
    }
}
