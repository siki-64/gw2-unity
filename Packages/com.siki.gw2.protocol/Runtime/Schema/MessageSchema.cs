#nullable enable
using System;
using System.Collections.Generic;

namespace Gw2.Protocol.Schema
{
    /// <summary>
    /// One field of a message schema (<c>MsgPackFieldDef</c>), recovered for build 205.780.
    /// <para>
    /// <see cref="FieldType"/> selects the reader behaviour; <see cref="Param"/> is the element
    /// or array count (and, for <c>MP_MSGID</c>, the message id); <see cref="Reference"/> is the
    /// nested chain used by the struct/array field types.
    /// </para>
    /// </summary>
    public sealed class FieldDefinition
    {
        public FieldDefinition(int fieldType, int param = 0, FieldDefinition[]? reference = null)
        {
            FieldType = fieldType;
            Param = param;
            Reference = reference;
        }

        public int FieldType { get; }
        public int Param { get; }
        public FieldDefinition[]? Reference { get; }
    }

    /// <summary>A message schema: the message id plus its field chain.</summary>
    public sealed class MessageSchema
    {
        public MessageSchema(int messageId, FieldDefinition[] fields)
        {
            MessageId = messageId;
            Fields = fields ?? throw new ArgumentNullException(nameof(fields));
        }

        public int MessageId { get; }
        public FieldDefinition[] Fields { get; }

        /// <summary>
        /// Build a schema from a descriptor chain. The chain must begin with <c>MP_MSGID</c>
        /// (field type 1), whose <c>Param</c> carries the id.
        /// </summary>
        public static MessageSchema FromChain(FieldDefinition[] chain)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (chain.Length == 0 || chain[0].FieldType != 1)
                throw new ArgumentException("schema chain must begin with MP_MSGID (field type 1)", nameof(chain));
            return new MessageSchema(chain[0].Param, chain);
        }
    }

    /// <summary>The set of schemas a connection can dispatch, keyed by message id.</summary>
    public sealed class MessageSchemaSet
    {
        private readonly Dictionary<int, MessageSchema> _byId = new Dictionary<int, MessageSchema>();

        public MessageSchemaSet(IEnumerable<MessageSchema> schemas)
        {
            if (schemas == null) throw new ArgumentNullException(nameof(schemas));
            foreach (var s in schemas) Add(s);
        }

        public int Count => _byId.Count;

        public void Add(MessageSchema schema)
        {
            if (schema == null) throw new ArgumentNullException(nameof(schema));
            _byId[schema.MessageId] = schema;
        }

        public bool TryGet(int messageId, out MessageSchema schema) =>
            _byId.TryGetValue(messageId, out schema!);
    }
}
