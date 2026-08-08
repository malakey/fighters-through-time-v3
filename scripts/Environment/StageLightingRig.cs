using Godot;

namespace FTT.Environment {

    /// <summary>
    /// Package 8 B7: the reusable placeholder lighting rig for Fighter stages, the
    /// hub and campaign levels.
    ///
    /// <para>One <see cref="CanvasModulate"/> ambient tone plus three textured
    /// <see cref="PointLight2D"/>s (key / fill / rim), all driven from exports on
    /// this root so a stage tunes its era with a single node-override block in its
    /// own <c>.tscn</c> — no <c>index=</c> blocks reaching into an instanced
    /// scene's children.</para>
    ///
    /// <para><b>Readability is the constraint, not mood.</b> A
    /// <see cref="CanvasModulate"/> multiplies the entire canvas layer it belongs
    /// to, fighters included, and this project renders characters as flat
    /// placeholder silhouettes with no rim art to survive a crush. Every authored
    /// ambient tone therefore stays above <see cref="MinimumAmbientChannel"/> on
    /// every channel; the era identity comes from the light colours and from the
    /// *ratio* between channels, not from darkness. <c>StageLightingRigTests</c>
    /// enforces the floor across every scene that instances the rig.</para>
    ///
    /// <para>The lights are <see cref="Light2D.BlendModeEnum.Add"/> (the engine
    /// default) so they add energy back over the modulated canvas. They carry the
    /// shared radial gradient A3 authored for the glow arbiter; a texture-less
    /// <see cref="PointLight2D"/> renders nothing at all, which is how the repo's
    /// one pre-existing light sat inert since Package 2.</para>
    ///
    /// <para>2D lights and <see cref="CanvasModulate"/> both act per canvas — a
    /// <see cref="CanvasLayer"/> owns its own canvas — so a rig placed in the scene
    /// root affects world content only and never touches the HUD layer above it.</para>
    /// </summary>
    [GlobalClass]
    public partial class StageLightingRig : Node2D {

        /// <summary>A3's shared radial falloff. A light without a texture draws nothing.</summary>
        public const string LightTexturePath = "res://assets/placeholders/glow_light_gradient.tres";

        /// <summary>
        /// No authored ambient channel may fall below this. See the class remarks:
        /// the fighters are silhouettes and a crushed canvas costs legibility that
        /// no amount of additive light reliably buys back at the screen edges.
        /// </summary>
        public const float MinimumAmbientChannel = 0.55f;

        private Color _ambientTone = new(0.82f, 0.82f, 0.86f);
        private Color _keyColor = new(1f, 0.86f, 0.66f);
        private Color _fillColor = new(0.62f, 0.74f, 0.95f);
        private Color _rimColor = new(0.8f, 0.86f, 1f);
        private float _keyEnergy = 0.65f;
        private float _fillEnergy = 0.4f;
        private float _rimEnergy = 0.3f;
        private Vector2 _keyOffset = new(700f, 380f);
        private Vector2 _fillOffset = new(1200f, 380f);
        private Vector2 _rimOffset = new(950f, 150f);
        private float _keyScale = 10f;
        private float _fillScale = 9f;
        private float _rimScale = 12f;
        private bool _rimEnabled = true;
        private bool _followsCamera;

        /// <summary>Ambient canvas tone. Never crushed — see <see cref="MinimumAmbientChannel"/>.</summary>
        [Export]
        public Color AmbientTone {
            get => _ambientTone;
            set { _ambientTone = value; Apply(); }
        }

        [Export]
        public Color KeyColor {
            get => _keyColor;
            set { _keyColor = value; Apply(); }
        }

        [Export]
        public Color FillColor {
            get => _fillColor;
            set { _fillColor = value; Apply(); }
        }

        [Export]
        public Color RimColor {
            get => _rimColor;
            set { _rimColor = value; Apply(); }
        }

        [Export(PropertyHint.Range, "0,4,0.05")]
        public float KeyEnergy {
            get => _keyEnergy;
            set { _keyEnergy = value; Apply(); }
        }

