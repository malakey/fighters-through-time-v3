using System;
using System.Security.Cryptography;
using FTT.Core;
using FTT.FighterSim;
using Godot;

namespace FTT.Networking {

    public enum NetworkConnectionState {
        Offline,
        HostingLan,
        JoiningLan,
        Connected,
        Faulted
    }

    public partial class NetworkManager : Node {
        public static NetworkManager Instance { get; private set; }
        public NetworkConnectionState State { get; private set; } = NetworkConnectionState.Offline;
        public int LocalPlayerIndex { get; private set; }
        public string LastError { get; private set; } = "";
        public OnlineRollbackSession Session { get; private set; }
        /// <summary>Confirmed-frame desyncs observed on the active session. Diagnostics only.</summary>
        public int DesyncCount { get; private set; }
        /// <summary>Tick of the most recent desync, or -1 when none has been observed.</summary>
        public int LastDesyncTick { get; private set; } = -1;
        private IRollbackTransport _transport;

        public override void _Ready() => Instance = this;

        public override void _ExitTree() {
            Disconnect();
            if (Instance == this) Instance = null;
        }

        public bool HostLan(int port = 27850) {
            Disconnect();
            try {
                _transport = UdpRollbackTransport.Host(port);
                LocalPlayerIndex = 0;
                State = NetworkConnectionState.HostingLan;
                return true;
            } catch (Exception exception) when (exception is System.Net.Sockets.SocketException || exception is ArgumentException) {
                Fail(exception.Message);
                return false;
            }
        }

        public bool JoinLan(string host, int port = 27850) {
            Disconnect();
            try {
                _transport = UdpRollbackTransport.Join(host, port);
                LocalPlayerIndex = 1;
                State = NetworkConnectionState.JoiningLan;
                return true;
            } catch (Exception exception) when (exception is System.Net.Sockets.SocketException || exception is ArgumentException) {
                Fail(exception.Message);
                return false;
            }
        }

        public OnlineRollbackSession BeginRollback(FighterSimulation simulation, uint sessionID) {
            if (_transport == null) throw new InvalidOperationException("A LAN or platform transport must be connected first.");
            if (Session != null) throw new InvalidOperationException("A rollback session is already active. Disconnect before starting another session.");
            Session = new OnlineRollbackSession(simulation, _transport, sessionID, LocalPlayerIndex);
            DesyncCount = 0;
            LastDesyncTick = -1;
            Session.DesyncDetected += OnDesyncDetected;
            State = NetworkConnectionState.Connected;
            return Session;
        }

        /// <summary>
        /// Minimal production consumer for the desync event: a hard error in the
        /// Godot log with the confirmed tick and both hashes. Full-state resync is
        /// Package 7 work; until then a desync must at least be visible.
        /// </summary>
        private void OnDesyncDetected(int tick, long localHash, long remoteHash) {
            DesyncCount++;
            LastDesyncTick = tick;
            LastError = RollbackDiagnostics.FormatDesync(tick, localHash, remoteHash);
            GD.PushError(LastError);
        }

        public void Disconnect() {
            if (Session != null) {
                Session.DesyncDetected -= OnDesyncDetected;
                Session.Dispose();
            } else {
                _transport?.Dispose();
            }
            Session = null;
            _transport = null;
            State = NetworkConnectionState.Offline;
            LastError = "";
        }

        private void Fail(string error) {
            LastError = error ?? "Unknown network error.";
            State = NetworkConnectionState.Faulted;
        }
    }

    public partial class MatchmakingManager : Node {
        public static MatchmakingManager Instance { get; private set; }
        public string CurrentRoomCode { get; private set; } = "";
        public bool SteamTransportAvailable => false;

        public override void _Ready() => Instance = this;

        public string CreatePrivateRoomCode() {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            Span<byte> random = stackalloc byte[6];
            System.Security.Cryptography.RandomNumberGenerator.Fill(random);
            Span<char> code = stackalloc char[6];
            for (int index = 0; index < code.Length; index++) code[index] = alphabet[random[index] % alphabet.Length];
            CurrentRoomCode = new string(code);
            return CurrentRoomCode;
        }

        public bool JoinPublicQueue() {
            GD.PushWarning("Steam Networking Sockets transport is not installed; public matchmaking is unavailable.");
            return false;
        }

        public void LeaveQueue() {
            NetworkManager.Instance?.Disconnect();
            CurrentRoomCode = "";
        }
    }
}
