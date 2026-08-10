using System;
using System.Buffers.Binary;

namespace FTT.Core {

    [Flags]
    public enum GameplayButtons : ushort {
        None = 0,
        Jump = 1 << 0,
        Down = 1 << 1,
        BasicAttack = 1 << 2,
        Special1 = 1 << 3,
        Special2 = 1 << 4,
        MovementAbility = 1 << 5,
        Block = 1 << 6,
        Ultimate = 1 << 7,
        Interact = 1 << 8,
        Pause = 1 << 9,
        Roll = 1 << 10,
        /// <summary>
        /// Reserved wire bit. The universal dash mechanic (and its derived
        /// double-tap/flick gesture) was removed on 2026-08-09 by user directive;
        /// the bit stays allocated so the protocol v2 packet layout is unchanged.
        /// Nothing sets it and nothing consumes it.
        /// </summary>
        Dash = 1 << 11
    }

    /// <summary>
    /// The complete, deterministic command consumed by one player for one 60 Hz tick.
    /// Axes are quantized to signed bytes so the same representation can be recorded,
    /// transmitted, predicted, and replayed without floating-point drift.
    /// </summary>
    [Serializable]
    public struct PlayerInputFrame : IEquatable<PlayerInputFrame> {
        public const int SerializedSize = 12;

        public uint Tick;
        public sbyte MoveX;
        public sbyte MoveY;
        public GameplayButtons Held;
        public GameplayButtons Pressed;
        public GameplayButtons Released;

        public readonly float Horizontal => MoveX / 127.0f;
        public readonly float Vertical => MoveY / 127.0f;

        public readonly bool IsHeld(GameplayButtons button) => (Held & button) != 0;
        public readonly bool IsPressed(GameplayButtons button) => (Pressed & button) != 0;
        public readonly bool IsReleased(GameplayButtons button) => (Released & button) != 0;

        public static PlayerInputFrame Create(
            uint tick,
            float horizontal,
            float vertical,
            GameplayButtons held,
            GameplayButtons previousHeld = GameplayButtons.None) {
            return new PlayerInputFrame {
                Tick = tick,
                MoveX = QuantizeAxis(horizontal),
                MoveY = QuantizeAxis(vertical),
                Held = held,
                Pressed = held & ~previousHeld,
                Released = previousHeld & ~held
            };
        }

        public readonly byte[] Serialize() {
            byte[] bytes = new byte[SerializedSize];
            WriteTo(bytes);
            return bytes;
        }

        public readonly void WriteTo(Span<byte> destination) {
            if (destination.Length < SerializedSize) {
                throw new ArgumentException($"Input frame requires {SerializedSize} bytes.", nameof(destination));
            }

            BinaryPrimitives.WriteUInt32LittleEndian(destination, Tick);
            destination[4] = unchecked((byte)MoveX);
            destination[5] = unchecked((byte)MoveY);
            BinaryPrimitives.WriteUInt16LittleEndian(destination[6..], (ushort)Held);
            BinaryPrimitives.WriteUInt16LittleEndian(destination[8..], (ushort)Pressed);
            BinaryPrimitives.WriteUInt16LittleEndian(destination[10..], (ushort)Released);
        }

        public static PlayerInputFrame Deserialize(ReadOnlySpan<byte> source) {
            if (source.Length < SerializedSize) {
                throw new ArgumentException($"Input frame requires {SerializedSize} bytes.", nameof(source));
            }

            return new PlayerInputFrame {
                Tick = BinaryPrimitives.ReadUInt32LittleEndian(source),
                MoveX = unchecked((sbyte)source[4]),
                MoveY = unchecked((sbyte)source[5]),
                Held = (GameplayButtons)BinaryPrimitives.ReadUInt16LittleEndian(source[6..]),
                Pressed = (GameplayButtons)BinaryPrimitives.ReadUInt16LittleEndian(source[8..]),
                Released = (GameplayButtons)BinaryPrimitives.ReadUInt16LittleEndian(source[10..])
            };
        }

        private static sbyte QuantizeAxis(float value) {
            float clamped = Math.Clamp(value, -1.0f, 1.0f);
            return (sbyte)MathF.Round(clamped * 127.0f, MidpointRounding.AwayFromZero);
        }

        public readonly bool Equals(PlayerInputFrame other) {
            return Tick == other.Tick
                && MoveX == other.MoveX
                && MoveY == other.MoveY
                && Held == other.Held
                && Pressed == other.Pressed
                && Released == other.Released;
        }

        public override readonly bool Equals(object obj) => obj is PlayerInputFrame other && Equals(other);
        public override readonly int GetHashCode() => HashCode.Combine(Tick, MoveX, MoveY, Held, Pressed, Released);
    }

    public interface IPlayerInputSource {
        PlayerInputFrame Sample(uint tick, in PlayerInputFrame previousFrame);
    }

    /// <summary>Injectable source used by CPU, replay, rollback prediction, and tests.</summary>
    public sealed class BufferedInputSource : IPlayerInputSource {
        private PlayerInputFrame _nextFrame;

        public void SetNextFrame(PlayerInputFrame frame) => _nextFrame = frame;

        public PlayerInputFrame Sample(uint tick, in PlayerInputFrame previousFrame) {
            PlayerInputFrame result = _nextFrame;
            result.Tick = tick;
            _nextFrame = result;
            return result;
        }
    }
}
