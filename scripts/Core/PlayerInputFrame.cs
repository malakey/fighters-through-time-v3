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
        Dash = 1 << 11,
        /// <summary>
        /// C01c (Package 11 A1c): the logical <b>Echo Step</b> request — the verb,
        /// not the keys. Raised by the direct <c>gameplay_echo_step</c> binding,
        /// and by the Block+Roll preset chord when that chord is enabled for the
        /// device. Bits 12-15 were free, so protocol v3 carries all three new flags
        /// without changing <see cref="PlayerInputFrame.SerializedSize"/> (12 B) or
        /// the 47-byte rollback packet.
        /// </summary>
        EchoStep = 1 << 12,
        /// <summary>
        /// C01c (Package 11 A1c): the logical <b>Grab</b> request. Raised by the
        /// direct <c>gameplay_grab</c> binding, and by the Block+BasicAttack preset
        /// chord when that chord is enabled for the device.
        /// </summary>
        Grab = 1 << 13,
        /// <summary>
        /// C01c origin flag: set when the combined-verb requests on this frame came
        /// from <b>direct bindings only</b> — that is, the originating machine's
        /// preset chords are off (or absent) for the device that produced it.
        ///
        /// <para>This is what stops a peer re-recognizing a remote chord with its
        /// own shortcut settings. The simulation accepts a verb from
        /// <c>Pressed(Grab)</c> / <c>Pressed(EchoStep)</c> <em>or</em> from the
        /// component bits, and this flag is the normalized instruction to skip the
        /// second route: the sender already decided, so the component bits on this
        /// frame mean plain Block, Roll and BasicAttack. Serializing the logical
        /// request plus this flag — never local key codes — is what makes two peers
        /// replay identically.</para>
        /// </summary>
        DirectOrigin = 1 << 14
        // Bit 15 is free.
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
        /// <summary>
        /// Vertical intent, positive = down. Synthesized as Down minus Up
        /// (gameplay feel §2.7); Jump is deliberately NOT part of it, so a
        /// negative value means the player is really holding Up. The up-attack
        /// selection and the directional Warp/Blink/Mirage both read it, and
        /// <c>gameplay_up</c> has no button bit precisely because this axis is
        /// its only channel — the wire layout is unchanged.
        /// </summary>
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
