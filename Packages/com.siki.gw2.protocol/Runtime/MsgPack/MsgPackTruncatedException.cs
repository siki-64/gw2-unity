using System;

namespace Gw2.Protocol.MsgPack
{
    /// <summary>
    /// Raised when a schema read runs past the end of the available bytes. It is a
    /// <see cref="FormatException"/> so fail-closed callers treat it as malformed, but a streaming
    /// decoder can catch it specifically and wait for more bytes (the message may simply not have
    /// arrived yet).
    /// </summary>
    public sealed class MsgPackTruncatedException : FormatException
    {
        public MsgPackTruncatedException(string message) : base(message) { }
    }
}
