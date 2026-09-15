using System;
using System.IO;
using System.Text.Json;
using Gw2.Protocol.Session;
using Gw2.Protocol.Transport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Gw2.Protocol.Tests
{
    // Handshake key derivation: seed -> R, outA, outB.
    // See research/contracts/notes/Protocol/handshake-key-derivation.md.
    [TestClass]
    public class HandshakeKdfTests
    {
        private static JsonDocument Load() =>
            JsonDocument.Parse(Fixtures.ReadBytes("handshake-kdf.json"));

        private static byte[] Hex(string s)
        {
            var b = new byte[s.Length / 2];
            for (int i = 0; i < b.Length; i++) b[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
            return b;
        }

        private static string HexOf(ReadOnlySpan<byte> b) => BitConverter.ToString(b.ToArray()).Replace("-", "").ToLowerInvariant();

        private static string ReverseHex(ReadOnlySpan<byte> littleEndian)
        {
            byte[] c = littleEndian.ToArray();
            Array.Reverse(c);
            return HexOf(c);
        }

        // R is stored big-endian in the fixture; outA/outB little-endian.
        [TestMethod]
        public void KdfVectors_RoundTrip()
        {
            using JsonDocument doc = Load();
            JsonElement root = doc.RootElement;
            JsonElement vectors = root.GetProperty("vectors");
            Assert.AreEqual(3, vectors.GetArrayLength());

            foreach (JsonElement v in vectors.EnumerateArray())
            {
                byte[] seed = Hex(v.GetProperty("seed").GetString());
                HandshakeKeyPair keys = HandshakeKeyExchange.Derive(seed);

                Assert.AreEqual(v.GetProperty("R").GetString(),
                    ReverseHex(HandshakeKeyExchange.ExpandExponent(seed)), "R");
                Assert.AreEqual(v.GetProperty("outA").GetString(), HexOf(keys.SharedSecret), "outA");
                Assert.AreEqual(v.GetProperty("outB").GetString(), HexOf(keys.PublicValue), "outB");
            }
        }

        [TestMethod]
        public void Kdf_CapturedVector_MatchesConn118()
        {
            using JsonDocument doc = Load();
            JsonElement captured = doc.RootElement.GetProperty("capturedVector");

            byte[] seed = Hex(captured.GetProperty("seed").GetString());
            byte[] expected = Hex(captured.GetProperty("outA20").GetString());

            HandshakeKeyPair keys = HandshakeKeyExchange.Derive(seed);
            Assert.AreEqual(HandshakeKeyExchange.SeedLength, seed.Length);
            CollectionAssert.AreEqual(expected, keys.SharedSecretPrefix());
        }

        [TestMethod]
        public void ExpandExponent_IsDeterministic_AndBound()
        {
            byte[] seed = Hex("000102030405060708090a0b0c0d0e0f10111213");
            CollectionAssert.AreEqual(HandshakeKeyExchange.ExpandExponent(seed), HandshakeKeyExchange.ExpandExponent(seed));
            Assert.AreEqual(HandshakeKeyExchange.ValueLength, HandshakeKeyExchange.ExpandExponent(seed).Length);
            Assert.ThrowsExactly<ArgumentException>(() => HandshakeKeyExchange.ExpandExponent(ReadOnlySpan<byte>.Empty));
        }

        // End to end: derive keys, send the hello, accept the server key, key the transport cipher.
        [TestMethod]
        public void Handshake_EndToEnd_KeysTheCipher()
        {
            byte[] seed = Hex("0558904f44f11d2eb022eb43b0a25441b6c0d800");
            HandshakeKeyPair keys = HandshakeKeyExchange.Derive(seed);

            byte[] hello = HandshakeFrame.EncodeClientHello(keys.PublicValue);
            Assert.AreEqual((byte)HandshakeKind.ClientHello, hello[0]);
            Assert.AreEqual(2 + HandshakeKeyExchange.ValueLength, (int)hello[1]);

            byte[] serverValue = new byte[0x14];
            for (int i = 0; i < serverValue.Length; i++) serverValue[i] = (byte)(i * 7 + 1);
            var serverFrame = new byte[HandshakeFrame.ServerKeyFrameLength];
            serverFrame[0] = (byte)HandshakeKind.ServerKey;
            serverFrame[1] = HandshakeFrame.ServerKeyFrameLength;
            serverValue.CopyTo(serverFrame.AsSpan(2));

            var phase = new SessionPhase();
            Assert.IsTrue(phase.TryEstablish(serverFrame, keys.SharedSecretPrefix(), out byte[] transportKey));
            Assert.AreEqual(MsgConnMode.Encrypted, phase.Mode);
            CollectionAssert.AreEqual(HandshakeFrame.DeriveTransportKey(serverValue, keys.SharedSecretPrefix()), transportKey);

            var enc = new TransportCipherState();
            TransportCipher.KeySchedule(transportKey, enc);
            byte[] plain = { 1, 2, 3, 4, 5 };
            byte[] cipher = TransportCipher.Crypt(enc, plain);

            var dec = new TransportCipherState();
            TransportCipher.KeySchedule(transportKey, dec);
            CollectionAssert.AreEqual(plain, TransportCipher.Crypt(dec, cipher));
        }
    }
}
