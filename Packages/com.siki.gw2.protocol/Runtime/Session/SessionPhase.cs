using System;

namespace Gw2.Protocol.Session
{
    /// <summary>
    /// The <c>MsgConn</c> mode word (<c>conn+0x108</c>). Only three values are
    /// reachable. See <c>research/contracts/notes/Protocol/session-state.md</c>.
    /// </summary>
    public enum MsgConnMode
    {
        /// <summary>
        /// `1` - `MSGCONN_MODE_CLIENT_START`: inbound frames are buffered as handshake data and are
        /// not dispatched as messages.
        /// </summary>
        ClientStart = 1,

        /// <summary>
        /// `2` - the initial state after the constructor sent the client Diffie-Hellman public value;
        /// the connection awaits the server key frame. Written only by the constructor.
        /// </summary>
        Initial = 2,

        /// <summary>
        /// `3` - `MSGCONN_MODE_ENCRYPTED`: keyed/established; the encrypted receive pipeline and the
        /// send flush run.
        /// </summary>
        Encrypted = 3,
    }

    /// <summary>
    /// The phase machine around the codecs for one game connection. It performs no
    /// I/O: it models the recovered transitions so a connection driver can gate cipher use and
    /// handshake buffering. The transport cipher state itself is supplied separately; on
    /// <see cref="TryEstablish"/> the caller keys both directions from the returned transport key.
    /// </summary>
    public sealed class SessionPhase
    {
        /// <summary>The current mode.</summary>
        public MsgConnMode Mode { get; private set; }

        /// <summary>A new game connection starts in <see cref="MsgConnMode.Initial"/> (constructor).</summary>
        public SessionPhase()
        {
            Mode = MsgConnMode.Initial;
        }

        /// <summary>
        /// <c>MsgConn_SetMode(conn, flag)</c>: yields <see cref="MsgConnMode.ClientStart"/> when the
        /// flag is non-zero, otherwise <see cref="MsgConnMode.Encrypted"/>. It can never produce
        /// <see cref="MsgConnMode.Initial"/>.
        /// </summary>
        public void ApplySetMode(bool flag)
        {
            Mode = flag ? MsgConnMode.ClientStart : MsgConnMode.Encrypted;
        }

        /// <summary>Whether encrypted gameplay may be sent (the outbound flush asserts this).</summary>
        public bool CanSendEncrypted => Mode == MsgConnMode.Encrypted;

        /// <summary>Whether inbound frames are buffered as handshake data instead of dispatched.</summary>
        public bool BuffersHandshake => Mode == MsgConnMode.ClientStart;

        /// <summary>
        /// Apply the server key frame: valid only in <see cref="MsgConnMode.Initial"/>. On success it
        /// decodes the frame, derives the transport key from <paramref name="sharedSecret"/>
        /// (<c>outA[:20]</c>), moves to <see cref="MsgConnMode.Encrypted"/> and returns true. A wrong
        /// mode or a malformed frame returns false and leaves the state unchanged.
        /// </summary>
        public bool TryEstablish(ReadOnlySpan<byte> frame, ReadOnlySpan<byte> sharedSecret, out byte[] transportKey)
        {
            transportKey = null;
            if (Mode != MsgConnMode.Initial) return false;
            if (!HandshakeFrame.TryDecodeServerKey(frame, out byte[] serverValue)) return false;

            transportKey = HandshakeFrame.DeriveTransportKey(serverValue, sharedSecret);
            Mode = MsgConnMode.Encrypted;
            return true;
        }
    }
}
