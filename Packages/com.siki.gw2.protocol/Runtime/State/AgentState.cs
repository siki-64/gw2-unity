using System;
using System.Collections.Generic;
using System.Numerics;
using Gw2.Protocol.MsgPack;

namespace Gw2.Protocol.State
{
    /// <summary>
    /// The motion block of an AgWorld object (build 205.780, object <c>+0x88</c>, <c>0x38</c> bytes)
    /// as the wire carries it: four contiguous floats, a <c>u32</c>, two optional four-float vectors
    /// and two flag bytes (<c>AgUtil::FUN_14102b2c0</c>).
    /// <para>
    /// The **semantic roles** (which floats are position vs velocity/rotation) are not yet proven, so
    /// the fields are named by wire shape, not by meaning.
    /// </para>
    /// </summary>
    public sealed class MotionState
    {
        /// <summary>The 16-byte blob's four floats (object <c>+0x88</c>).</summary>
        public Vector4 Vector0 { get; internal set; }

        /// <summary>The motion varint (object <c>+0x98</c>).</summary>
        public uint Flags { get; internal set; }

        /// <summary>The first optional four-float vector (object <c>+0x9c</c>), or null.</summary>
        public Vector4? Vector1 { get; internal set; }

        /// <summary>The second optional four-float vector (object <c>+0xac</c>), or null.</summary>
        public Vector4? Vector2 { get; internal set; }

        /// <summary>The first trailing flag byte (object <c>+0xbc</c>).</summary>
        public byte Byte0 { get; internal set; }

        /// <summary>The second trailing flag byte (object <c>+0xbd</c>).</summary>
        public byte Byte1 { get; internal set; }
    }

    /// <summary>
    /// Minimal per-agent (AgWorld object) state for build 205.780: the object kind, the last
    /// transform/orientation and the motion block. Keyed by <see cref="AgentNetworkId"/>.
    /// </summary>
    public sealed class AgentState
    {
        public AgentState(AgentNetworkId networkId)
        {
            NetworkId = networkId;
        }

        public AgentNetworkId NetworkId { get; }

        /// <summary>The object kind (last message applied).</summary>
        public uint Kind { get; internal set; }

        /// <summary>The last modelled message id applied to this agent.</summary>
        public int LastMessageId { get; internal set; }

        /// <summary>First orientation axis (a 12-byte unit vec3), if the message carried it.</summary>
        public Vector3? OrientationA { get; internal set; }

        /// <summary>Second orientation axis (a 12-byte unit vec3), if the message carried it.</summary>
        public Vector3? OrientationB { get; internal set; }

        /// <summary>The motion block.</summary>
        public MotionState Motion { get; } = new MotionState();
    }

    /// <summary>
    /// Applies decoded AgWorld content messages to per-agent state (build 205.780). Currently the
    /// transform message <c>0x39</c> (kind <c>0x11</c>): two orientation vec3s and the motion block.
    /// See <c>research/contracts/notes/Protocol/handler-to-subsystem.md</c>.
    /// <para>
    /// The object's position is inside the motion block, but its exact field is not yet proven; the
    /// runtime preserves the wire-shaped motion rather than naming a position.
    /// </para>
    /// </summary>
    public sealed class AgentStateStore
    {
        /// <summary>The transform/orientation message.</summary>
        public const int TransformMessageId = 0x39;

        /// <summary>The AgWorld object kind the transform message creates (<c>0x11</c>).</summary>
        public const uint TransformKind = 0x11;

        private readonly Dictionary<uint, AgentState> _agents = new Dictionary<uint, AgentState>();

        public IReadOnlyCollection<AgentState> Agents => _agents.Values;

        public int AgentCount => _agents.Count;

        public AgentState GetOrCreate(AgentNetworkId id)
        {
            if (!_agents.TryGetValue(id.Value, out AgentState agent))
            {
                agent = new AgentState(id);
                _agents.Add(id.Value, agent);
            }
            return agent;
        }

        public bool TryGet(AgentNetworkId id, out AgentState agent) =>
            _agents.TryGetValue(id.Value, out agent);

        /// <summary>Forget an agent (despawn). Returns true when it existed.</summary>
        public bool Remove(AgentNetworkId id) => _agents.Remove(id.Value);

        public void Clear() => _agents.Clear();

        /// <summary>Apply one decoded message; returns the number of changed state fields.</summary>
        public int Apply(DecodedMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            switch (message.MessageId)
            {
                case TransformMessageId:
                    return ApplyTransform(message);
                default:
                    return 0;
            }
        }

