using System;
using System.IO;
using System.Text.Json;
using Gw2.Protocol.Transport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Gw2.Protocol.Tests
{
    [TestClass]
    public class TransportCipherTests
    {

        private static JsonDocument Load(string name) =>
            JsonDocument.Parse(Fixtures.ReadBytes(name));

        private static string Hex(ReadOnlySpan<byte> b) => Convert.ToHexString(b).ToLowerInvariant();

        private static int HexInt(string s) => Convert.ToInt32(s, 16);

        [TestMethod]
        public void ReferenceVectors_Match()
        {
            using var doc = Load("transport-cipher.json");
            int count = 0;
            foreach (var v in doc.RootElement.GetProperty("vectors").EnumerateArray())
            {
                byte[] key = Convert.FromHexString(v.GetProperty("inputKey").GetString());
                Assert.AreEqual(v.GetProperty("mixedKey").GetString(),
                    Hex(TransportCipher.DeriveKey(key)), "mixedKey");

                var st = new TransportCipherState();
                TransportCipher.KeySchedule(key, st);
                Assert.AreEqual(v.GetProperty("sboxAfterKsa").GetString(),
                    Hex(st.CopySBox()), "sboxAfterKsa");

                Assert.AreEqual(v.GetProperty("keystream32").GetString(),
                    Hex(TransportCipher.Crypt(st, new byte[32])), "keystream32");
                count++;
            }
            Assert.AreEqual(3, count, "expected three vectors");
        }

        [TestMethod]
        public void OverlongKey_ClampsTo20()
        {
            var k = new byte[25];
            for (int i = 0; i < k.Length; i++) k[i] = (byte)(i * 7 + 3);
            Assert.AreEqual(Hex(TransportCipher.DeriveKey(k.AsSpan(0, 20))),
                Hex(TransportCipher.DeriveKey(k)));
        }

        [TestMethod]
        public void StateCarriesAcrossCalls()
        {
            var key = new byte[20];
            var data = new byte[128];
            for (int i = 0; i < 20; i++) key[i] = (byte)i;
            for (int i = 0; i < data.Length; i++) data[i] = (byte)i;

            var whole = new TransportCipherState();
            TransportCipher.KeySchedule(key, whole);
            byte[] a = TransportCipher.Crypt(whole, data);

            var split = new TransportCipherState();
            TransportCipher.KeySchedule(key, split);
            var b = new byte[128];
            TransportCipher.Crypt(split, data.AsSpan(0, 64), b.AsSpan(0, 64));
            TransportCipher.Crypt(split, data.AsSpan(64), b.AsSpan(64));
            Assert.AreEqual(Hex(a), Hex(b));
        }

        [TestMethod]
        public void CapturedState_Decrypts0264WireToTransportFrame()
        {
            using var doc = Load("0x264-wire.json");
            var root = doc.RootElement;
            byte[] wire = Convert.FromHexString(root.GetProperty("wireHex").GetString());
            var cs = root.GetProperty("cipherState");
            int i = HexInt(cs.GetProperty("i").GetString());
            int j = HexInt(cs.GetProperty("j").GetString());
            byte[] sbox = Convert.FromHexString(cs.GetProperty("sboxHex").GetString());

            var st = TransportCipherState.FromCapturedState(i, j, sbox);
            byte[] frame = TransportCipher.Crypt(st, wire);
            Assert.AreEqual(root.GetProperty("transportFrameHex").GetString(), Hex(frame));
        }

        [TestMethod]
        public void CapturedState_RejectsMalformedInput()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => TransportCipherState.FromCapturedState(0, 0, new byte[255]));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => TransportCipherState.FromCapturedState(256, 0, new byte[256]));
        }
    }
}
