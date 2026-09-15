using System;

namespace Gw2.Protocol.Schema
{
    /// <summary>
    /// A build's schema corpora for both directions. Inbound and outbound have **different** chains
    /// per message id (build 205.780; msg-dispatch Addendum 26), so a consumer must select the
    /// corpus by direction: server-to-client uses <see cref="Recv"/>, client-to-server uses
    /// <see cref="Send"/>.
    /// </summary>
    public sealed class ProtocolSchemaCorpus
    {
        public ProtocolSchemaCorpus(MessageSchemaSet recv, MessageSchemaSet send)
        {
            Recv = recv ?? throw new ArgumentNullException(nameof(recv));
            Send = send ?? throw new ArgumentNullException(nameof(send));
        }

        /// <summary>The server-to-client schema set.</summary>
        public MessageSchemaSet Recv { get; }

        /// <summary>The client-to-server schema set.</summary>
        public MessageSchemaSet Send { get; }

        /// <summary>Load both corpora from their JSON texts.</summary>
        public static ProtocolSchemaCorpus FromJson(string recvJson, string sendJson) =>
            new ProtocolSchemaCorpus(MessageSchemaJson.Parse(recvJson), MessageSchemaJson.Parse(sendJson));

        /// <summary>Select the schema set for a direction. Unknown is rejected.</summary>
        public MessageSchemaSet ForDirection(TrafficDirection direction)
        {
            switch (direction)
            {
                case TrafficDirection.ServerToClient: return Recv;
                case TrafficDirection.ClientToServer: return Send;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction,
                        "schema corpus requires a concrete direction");
            }
        }
    }
}
