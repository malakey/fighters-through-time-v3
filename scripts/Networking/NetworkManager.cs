using Godot;
using System.Collections.Generic;

namespace FTT.Networking {

    public struct PlayerSnapshot {
        public Vector2 Position;
        public Vector2 Velocity;
        public int CurrentHP;
        public float UltimateMeter;
        public int CurrentBlockCharges;
        public int RemainingJumps;
        public FTT.Characters.CharacterState State;
        public bool IsFacingRight;
        public int Frame;
    }

    public struct ProjectileSnapshot {
        public string ObjectTypeID;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Lifetime;
        public int OwnerIndex;
    }

    public struct GameStateSnapshot {
        public int Frame;
        public PlayerSnapshot[] Players;
        public ProjectileSnapshot[] Projectiles;
        public uint Checksum;
    }

    public partial class NetworkManager : Node {
        public static NetworkManager Instance { get; private set; }

        public bool IsOnline { get; private set; }
        public bool IsHost { get; private set; }
        public int LocalPlayerIndex { get; private set; }

        private const int MaxRollbackFrames = 7;
        private const int TickRate = 60;
        private Queue<GameStateSnapshot> _snapshotHistory = new();
        private int _currentFrame;

        public override void _Ready() {
            Instance = this;
        }

        public void StartHosting(string roomCode) {
            IsOnline = true;
            IsHost = true;
            LocalPlayerIndex = 0;
        }

        public void JoinRoom(string roomCode) {
            IsOnline = true;
            IsHost = false;
            LocalPlayerIndex = 1;
        }

        public void Disconnect() {
            IsOnline = false;
        }

        public void SaveSnapshot(GameStateSnapshot snapshot) {
            snapshot.Frame = _currentFrame;
            _snapshotHistory.Enqueue(snapshot);
            while (_snapshotHistory.Count > MaxRollbackFrames * 2) {
                _snapshotHistory.Dequeue();
            }
        }

        public GameStateSnapshot? GetSnapshot(int frame) {
            foreach (var snap in _snapshotHistory) {
                if (snap.Frame == frame) return snap;
            }
            return null;
        }

        public uint CalculateChecksum(GameStateSnapshot snapshot) {
            uint hash = 0;
            if (snapshot.Players != null) {
                foreach (var p in snapshot.Players) {
                    hash ^= (uint)(p.Position.X * 1000) ^ (uint)(p.Position.Y * 1000) ^ (uint)p.CurrentHP;
                }
            }
            return hash;
        }

        public void AdvanceFrame() {
            _currentFrame++;
        }

        public int CurrentFrame => _currentFrame;
    }

    public partial class MatchmakingManager : Node {
        public static MatchmakingManager Instance { get; private set; }

        public string CurrentRoomCode { get; private set; }

        public override void _Ready() {
            Instance = this;
        }

        public string CreatePrivateRoom() {
            CurrentRoomCode = GenerateRoomCode();
            NetworkManager.Instance?.StartHosting(CurrentRoomCode);
            return CurrentRoomCode;
        }

        public void JoinPrivateRoom(string code) {
            CurrentRoomCode = code;
            NetworkManager.Instance?.JoinRoom(code);
        }

        public void JoinPublicQueue() {
            // Matchmaking queue - placeholder
        }

        public void LeaveQueue() {
            NetworkManager.Instance?.Disconnect();
            CurrentRoomCode = null;
        }

        private string GenerateRoomCode() {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            char[] code = new char[6];
            var rng = new System.Random();
            for (int i = 0; i < 6; i++) code[i] = chars[rng.Next(chars.Length)];
            return new string(code);
        }
    }
}
