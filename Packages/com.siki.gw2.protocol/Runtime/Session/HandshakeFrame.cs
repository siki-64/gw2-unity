using System;

namespace Gw2.Protocol.Session
{
    /// <summary>
    /// The frame kinds in the <c>MsgConn</c> handshake/control space (build 205.780): the three-entry
    /// dispatch table indexed by the frame's first byte. This space is only used before the transport
    /// cipher is keyed (modes 1/2); established traffic is not framed this way.
    /// </summary>
    public enum HandshakeKind : byte
    {
        /// <summary>
        /// `0` - the client's own Diffie-Hellman public value. The client sends it and never receives
        /// it, so its inbound handler is `Msg::Raw::RecvInvalid` (always fails).
        /// </summary>
        ClientHello = 0,

        /// <summary>`1` - the server's key-delivery frame (`Msg::Raw::ClientRecvEncrypt`).</summary>
        ServerKey = 1,

        /// <summary>`2` - a server error frame (`Msg::Raw::ClientRecvError`).</summary>
        ServerError = 2,
    }

    /// <summary>
    /// The <c>MsgConn</c> handshake/control frame codec, recovered for build 205.780
    /// (<c>MsgConn.cpp</c>: the constructor, <c>MsgRaw_ClientRecvEncrypt</c>,
    /// <c>MsgRaw_ClientRecvError</c>). See
    /// <c>research/contracts/notes/Protocol/session-state.md</c>.
    /// <para>
    /// A frame is <c>[u8 kind][u8 length][payload]</c>, where <c>length</c> counts the whole frame.
    /// </para>
    /// </summary>
    public static class HandshakeFrame
    {
        /// <summary>Maximum client public-value length; the in-image assert clamps at <c>0x40</c>.</summary>
        public const int MaxClientHelloValue = 0x40;

        /// <summary>The server key frame's length byte (<c>0x16</c>): a two-byte header plus 20 bytes.</summary>
        public const int ServerKeyFrameLength = 0x16;

        /// <summary>The handshake key-material length in bytes (the transport key width).</summary>
        public const int KeyMaterialLength = 0x14;

        /// <summary>
        /// Build the client hello the constructor sends: <c>[0x00][len+2][value]</c>. This send
        /// bypasses the transport cipher. The frame shape is a static reconstruction; no captured
        /// frame validates it yet.
        /// </summary>
        public static byte[] EncodeClientHello(ReadOnlySpan<byte> publicValue)
        {
            if (publicValue.Length > MaxClientHelloValue)
                throw new ArgumentException(
                    $"client public value must be <= 0x{MaxClientHelloValue:x} bytes", nameof(publicValue));

            var frame = new byte[2 + publicValue.Length];
            frame[0] = (byte)HandshakeKind.ClientHello;
            frame[1] = (byte)(publicValue.Length + 2);
            publicValue.CopyTo(frame.AsSpan(2));
            return frame;
        }

        /// <summary>Classify a handshake/control frame by its kind byte.</summary>
        public static bool TryClassify(ReadOnlySpan<byte> frame, out HandshakeKind kind)
        {
            kind = default;
            if (frame.Length < 2) return false;
            if (frame[0] > (byte)HandshakeKind.ServerError) return false;
            kind = (HandshakeKind)frame[0];
            return true;
        }

        /// <summary>
        /// Decode the server key frame (<c>[0x01][0x16][20 bytes]</c>) and return the 20-byte server
        /// value, which the transport key is XOR-ed against. Validated live
        /// (msg-dispatch-addenda Addendum 26).
        /// </summary>
        public static bool TryDecodeServerKey(ReadOnlySpan<byte> frame, out byte[] serverValue)
        {
            serverValue = null;
            if (frame.Length < ServerKeyFrameLength) return false;
            if (frame[0] != (byte)HandshakeKind.ServerKey) return false;
            if (frame[1] != ServerKeyFrameLength) return false;
            serverValue = frame.Slice(2, KeyMaterialLength).ToArray();
            return true;
        }

        /// <summary>
        /// The transport key is the 20-byte server value XOR the first 20 bytes of the client's
        /// Diffie-Hellman shared secret (<c>conn+0x118</c>, i.e. <c>outA[:20]</c>).
        /// </summary>
        public static byte[] DeriveTransportKey(ReadOnlySpan<byte> serverValue, ReadOnlySpan<byte> sharedSecret)
        {
            if (serverValue.Length < KeyMaterialLength)
                throw new ArgumentException("server value must be at least 20 bytes", nameof(serverValue));
            if (sharedSecret.Length < KeyMaterialLength)
                throw new ArgumentException("shared secret must be at least 20 bytes", nameof(sharedSecret));

            var key = new byte[KeyMaterialLength];
            for (int i = 0; i < KeyMaterialLength; i++) key[i] = (byte)(serverValue[i] ^ sharedSecret[i]);
            return key;
        }
    }
}
