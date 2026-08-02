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

        public override void _Ready() {
            Instance = this;
            _noise = new FastNoiseLite();
            _noise.Seed = (int)GD.Randi();
            _noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
            _noise.Frequency = 4.0f;
        }

        public void Shake(float intensity, float duration) {
            _shakeIntensity = Mathf.Max(_shakeIntensity, intensity);
            _shakeDuration = Mathf.Max(_shakeDuration, duration);
            _shakeTimer = _shakeDuration;
        }

        public override void _Process(double delta) {
            if (_shakeTimer <= 0) return;

            _camera ??= GetViewport().GetCamera2D();
            if (_camera == null) return;

            float dt = (float)delta;
            _shakeTimer -= dt;
            float decay = Mathf.Exp(-5f * (1f - _shakeTimer / _shakeDuration));
            float intensity = _shakeIntensity * decay;

            _noiseOffset += dt * 100f;
            float offsetX = _noise.GetNoise2D(_noiseOffset, 0) * intensity;
            float offsetY = _noise.GetNoise2D(0, _noiseOffset) * intensity;
            _camera.Offset = new Vector2(offsetX, offsetY);

            if (_shakeTimer <= 0) {
                _camera.Offset = Vector2.Zero;
                _shakeIntensity = 0;
                _shakeDuration = 0;
            }
        }
    }
}