        [Export(PropertyHint.Range, "0,4,0.05")]
        public float FillEnergy {
            get => _fillEnergy;
            set { _fillEnergy = value; Apply(); }
        }

        [Export(PropertyHint.Range, "0,4,0.05")]
        public float RimEnergy {
            get => _rimEnergy;
            set { _rimEnergy = value; Apply(); }
        }

        [Export]
        public Vector2 KeyOffset {
            get => _keyOffset;
            set { _keyOffset = value; Apply(); }
        }

        [Export]
        public Vector2 FillOffset {
            get => _fillOffset;
            set { _fillOffset = value; Apply(); }
        }

        [Export]
        public Vector2 RimOffset {
            get => _rimOffset;
            set { _rimOffset = value; Apply(); }
        }

        [Export(PropertyHint.Range, "0.5,15,0.5")]
        public float KeyScale {
            get => _keyScale;
            set { _keyScale = value; Apply(); }
        }

        [Export(PropertyHint.Range, "0.5,15,0.5")]
        public float FillScale {
            get => _fillScale;
            set { _fillScale = value; Apply(); }
        }

        [Export(PropertyHint.Range, "0.5,15,0.5")]
        public float RimScale {
            get => _rimScale;
            set { _rimScale = value; Apply(); }
        }

        /// <summary>A two-light rig is a legitimate era choice; the rim is optional.</summary>
        [Export]
        public bool RimEnabled {
            get => _rimEnabled;
            set { _rimEnabled = value; Apply(); }
        }

        /// <summary>
        /// Campaign levels are thousands of pixels wide and build their geometry at
        /// runtime, so three fixed lights would only ever dress the opening room.
        /// With this set the rig glides to the active <see cref="Camera2D"/> each
        /// frame and the key/fill pair stays meaningful across the whole level. The
        /// ten Fighter stages leave it off: their play area is one authored screen
        /// and the lights are placed against real geometry.
        /// </summary>
        [Export]
        public bool FollowsCamera {
            get => _followsCamera;
            set { _followsCamera = value; SetProcess(_followsCamera); }
        }

        public CanvasModulate Ambient => GetNodeOrNull<CanvasModulate>("Ambient");
        public PointLight2D KeyLight => GetNodeOrNull<PointLight2D>("KeyLight");
        public PointLight2D FillLight => GetNodeOrNull<PointLight2D>("FillLight");
        public PointLight2D RimLight => GetNodeOrNull<PointLight2D>("RimLight");

        public override void _Ready() {
            Apply();
            SetProcess(_followsCamera);
        }

        public override void _Process(double delta) {
            if (!_followsCamera) return;
            Camera2D camera = GetViewport()?.GetCamera2D();
            if (camera == null) return;
            GlobalPosition = camera.GetScreenCenterPosition();
        }

        /// <summary>
        /// Pushes every export onto the child nodes. Called from the property
        /// setters (so an instanced scene's overrides land as the scene is built)
        /// and again from <see cref="_Ready"/> (so a rig constructed in code, or one
        /// whose children were not yet attached when a setter ran, still resolves).
        /// </summary>
        public void Apply() {
            CanvasModulate ambient = Ambient;
            if (ambient != null) ambient.Color = _ambientTone;

            ApplyLight(KeyLight, _keyColor, _keyEnergy, _keyOffset, _keyScale, true);
            ApplyLight(FillLight, _fillColor, _fillEnergy, _fillOffset, _fillScale, true);
            ApplyLight(RimLight, _rimColor, _rimEnergy, _rimOffset, _rimScale, _rimEnabled);
        }

        private static void ApplyLight(
            PointLight2D light, Color color, float energy, Vector2 offset, float scale, bool enabled) {
            if (light == null) return;
            light.Color = color;
            light.Energy = energy;
            light.Position = offset;
            light.TextureScale = scale;
            light.Enabled = enabled && energy > 0f;
        }
    }
}
