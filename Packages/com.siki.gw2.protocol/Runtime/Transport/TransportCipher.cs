using System;

namespace Gw2.Protocol.Transport
{
    /// <summary>
    /// Persistent stream-cipher state for one direction of the game connection.
    /// <para>
    /// The state is <c>(i, j, S[256])</c> and carries across calls, so the cipher is a
    /// stateful keystream generator and cannot be replayed from a single frame.
    /// A captured mid-session state can be loaded with <see cref="FromCapturedState"/>.
    /// </para>
    /// </summary>
    public sealed class TransportCipherState
    {
        internal byte IndexI;
        internal byte IndexJ;
        internal readonly byte[] SBox = new byte[256];

        /// <summary>Current PRGA index i (0..255).</summary>
        public int I => IndexI;

        /// <summary>Current PRGA index j (0..255).</summary>
        public int J => IndexJ;

        /// <summary>A copy of the current S-box.</summary>
        public byte[] CopySBox() => (byte[])SBox.Clone();

        /// <summary>
        /// Load a state captured mid-session (e.g. at <c>conn+0x12C</c> or <c>conn+0x234</c>):
        /// <paramref name="sbox"/> is the 256-byte permutation.
        /// </summary>
        public static TransportCipherState FromCapturedState(int i, int j, ReadOnlySpan<byte> sbox)
        {
            if ((uint)i > 0xFF) throw new ArgumentOutOfRangeException(nameof(i));
            if ((uint)j > 0xFF) throw new ArgumentOutOfRangeException(nameof(j));
            if (sbox.Length != 256) throw new ArgumentException("S-box must be 256 bytes", nameof(sbox));
            var state = new TransportCipherState { IndexI = (byte)i, IndexJ = (byte)j };
            sbox.CopyTo(state.SBox);
            return state;
        }
    }

    /// <summary>
    /// The game-connection transport cipher, recovered for build 205.780
    /// (<c>MsgUtil.cpp</c>: the key schedule and the RC4-shaped PRGA).
    /// <para>
    /// The key schedule zeroes a 20-byte buffer, copies in up to 20 key bytes (longer is
    /// clamped), mixes the five little-endian words with a bespoke four-step transform,
    /// then runs a textbook RC4 KSA over the 20 mixed bytes. The keystream is a textbook
    /// RC4 PRGA. This is not stock RC4: the key is pre-mixed.
    /// </para>
    /// <para>
    /// The 20-byte key here is the <c>MsgConn</c> derived key (<c>conn+0x118</c> masked by the
    /// server handshake value); on the wire the two directions start from the same state,
    /// so supply the state captured for the direction being decoded.
    /// </para>
    /// </summary>
    public static class TransportCipher
    {
        /// <summary>Key material length in bytes.</summary>
        public const int KeyLength = 0x14;

        private const uint C_A0 = 0x9FB498B3u;
        private const uint C_A1 = 0x66B0CD0Du;
        private const uint C_A2_F = 0x7BF36AE2u;
        private const uint C_A2 = 0xF33D5697u;
        private const uint C_A3 = 0xD675E47Bu;
        private const uint C_A3_B = 0x59D148C0u;
        private const uint C_W0 = 0xB453C259u;
        private const uint C_W0_MASK = 0x22222222u;

        private static uint Rol(uint x, int n) => (x << n) | (x >> (32 - n));

        /// <summary>
        /// The five-word mixing (four dependent steps). Exposed for the reference-vector
        /// test; not part of the wire contract.
        /// </summary>
        public static uint[] MixFiveWords(uint w0, uint w1, uint w2, uint w3, uint w4)
        {
            uint a0 = w0 + C_A0;
            uint b0 = Rol(a0, 30);
            uint a1 = w1 + C_A1 + Rol(a0, 5);
            uint b1 = Rol(a1, 30);
            uint f2 = ~(a0 & C_W0_MASK) & C_A2_F;
            uint a2 = Rol(a1, 5) + w2 + f2 + C_A2;
            uint b2 = Rol(a2, 30);
            uint f3 = ((b0 ^ C_A3_B) & a1) ^ C_A3_B;
            uint a3 = Rol(a2, 5) + w3 + f3 + C_A3;
            uint f4 = ((b0 ^ b1) & a2) ^ b0;
            return new[]
            {
                f4 + w0 + w4 + Rol(a3, 5) + C_W0,
                w1 + a3,
                w2 + b2,
                w3 + b1,
                w4 + b0,
            };
        }

        /// <summary>Steps 1 and 2 of the key schedule: returns the 20 mixed key bytes.</summary>
        public static byte[] DeriveKey(ReadOnlySpan<byte> key)
        {
            Span<byte> buf = stackalloc byte[KeyLength];
            buf.Clear();
            int n = Math.Min(key.Length, KeyLength);
            for (int i = 0; i < n; i++) buf[i] = key[i];

            uint[] mixed = MixFiveWords(
                ReadU32(buf, 0), ReadU32(buf, 4), ReadU32(buf, 8), ReadU32(buf, 12), ReadU32(buf, 16));

            var result = new byte[KeyLength];
            for (int i = 0; i < 5; i++)
            {
                result[i * 4 + 0] = (byte)(mixed[i]);
                result[i * 4 + 1] = (byte)(mixed[i] >> 8);
                result[i * 4 + 2] = (byte)(mixed[i] >> 16);
                result[i * 4 + 3] = (byte)(mixed[i] >> 24);
            }
            return result;
        }

        /// <summary>Full schedule: fills <paramref name="state"/> with the KSA S-box and i = j = 0.</summary>
        public static void KeySchedule(ReadOnlySpan<byte> key, TransportCipherState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            byte[] mixed = DeriveKey(key);
            byte[] s = state.SBox;
            for (int i = 0; i < 256; i++) s[i] = (byte)i;
            byte j = 0;
            for (int i = 0; i < 256; i++)
            {
                j = (byte)(j + s[i] + mixed[i % KeyLength]);
                (s[i], s[j]) = (s[j], s[i]);
            }
            state.IndexI = 0;
            state.IndexJ = 0;
        }

        /// <summary>RC4 PRGA; advances <paramref name="state"/> and writes <paramref name="output"/>.</summary>
        public static void Crypt(TransportCipherState state, ReadOnlySpan<byte> input, Span<byte> output)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (output.Length < input.Length)
                throw new ArgumentException("output shorter than input", nameof(output));
            byte[] s = state.SBox;
            byte i = state.IndexI;
            byte j = state.IndexJ;
            for (int k = 0; k < input.Length; k++)
            {
                i = (byte)(i + 1);
                j = (byte)(j + s[i]);
                (s[i], s[j]) = (s[j], s[i]);
                output[k] = (byte)(s[(s[i] + s[j]) & 0xFF] ^ input[k]);
            }
            state.IndexI = i;
            state.IndexJ = j;
        }

        /// <summary>RC4 PRGA returning a new buffer.</summary>
        public static byte[] Crypt(TransportCipherState state, ReadOnlySpan<byte> input)
        {
            var result = new byte[input.Length];
            Crypt(state, input, result);
            return result;
        }

        private static uint ReadU32(ReadOnlySpan<byte> b, int off) =>
            (uint)(b[off] | (b[off + 1] << 8) | (b[off + 2] << 16) | (b[off + 3] << 24));
    }
}
