using System;
using System.Numerics;

namespace Gw2.Protocol.Session
{
    /// <summary>
    /// The client's Diffie-Hellman material for one handshake (build 205.780). Both values are 64
    /// bytes, little-endian, as stored by the client.
    /// </summary>
    public readonly struct HandshakeKeyPair
    {
        /// <summary>
        /// The shared secret (<c>outA = G^R mod P</c>). The client stores its first 20 bytes at
        /// <c>conn+0x118</c>; that prefix is what the transport key is XOR-ed against.
        /// </summary>
        public byte[] SharedSecret { get; }

        /// <summary>The public value (<c>outB = 4^R mod P</c>), sent to the server in the client hello.</summary>
        public byte[] PublicValue { get; }

        internal HandshakeKeyPair(byte[] sharedSecret, byte[] publicValue)
        {
            SharedSecret = sharedSecret;
            PublicValue = publicValue;
        }

        /// <summary>The first 20 bytes of <see cref="SharedSecret"/> (<c>conn+0x118</c>).</summary>
        public byte[] SharedSecretPrefix()
        {
            var prefix = new byte[0x14];
            Array.Copy(SharedSecret, prefix, prefix.Length);
            return prefix;
        }
    }

    /// <summary>
    /// The build 205.780 handshake key derivation (<c>MsgConn.cpp</c>, <c>FUN_140fede80</c>):
    /// <c>R = FUN_141570640(seed, 512)</c>, <c>outA = G^R mod P</c>, <c>outB = 4^R mod P</c>.
    /// See <c>research/contracts/notes/Protocol/handshake-key-derivation.md</c>.
    /// <para>
    /// This is a **Diffie-Hellman exchange**, not a hash: the exponent is expanded from a fresh
    /// client seed (<c>FUN_140fdf2d0</c>, not on the wire) with a Park-Miller LCG, and <c>P</c> is a
    /// 512-bit probable prime, so <c>R</c> is not recoverable from <c>outB</c>. The seed generator
    /// is platform-specific and is not modelled here: pass a seed (a replay value or fresh entropy).
    /// </para>
    /// </summary>
    public static class HandshakeKeyExchange
    {
        /// <summary>Seed length in bytes (the client expands a 20-byte seed).</summary>
        public const int SeedLength = 0x14;

        /// <summary>Modulus/public-value length in bytes.</summary>
        public const int ValueLength = 0x40;

        /// <summary>Park-Miller multiplier (48271).</summary>
        private const uint PmA = 0xBC8F;

        /// <summary>Park-Miller Schrage quotient (44488).</summary>
        private const uint PmQ = 0xADC8;

        private const uint PmMask = 0x7FFFFFFF;
        private const uint XorSeed = 0x075BD924;

        // DAT_142109e40, little-endian as stored (`P` then `G`).
        private const string ModulusHex =
            "f9e43af525f1b41fa263a00748cdf14898ef8f59783e8fcd70b41db94afd3d0a" +
            "97ac56571c3b5ee7c4a8804f63961fd84158e553f1ed49cb4c470b72598573f9";
        private const string GeneratorHex =
            "270e7b5849658baf6bbadfe4a86ee1f00596f819b970eeb9fcb2080241c28b09" +
            "8ff4737ccd996175482465084eb44ff89567c6fbd6a3e6a56292623dc358a6c4";

        /// <summary>
        /// Expand a seed to the 512-bit exponent <c>R</c>, returned as 64 little-endian bytes
        /// (<c>FUN_141570640</c>: 16 output words, each two Park-Miller LCG steps).
        /// </summary>
        public static byte[] ExpandExponent(ReadOnlySpan<byte> seed)
        {
            if (seed.Length == 0)
                throw new ArgumentException("seed must be non-empty", nameof(seed));

            uint[] words = ExpandExponentWords(seed);
            var bytes = new byte[words.Length * 4];
            for (int i = 0; i < words.Length; i++)
            {
                bytes[i * 4 + 0] = (byte)words[i];
                bytes[i * 4 + 1] = (byte)(words[i] >> 8);
                bytes[i * 4 + 2] = (byte)(words[i] >> 16);
                bytes[i * 4 + 3] = (byte)(words[i] >> 24);
            }
            return bytes;
        }

        /// <summary>Derive the shared secret <c>outA</c> and public value <c>outB</c> from a seed.</summary>
        public static HandshakeKeyPair Derive(ReadOnlySpan<byte> seed)
        {
            BigInteger r = Decode(ExpandExponent(seed));
            BigInteger p = Decode(ParseHex(ModulusHex));
            BigInteger g = Decode(ParseHex(GeneratorHex));

            BigInteger shared = BigInteger.ModPow(g, r, p);
            BigInteger publicValue = BigInteger.ModPow(4, r, p);
            return new HandshakeKeyPair(
                ToLittleEndian(shared, ValueLength),
                ToLittleEndian(publicValue, ValueLength));
        }

        private static uint[] ExpandExponentWords(ReadOnlySpan<byte> seed)
        {
            const int wordCount = 16; // 512 bits

            var limbs = new uint[(seed.Length + 3) / 4];
            for (int i = 0; i < seed.Length; i++)
                limbs[i >> 2] |= (uint)seed[i] << ((i & 3) * 8);

            int size = limbs.Length;
            while (size > 0 && limbs[size - 1] == 0) size--;
            if (size == 0)
            {
                limbs = new uint[] { 0 };
                size = 1;
            }

            var output = new uint[wordCount];
            int idx = 0;
            for (int k = 0; k < wordCount; k++)
            {
                uint x = (k == idx ? XorSeed : 0u) ^ limbs[idx];
                uint word = 0;
                uint state = 0;
                for (int step = 0; step < 2; step++)
                {
                    x = (uint)(((x / PmQ) + (ulong)x * PmA) & 0xFFFFFFFF);
                    state = x & PmMask;
                    word |= (x & 0xFFFF) << (16 * step);
                    x = state;
                }
                output[k] = word;
                limbs[idx] = state;
                idx = (idx + 1) % size;
            }
            return output;
        }

        private static BigInteger Decode(ReadOnlySpan<byte> littleEndian) =>
            new BigInteger(littleEndian, isUnsigned: true, isBigEndian: false);

        private static byte[] ToLittleEndian(BigInteger value, int length)
        {
            byte[] raw = value.ToByteArray(isUnsigned: true, isBigEndian: false);
            var result = new byte[length];
            Array.Copy(raw, result, Math.Min(raw.Length, length));
            return result;
        }

        private static byte[] ParseHex(string hex)
        {
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = (byte)((Nibble(hex[i * 2]) << 4) | Nibble(hex[i * 2 + 1]));
            return bytes;
        }

        private static int Nibble(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            return char.ToLowerInvariant(c) - 'a' + 10;
        }
    }
}
