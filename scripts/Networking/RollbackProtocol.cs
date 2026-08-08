using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using FTT.Core;
using FTT.FighterSim;

namespace FTT.Networking {

    public readonly struct RollbackInputPacket {
        public const uint ProtocolMagic = 0x46545452;
        public const ushort ProtocolVersion = 2;
        public const int SerializedSize = 47;
        public readonly uint SessionID;
        public readonly uint Sequence;
        public readonly uint AcknowledgedSequence;
        public readonly byte PlayerID;
        public readonly int Tick;
        public readonly PlayerInputFrame Input;
        public readonly int HashTick;
        public readonly long StateHash;

        public RollbackInputPacket(
            uint sessionID,
            uint sequence,
            uint acknowledgedSequence,
            byte playerID,
            int tick,
            PlayerInputFrame input,
            int hashTick,
            long stateHash) {
            SessionID = sessionID;
            Sequence = sequence;
            AcknowledgedSequence = acknowledgedSequence;
            PlayerID = playerID;
            Tick = tick;
            Input = input;
            HashTick = hashTick;
            StateHash = stateHash;
        }

        public byte[] Serialize() {
            byte[] bytes = new byte[SerializedSize];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, ProtocolMagic);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), ProtocolVersion);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(6), SessionID);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(10), Sequence);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(14), AcknowledgedSequence);
            bytes[18] = PlayerID;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(19), Tick);
            Input.WriteTo(bytes.AsSpan(23, PlayerInputFrame.SerializedSize));
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(35), HashTick);
            BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(39), StateHash);
            return bytes;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> bytes, out RollbackInputPacket packet) {
            packet = default;
            if (bytes.Length != SerializedSize
                || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != ProtocolMagic
                || BinaryPrimitives.ReadUInt16LittleEndian(bytes[4..]) != ProtocolVersion) return false;
            uint sessionID = BinaryPrimitives.ReadUInt32LittleEndian(bytes[6..]);
            uint sequence = BinaryPrimitives.ReadUInt32LittleEndian(bytes[10..]);
            uint acknowledged = BinaryPrimitives.ReadUInt32LittleEndian(bytes[14..]);
            byte playerID = bytes[18];
            if (playerID > 1) return false;
            int tick = BinaryPrimitives.ReadInt32LittleEndian(bytes[19..]);
            PlayerInputFrame input = PlayerInputFrame.Deserialize(bytes.Slice(23, PlayerInputFrame.SerializedSize));
            int hashTick = BinaryPrimitives.ReadInt32LittleEndian(bytes[35..]);
            long hash = BinaryPrimitives.ReadInt64LittleEndian(bytes[39..]);
            packet = new RollbackInputPacket(sessionID, sequence, acknowledged, playerID, tick, input, hashTick, hash);
            return true;
        }
    }

    public interface IRollbackTransport : IDisposable {
        bool IsConnected { get; }
        void Send(byte[] packet);
        void Poll();
        bool TryReceive(out byte[] packet);
    }

    public sealed class InMemoryRollbackTransport : IRollbackTransport {
        private readonly Queue<ScheduledPacket> _pending = new();
        private readonly Queue<byte[]> _received = new();
        private readonly int _latencyPolls;
        private InMemoryRollbackTransport _peer;
        private int _poll;
        private bool _disposed;

        private InMemoryRollbackTransport(int latencyPolls) {
            _latencyPolls = Math.Max(0, latencyPolls);
        }

        public bool IsConnected => !_disposed && _peer != null && !_peer._disposed;

        public static (InMemoryRollbackTransport First, InMemoryRollbackTransport Second) CreatePair(int latencyPolls = 0) {
            var first = new InMemoryRollbackTransport(latencyPolls);
            var second = new InMemoryRollbackTransport(latencyPolls);
            first._peer = second;
            second._peer = first;
            return (first, second);
        }

        public void Send(byte[] packet) {
            if (!IsConnected || packet == null) return;
            _peer._pending.Enqueue(new ScheduledPacket(_peer._poll + _latencyPolls + 1, (byte[])packet.Clone()));
        }

        public void Poll() {
            if (_disposed) return;
            _poll++;
            while (_pending.Count > 0 && _pending.Peek().DeliveryPoll <= _poll) {
                _received.Enqueue(_pending.Dequeue().Bytes);
            }
        }

        public bool TryReceive(out byte[] packet) {
            if (_received.Count > 0) {
                packet = _received.Dequeue();
                return true;
            }
            packet = null;
            return false;
        }

        public void Dispose() {
            _disposed = true;
            _pending.Clear();
            _received.Clear();
        }

        private readonly struct ScheduledPacket {
            public readonly int DeliveryPoll;
            public readonly byte[] Bytes;
            public ScheduledPacket(int deliveryPoll, byte[] bytes) {
                DeliveryPoll = deliveryPoll;
                Bytes = bytes;
            }
        }
    }

    /// <summary>Direct-IP LAN datagram transport. Steam P2P/relay implements the same contract.</summary>
    public sealed class UdpRollbackTransport : IRollbackTransport {
        private readonly Socket _socket;
        private readonly Queue<byte[]> _received = new();
        private EndPoint _remoteEndpoint;
        private readonly bool _acceptFirstPeer;
        private bool _disposed;

        private UdpRollbackTransport(Socket socket, EndPoint remoteEndpoint, bool acceptFirstPeer) {
            _socket = socket;
            _remoteEndpoint = remoteEndpoint;
            _acceptFirstPeer = acceptFirstPeer;
            _socket.Blocking = false;
        }

        public bool IsConnected => !_disposed && _remoteEndpoint != null;

        public static UdpRollbackTransport Host(int localPort) {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Any, localPort));
            return new UdpRollbackTransport(socket, null, true);
        }

        public static UdpRollbackTransport Join(string host, int port, int localPort = 0) {
            IPAddress[] addresses = Dns.GetHostAddresses(host);
            IPAddress address = Array.Find(addresses, candidate => candidate.AddressFamily == AddressFamily.InterNetwork)
                ?? throw new SocketException((int)SocketError.HostNotFound);
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Any, localPort));
            return new UdpRollbackTransport(socket, new IPEndPoint(address, port), false);
        }

        public void Send(byte[] packet) {
            if (!IsConnected || packet == null || packet.Length == 0) return;
            _socket.SendTo(packet, SocketFlags.None, _remoteEndpoint);
        }

        public void Poll() {
            if (_disposed) return;
            while (_socket.Available > 0) {
                byte[] buffer = new byte[RollbackInputPacket.SerializedSize];
                EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                try {
                    int count = _socket.ReceiveFrom(buffer, SocketFlags.None, ref sender);
                    if (count != buffer.Length) continue;
                    if (_remoteEndpoint == null && _acceptFirstPeer) _remoteEndpoint = sender;
                    if (_remoteEndpoint is IPEndPoint expected && sender is IPEndPoint actual && !expected.Equals(actual)) continue;
                    _received.Enqueue(buffer);
                } catch (SocketException exception) when (exception.SocketErrorCode == SocketError.WouldBlock) {
                    break;
                }
            }
        }

        public bool TryReceive(out byte[] packet) {
            if (_received.Count > 0) {
                packet = _received.Dequeue();
                return true;
            }
            packet = null;
            return false;
        }

        public void Dispose() {
            if (_disposed) return;
            _disposed = true;
            _socket.Dispose();
            _received.Clear();
        }
    }

    /// <summary>
    /// Pure formatting helpers for rollback diagnostics. Kept engine-free so the
    /// wording is testable without a Godot runtime; <see cref="NetworkManager"/>
    /// is the production consumer.
    /// </summary>
    public static class RollbackDiagnostics {
        public static string FormatDesync(int tick, long localHash, long remoteHash) =>
            $"Rollback desync at confirmed tick {tick}: local state hash {localHash} != remote state hash {remoteHash}.";
    }

    public sealed class OnlineRollbackSession : IDisposable {
        public const int MaximumRollbackFrames = 7;
        public const double RollbackBudgetMilliseconds = 8.0;
        /// <summary>
        /// Slots retained for corrected-tick de-duplication. One more than the
        /// rollback depth limit so a tick and the tick exactly one window older
        /// can never share a slot; anything older is rejected before it is
        /// recorded. Bounding this is what keeps a 480 s match allocation-free.
        /// </summary>
        private const int CorrectedTickSlots = MaximumRollbackFrames + 1;
        private readonly FighterSimulation _simulation;
        private readonly IRollbackTransport _transport;
        private readonly uint _sessionID;
        private readonly int _localPlayerID;
        private readonly int _remotePlayerID;
        private readonly Dictionary<int, PlayerInputFrame> _remoteInputs = new();
        // Ring of already-corrected ticks, stored as tick + 1 so 0 means empty.
        // Replaces an unbounded HashSet that grew for the whole match.
        private readonly int[] _correctedTicks = new int[CorrectedTickSlots];
        private uint _outgoingSequence;
        private uint _latestRemoteSequence;

        public event Action<int, long, long> DesyncDetected;
        public event Action<int> InputArrivedTooLate;
        public event Action<int, double> RollbackBudgetExceeded;
        /// <summary>
        /// Fired for every applied correction with its rollback depth and the
        /// measured resimulation cost in milliseconds, whether or not the cost
        /// exceeded <see cref="RollbackBudgetMilliseconds"/>. Diagnostics only —
        /// it carries no simulation state.
        /// </summary>
        public event Action<int, double> RollbackCorrectionMeasured;

        public OnlineRollbackSession(
            FighterSimulation simulation,
            IRollbackTransport transport,
            uint sessionID,
            int localPlayerID) {
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            if (localPlayerID is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(localPlayerID));
            _sessionID = sessionID;
            _localPlayerID = localPlayerID;
            _remotePlayerID = localPlayerID == 0 ? 1 : 0;
        }

        public FighterSimulation Simulation => _simulation;
        public bool IsConnected => _transport.IsConnected;
        public int LocalPlayerID => _localPlayerID;

        /// <summary>
        /// Corrected-tick records currently retained. Bounded by
        /// <see cref="CorrectedTickSlots"/> for the whole match length.
        /// </summary>
        public int RetainedCorrectedTickCount {
            get {
                int count = 0;
                for (int slot = 0; slot < _correctedTicks.Length; slot++) {
                    if (_correctedTicks[slot] != 0) count++;
                }
                return count;
            }
        }

        /// <summary>Upper bound on <see cref="RetainedCorrectedTickCount"/>.</summary>
        public static int CorrectedTickCapacity => CorrectedTickSlots;

        public long Advance(PlayerInputFrame localInput) {
            int tick = _simulation.CurrentTick;
            localInput.Tick = (uint)tick;
            SendLocalInput(tick, localInput);
            PumpNetwork();

            if (_remoteInputs.Remove(tick, out PlayerInputFrame remoteInput)) {
                return _localPlayerID == 0
                    ? _simulation.Advance(localInput, remoteInput)
                    : _simulation.Advance(remoteInput, localInput);
            }
            return _simulation.AdvanceWithPredictedRemote(_localPlayerID, localInput);
        }

        public void PumpNetwork() {
            _transport.Poll();
            while (_transport.TryReceive(out byte[] bytes)) {
                if (!RollbackInputPacket.TryDeserialize(bytes, out RollbackInputPacket packet)
                    || packet.SessionID != _sessionID
                    || packet.PlayerID != _remotePlayerID) continue;
                if (packet.Sequence > _latestRemoteSequence) _latestRemoteSequence = packet.Sequence;
                CheckRemoteHash(in packet);
                int currentTick = _simulation.CurrentTick;
                if (packet.Tick >= currentTick) {
                    _remoteInputs[packet.Tick] = packet.Input;
                    continue;
                }
                int rollbackDepth = currentTick - packet.Tick;
                if (rollbackDepth > MaximumRollbackFrames) {
                    InputArrivedTooLate?.Invoke(packet.Tick);
                    continue;
                }
                if (!TryRecordCorrectedTick(packet.Tick)) continue;
                var stopwatch = Stopwatch.StartNew();
                _simulation.CorrectRemoteInput(_remotePlayerID, packet.Tick, packet.Input);
                stopwatch.Stop();
                double elapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
                RollbackCorrectionMeasured?.Invoke(rollbackDepth, elapsedMilliseconds);
                if (elapsedMilliseconds > RollbackBudgetMilliseconds) {
                    RollbackBudgetExceeded?.Invoke(rollbackDepth, elapsedMilliseconds);
                }
            }
        }

        /// <summary>
        /// Records a tick as corrected, returning false when the same tick was
        /// already corrected inside the live rollback window. Only ticks within
        /// <see cref="MaximumRollbackFrames"/> of the current tick reach here, so
        /// a fixed ring keeps de-duplication exact while staying bounded.
        /// </summary>
        private bool TryRecordCorrectedTick(int tick) {
            int slot = tick % CorrectedTickSlots;
            if (slot < 0) slot += CorrectedTickSlots;
            int record = tick + 1;
            if (_correctedTicks[slot] == record) return false;
            _correctedTicks[slot] = record;
            return true;
        }

        private void SendLocalInput(int tick, PlayerInputFrame localInput) {
            // Exchange only hashes older than the rollback window. Newer hashes may
            // still contain predicted input and would create false desync reports.
            int hashTick = tick - MaximumRollbackFrames - 1;
            long hash = hashTick >= 0 ? _simulation.GetRecordedHash(hashTick) : 0L;
            var packet = new RollbackInputPacket(
                _sessionID,
                ++_outgoingSequence,
                _latestRemoteSequence,
                (byte)_localPlayerID,
                tick,
                localInput,
                hashTick,
                hash);
            _transport.Send(packet.Serialize());
        }

        private void CheckRemoteHash(in RollbackInputPacket packet) {
            if (packet.HashTick < 0 || packet.StateHash == 0) return;
            long localHash = _simulation.GetRecordedHash(packet.HashTick);
            if (localHash != 0 && localHash != packet.StateHash) {
                DesyncDetected?.Invoke(packet.HashTick, localHash, packet.StateHash);
            }
        }

        public void Dispose() => _transport.Dispose();
    }
}
