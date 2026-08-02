using Godot;

namespace FTT.Core {

    public partial class AudioManager : Node {
        public static AudioManager Instance { get; private set; }

        private AudioStreamPlayer _musicPlayer;
        private AudioStreamPlayer _ambientPlayer;
        private AudioStreamPlayer _combatPlayer;

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
            var player = new AudioStreamPlayer();
            player.Stream = stream;
            player.Bus = "SFX";
            AddChild(player);
            player.Play();
            player.Finished += () => player.QueueFree();
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
