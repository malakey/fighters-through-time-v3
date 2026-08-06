using Godot;
using System.Collections.Generic;

namespace FTT.Core {

    public partial class AudioManager : Node {
        public static AudioManager Instance { get; private set; }

        private AudioStreamPlayer _musicPlayer;
        private AudioStreamPlayer _ambientPlayer;
        private AudioStreamPlayer _combatPlayer;
        private const int SfxPoolCapacity = 24;
        private readonly Queue<AudioStreamPlayer> _inactiveSfx = new();
        private readonly List<AudioStreamPlayer> _activeSfxOrder = new();
        private readonly HashSet<AudioStreamPlayer> _activeSfx = new();

        private int _masterBus;
        private int _musicBus;
        private int _sfxBus;
        private int _uiBus;
        private int _ambientBus;

        public override void _Ready() {
            Instance = this;

            _masterBus = AudioServer.GetBusIndex("Master");
            _musicBus = GetOrCreateBus("Music");
            _sfxBus = GetOrCreateBus("SFX");
            _uiBus = GetOrCreateBus("UI");
            _ambientBus = GetOrCreateBus("Ambient");

            _musicPlayer = new AudioStreamPlayer();
            _musicPlayer.Bus = "Music";
            AddChild(_musicPlayer);

            _ambientPlayer = new AudioStreamPlayer();
            _ambientPlayer.Bus = "Music";
            AddChild(_ambientPlayer);

            _combatPlayer = new AudioStreamPlayer();
            _combatPlayer.Bus = "Music";
            AddChild(_combatPlayer);

            for (int index = 0; index < SfxPoolCapacity; index++) {
                var player = new AudioStreamPlayer { Name = $"PooledSFX_{index}", Bus = "SFX" };
                AudioStreamPlayer captured = player;
                player.Finished += () => ReleaseSfx(captured);
                AddChild(player);
                _inactiveSfx.Enqueue(player);
            }
        }

        private int GetOrCreateBus(string busName) {
            int idx = AudioServer.GetBusIndex(busName);
            if (idx >= 0) return idx;
            AudioServer.AddBus();
            int newIdx = AudioServer.BusCount - 1;
            AudioServer.SetBusName(newIdx, busName);
            AudioServer.SetBusSend(newIdx, "Master");
            return newIdx;
        }

        public void SetMasterVolume(float linear) {
            AudioServer.SetBusVolumeDb(_masterBus, Mathf.LinearToDb(linear));
        }

        public void SetMusicVolume(float linear) {
            AudioServer.SetBusVolumeDb(_musicBus, Mathf.LinearToDb(linear));
        }

        public void SetSFXVolume(float linear) {
            AudioServer.SetBusVolumeDb(_sfxBus, Mathf.LinearToDb(linear));
        }

        public void SetUIVolume(float linear) {
            AudioServer.SetBusVolumeDb(_uiBus, Mathf.LinearToDb(linear));
        }

        public void PlayMusic(AudioStream stream, float fadeDuration = 1.0f) {
            _musicPlayer.Stream = stream;
            _musicPlayer.Play();
        }

        public void PlaySFX(AudioStream stream, Vector2 position = default) {
            if (stream == null) return;
            AudioStreamPlayer player;
            if (_inactiveSfx.Count > 0) {
                player = _inactiveSfx.Dequeue();
            } else {
                player = _activeSfxOrder[0];
                _activeSfxOrder.RemoveAt(0);
                _activeSfx.Remove(player);
                player.Stop();
            }
            player.Stream = stream;
            player.VolumeDb = 0f;
            player.PitchScale = 1f;
            _activeSfx.Add(player);
            _activeSfxOrder.Add(player);
            player.Play();
        }

        private void ReleaseSfx(AudioStreamPlayer player) {
            if (player == null || !_activeSfx.Remove(player)) return;
            _activeSfxOrder.Remove(player);
            player.Stop();
            player.Stream = null;
            _inactiveSfx.Enqueue(player);
        }

        public void TransitionToCombatStem() {
            _combatPlayer.VolumeDb = Mathf.LinearToDb(1.0f);
            _ambientPlayer.VolumeDb = Mathf.LinearToDb(0.3f);
        }

        public void TransitionToAmbientStem() {
            _combatPlayer.VolumeDb = Mathf.LinearToDb(0.0f);
            _ambientPlayer.VolumeDb = Mathf.LinearToDb(1.0f);
        }
    }
}
