using Godot;

namespace FTT.Core {

    public partial class CameraShake : Node {
        public static CameraShake Instance { get; private set; }

        private Camera2D _camera;
        private FastNoiseLite _noise;
        private float _shakeIntensity;
        private float _shakeDuration;
        private float _shakeTimer;
        private float _noiseOffset;
        private float _intensityScale = 1f;

        public override void _Ready() {
            Instance = this;
            _noise = new FastNoiseLite();
            _noise.Seed = (int)GD.Randi();
            _noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
            _noise.Frequency = 4.0f;
            _intensityScale = SaveManager.Instance?.GlobalData?.ScreenShakeScale ?? 1f;
        }

        public void SetIntensityScale(float scale) => _intensityScale = Mathf.Clamp(scale, 0f, 1f);

        public void Shake(float intensity, float duration) {
            _shakeIntensity = Mathf.Max(_shakeIntensity, intensity * _intensityScale);
            _shakeDuration = Mathf.Max(_shakeDuration, duration);
            _shakeTimer = _shakeDuration;
        }

        public override void _Process(double delta) {
            if (_shakeTimer <= 0) return;

            // M-24: never trust a camera cached across scene changes — the first
            // scene's camera is freed with it, and dereferencing the disposed
            // wrapper threw every shake frame for the rest of the session.
            // Re-resolving is cheap and only happens while a shake is active.
            if (_camera == null || !IsInstanceValid(_camera)) _camera = GetViewport().GetCamera2D();
            if (_camera == null) return;

            float dt = (float)delta;
            _shakeTimer -= dt;
            float decay = Mathf.Exp(-5f * (1f - _shakeTimer / _shakeDuration));
            float intensity = _shakeIntensity * decay;

            _noiseOffset += dt * 100f;
            float offsetX = _noise.GetNoise2D(_noiseOffset, 0) * intensity;
            float offsetY = _noise.GetNoise2D(0, _noiseOffset) * intensity;
            _camera.Offset = ScreenStableOffset(_camera, new Vector2(offsetX, offsetY));

            if (_shakeTimer <= 0) {
                _camera.Offset = Vector2.Zero;
                _shakeIntensity = 0;
                _shakeDuration = 0;
            }
        }

        /// <summary>
        /// GU-B (gameplay update 2026-10-12): <see cref="Camera2D.Offset"/> is in
        /// world pixels, which the camera's zoom magnifies on screen. The Story
        /// camera is zoomed in (<c>StoryCameraRules.ViewZoom</c>, eased out to
        /// frame a boss), so its offset is divided by its current zoom and a shake
        /// reads the same size on screen as it did at the old 1.0 view. Shake
        /// intensities stay authored in world pixels; the Fighter camera's offset
        /// is unchanged.
        /// </summary>
        internal static Vector2 ScreenStableOffset(Camera2D camera, Vector2 worldOffset) {
            if (camera is not FTT.Environment.StoryCameraConfiner story) return worldOffset;
            Vector2 zoom = story.Zoom;
            return new Vector2(worldOffset.X / Mathf.Max(0.01f, zoom.X), worldOffset.Y / Mathf.Max(0.01f, zoom.Y));
        }
    }
}
