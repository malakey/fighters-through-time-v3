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
            State = NetworkConnectionState.Connected;
            return Session;
        }

        public void Disconnect() {
            if (Session != null) {
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
