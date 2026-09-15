using System;
using Gw2.Protocol.Session;
using Gw2.Protocol.Transport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Gw2.Protocol.Tests
{
    // Session state machine + handshake/control frame codec (build 205.780).
    // See research/contracts/notes/Protocol/session-state.md.
    [TestClass]
    public class SessionTests
    {
        private static byte[] Seq(int n, byte start = 0)
        {
            var b = new byte[n];
            for (int i = 0; i < n; i++) b[i] = (byte)(start + i);
            return b;
        }

        private static byte[] ServerKeyFrame(byte[] serverValue)
        {
            var frame = new byte[HandshakeFrame.ServerKeyFrameLength];
            frame[0] = (byte)HandshakeKind.ServerKey;
            frame[1] = HandshakeFrame.ServerKeyFrameLength;
            serverValue.CopyTo(frame.AsSpan(2));
            return frame;
        }

        [TestMethod]
        public void ClientHello_RoundTrips()
        {
            byte[] value = Seq(0x40);
            byte[] frame = HandshakeFrame.EncodeClientHello(value);
            Assert.AreEqual(0x42, frame.Length);
            Assert.AreEqual((byte)HandshakeKind.ClientHello, frame[0]);
            Assert.AreEqual(0x42, frame[1]);
            CollectionAssert.AreEqual(value, frame.AsSpan(2).ToArray());

            byte[] small = HandshakeFrame.EncodeClientHello(Seq(4));
            Assert.AreEqual(6, small.Length);
            Assert.AreEqual(6, small[1]);
        }

        [TestMethod]
        public void ClientHello_TooLong_Throws()
        {
            Assert.ThrowsExactly<ArgumentException>(() => HandshakeFrame.EncodeClientHello(new byte[0x41]));
        }

        [TestMethod]
        public void TryClassify_KindsAndBounds()
        {
            Assert.IsTrue(HandshakeFrame.TryClassify(new byte[] { 0, 2 }, out HandshakeKind hello));
            Assert.AreEqual(HandshakeKind.ClientHello, hello);

            Assert.IsTrue(HandshakeFrame.TryClassify(new byte[] { 1, 0x16 }, out HandshakeKind key));
            Assert.AreEqual(HandshakeKind.ServerKey, key);

            Assert.IsTrue(HandshakeFrame.TryClassify(new byte[] { 2, 0x0a }, out HandshakeKind err));
            Assert.AreEqual(HandshakeKind.ServerError, err);

            Assert.IsFalse(HandshakeFrame.TryClassify(new byte[] { 3, 2 }, out _));
            Assert.IsFalse(HandshakeFrame.TryClassify(new byte[] { 0 }, out _));
        }

        [TestMethod]
        public void ServerKey_DecodesAndValidates()
        {
            byte[] value = Seq(0x14, 0xA0);
            Assert.IsTrue(HandshakeFrame.TryDecodeServerKey(ServerKeyFrame(value), out byte[] decoded));
            CollectionAssert.AreEqual(value, decoded);

            byte[] wrongKind = ServerKeyFrame(value);
            wrongKind[0] = (byte)HandshakeKind.ServerError;
            Assert.IsFalse(HandshakeFrame.TryDecodeServerKey(wrongKind, out _));

            byte[] wrongLength = ServerKeyFrame(value);
            wrongLength[1] = 0x15;
            Assert.IsFalse(HandshakeFrame.TryDecodeServerKey(wrongLength, out _));

            Assert.IsFalse(HandshakeFrame.TryDecodeServerKey(new byte[0x15], out _));
        }

        [TestMethod]
        public void DeriveTransportKey_Xor()
        {
            byte[] server = Seq(0x14, 1);
            byte[] secret = Seq(0x14, 0x80);
            byte[] key = HandshakeFrame.DeriveTransportKey(server, secret);
            Assert.AreEqual(0x14, key.Length);
            for (int i = 0; i < key.Length; i++) Assert.AreEqual((byte)(server[i] ^ secret[i]), key[i]);

            Assert.ThrowsExactly<ArgumentException>(() => HandshakeFrame.DeriveTransportKey(new byte[0x13], secret));
            Assert.ThrowsExactly<ArgumentException>(() => HandshakeFrame.DeriveTransportKey(server, new byte[0x13]));
        }

        [TestMethod]
        public void SessionPhase_Transitions()
        {
            var phase = new SessionPhase();
            Assert.AreEqual(MsgConnMode.Initial, phase.Mode);
            Assert.IsFalse(phase.CanSendEncrypted);
            Assert.IsFalse(phase.BuffersHandshake);

            phase.ApplySetMode(true);
            Assert.AreEqual(MsgConnMode.ClientStart, phase.Mode);
            Assert.IsTrue(phase.BuffersHandshake);

            phase.ApplySetMode(false);
            Assert.AreEqual(MsgConnMode.Encrypted, phase.Mode);
            Assert.IsTrue(phase.CanSendEncrypted);
        }

        [TestMethod]
        public void SessionPhase_Establish_KeysTheCipher()
        {
            var phase = new SessionPhase();
            byte[] serverValue = Seq(0x14, 0x10);
            byte[] sharedSecret = Seq(0x40, 0x40); // outA, only the first 20 bytes are used

            Assert.IsTrue(phase.TryEstablish(ServerKeyFrame(serverValue), sharedSecret, out byte[] key));
            Assert.AreEqual(MsgConnMode.Encrypted, phase.Mode);
            Assert.IsTrue(phase.CanSendEncrypted);

            for (int i = 0; i < 0x14; i++) Assert.AreEqual((byte)(serverValue[i] ^ sharedSecret[i]), key[i]);

            // The derived key keys the transport cipher and round-trips.
            var state = new TransportCipherState();
            TransportCipher.KeySchedule(key, state);
            byte[] plain = Seq(32, 7);
            byte[] cipher = TransportCipher.Crypt(state, plain);
            var state2 = new TransportCipherState();
            TransportCipher.KeySchedule(key, state2);
            CollectionAssert.AreEqual(plain, TransportCipher.Crypt(state2, cipher));
        }

        [TestMethod]
        public void SessionPhase_Establish_RejectsWrongStateOrFrame()
        {
            var inClientStart = new SessionPhase();
            inClientStart.ApplySetMode(true);
            Assert.IsFalse(inClientStart.TryEstablish(
                ServerKeyFrame(Seq(0x14)), Seq(0x14), out byte[] key));
            Assert.IsNull(key);
            Assert.AreEqual(MsgConnMode.ClientStart, inClientStart.Mode);

            var inInitial = new SessionPhase();
            Assert.IsFalse(inInitial.TryEstablish(new byte[] { 2, 0x16, 1 }, Seq(0x14), out key));
            Assert.IsNull(key);
            Assert.AreEqual(MsgConnMode.Initial, inInitial.Mode);
        }
    }
}