        /// <summary>Apply a batch; returns the total number of changed state fields.</summary>
        public int Apply(IReadOnlyList<DecodedMessage> messages)
        {
            if (messages == null) throw new ArgumentNullException(nameof(messages));
            int changes = 0;
            for (int i = 0; i < messages.Count; i++) changes += Apply(messages[i]);
            return changes;
        }

        // 0x39: [msgid, u16 networkId, varint, u8, varint, blob, optional{motion},
        //        12-byte orientationA, 12-byte orientationB, optional]
        private int ApplyTransform(DecodedMessage message)
        {
            IReadOnlyList<DecodedField> fields = message.Fields;
            if (fields == null || fields.Count < 9)
                throw new FormatException(
                    $"msgpack: 0x39 record has {fields?.Count ?? 0} fields, expected at least 9");

            var id = new AgentNetworkId((uint)AsUInt(fields[1], "networkId"));
            AgentState agent = GetOrCreate(id);
            agent.Kind = TransformKind;
            agent.LastMessageId = TransformMessageId;

            int changes = 0;
            changes += SetOrientationA(agent, ReadVec3(fields[7], "orientationA"));
            changes += SetOrientationB(agent, ReadVec3(fields[8], "orientationB"));
            changes += ApplyMotion(agent.Motion, fields[6]);
            return changes;
        }

        private static int SetOrientationA(AgentState agent, Vector3 value)
        {
            if (agent.OrientationA.HasValue && agent.OrientationA.Value.Equals(value)) return 0;
            agent.OrientationA = value;
            return 1;
        }

        private static int SetOrientationB(AgentState agent, Vector3 value)
        {
            if (agent.OrientationB.HasValue && agent.OrientationB.Value.Equals(value)) return 0;
            agent.OrientationB = value;
            return 1;
        }

        private static int ApplyMotion(MotionState motion, DecodedField field)
        {
            if (field == null || !(field.Value is DecodedField[] block) || block.Length < 6)
                throw new FormatException("msgpack: 0x39 motion block is malformed");

            Vector4 v0 = ReadVec4(block[0], "motion.vector0");
            uint flags = (uint)AsUInt(block[1], "motion.flags");
            Vector4? v1 = ReadOptionalVec4(block[2], "motion.vector1");
            Vector4? v2 = ReadOptionalVec4(block[3], "motion.vector2");
            byte b0 = (byte)AsUInt(block[4], "motion.byte0");
            byte b1 = (byte)AsUInt(block[5], "motion.byte1");

            int changes = 0;
            if (!motion.Vector0.Equals(v0)) { motion.Vector0 = v0; changes++; }
            if (motion.Flags != flags) { motion.Flags = flags; changes++; }
            if (!Nullable.Equals(motion.Vector1, v1)) { motion.Vector1 = v1; changes++; }
            if (!Nullable.Equals(motion.Vector2, v2)) { motion.Vector2 = v2; changes++; }
            if (motion.Byte0 != b0) { motion.Byte0 = b0; changes++; }
            if (motion.Byte1 != b1) { motion.Byte1 = b1; changes++; }
            return changes;
        }

        private static Vector4? ReadOptionalVec4(DecodedField field, string name)
        {
            if (field == null || !field.Present) return null;
            if (!(field.Value is DecodedField[] sub) || sub.Length < 1)
                throw new FormatException($"msgpack: {name} optional is malformed");
            return ReadVec4(sub[0], name);
        }

        private static Vector4 ReadVec4(DecodedField field, string name)
        {
            byte[] bytes = AsBytes(field, name, 0x10);
            return new Vector4(ReadFloat(bytes, 0), ReadFloat(bytes, 4), ReadFloat(bytes, 8), ReadFloat(bytes, 12));
        }

        private static Vector3 ReadVec3(DecodedField field, string name)
        {
            byte[] bytes = AsBytes(field, name, 0x0C);
            return new Vector3(ReadFloat(bytes, 0), ReadFloat(bytes, 4), ReadFloat(bytes, 8));
        }

        private static float ReadFloat(byte[] b, int off)
        {
            int bits = b[off] | (b[off + 1] << 8) | (b[off + 2] << 16) | (b[off + 3] << 24);
            return BitConverter.Int32BitsToSingle(bits);
        }

        private static ulong AsUInt(DecodedField field, string name)
        {
            if (field == null || !(field.Value is ulong value))
                throw new FormatException($"msgpack: field '{name}' is not an unsigned integer");
            return value;
        }

        private static byte[] AsBytes(DecodedField field, string name, int length)
        {
            if (field == null || !(field.Value is byte[] value) || value.Length != length)
                throw new FormatException($"msgpack: field '{name}' is not a {length}-byte blob");
            return value;
        }
    }
}
