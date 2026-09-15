using System;
using System.Collections.Generic;
using System.Numerics;
using Gw2.Protocol.MsgPack;
using Gw2.Protocol.State;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Gw2.Protocol.Tests
{
    // Minimal per-agent (AgWorld) state for the current build: message 0x39 (kind 0x11).
    [TestClass]
    public class AgentStateTests
    {
        private static byte[] Floats(params float[] values)
        {
            var bytes = new byte[values.Length * 4];
            for (int i = 0; i < values.Length; i++)
                BitConverter.GetBytes(values[i]).CopyTo(bytes, i * 4);
            return bytes;
        }

        private static DecodedField Vec3(float x, float y, float z) =>
            new DecodedField(0x08, true, Floats(x, y, z));

        private static DecodedField Vec4(float x, float y, float z, float w) =>
            new DecodedField(0x09, true, Floats(x, y, z, w));

        private static DecodedField OptionalVec4(float x, float y, float z, float w) =>
            new DecodedField(0x0f, true, new[] { Vec4(x, y, z, w) });

        private static DecodedField NoVec4() => new DecodedField(0x0f, false, null);

        private static DecodedMessage Transform(uint networkId,
            Vector3 orientA, Vector3 orientB,
            Vector4 vector0, uint flags, Vector4? vector1, Vector4? vector2,
            byte byte0, byte byte1)
        {
            DecodedField motion = new DecodedField(0x0f, true, new[]
            {
                Vec4(vector0.X, vector0.Y, vector0.Z, vector0.W),
                new DecodedField(4, true, (ulong)flags),
                vector1.HasValue ? OptionalVec4(vector1.Value.X, vector1.Value.Y, vector1.Value.Z, vector1.Value.W) : NoVec4(),
                vector2.HasValue ? OptionalVec4(vector2.Value.X, vector2.Value.Y, vector2.Value.Z, vector2.Value.W) : NoVec4(),
                new DecodedField(0x02, true, (ulong)byte0),
                new DecodedField(0x02, true, (ulong)byte1),
            });

            return new DecodedMessage(AgentStateStore.TransformMessageId, 0, 0, new List<DecodedField>
            {
                new DecodedField(1, true, (ulong)AgentStateStore.TransformMessageId),
                new DecodedField(3, true, (ulong)networkId),
                new DecodedField(4, true, 7UL),
                new DecodedField(2, true, 3UL),
                new DecodedField(4, true, 0UL),
                new DecodedField(0x14, true, new byte[] { 1, 2, 3 }),
                motion,
                Vec3(orientA.X, orientA.Y, orientA.Z),
                Vec3(orientB.X, orientB.Y, orientB.Z),
                new DecodedField(0x0f, false, null),
            });
        }

        [TestMethod]
        public void Transform_SetsOrientationAndMotion()
        {
            var store = new AgentStateStore();
            DecodedMessage message = Transform(
                0x1234, new Vector3(1, 0, 0), new Vector3(0, 1, 0),
                new Vector4(10, 20, 30, 1), 0x55, new Vector4(1, 2, 3, 4), null, 7, 9);

            Assert.AreEqual(7, store.Apply(message)); // 2 orientation + 5 changed motion fields

            Assert.IsTrue(store.TryGet(new AgentNetworkId(0x1234), out AgentState agent));
            Assert.AreEqual(AgentStateStore.TransformKind, agent.Kind);
            Assert.AreEqual(AgentStateStore.TransformMessageId, agent.LastMessageId);
            Assert.AreEqual(new Vector3(1, 0, 0), agent.OrientationA);
            Assert.AreEqual(new Vector3(0, 1, 0), agent.OrientationB);
            Assert.AreEqual(new Vector4(10, 20, 30, 1), agent.Motion.Vector0);
            Assert.AreEqual(0x55u, agent.Motion.Flags);
            Assert.AreEqual(new Vector4(1, 2, 3, 4), agent.Motion.Vector1);
            Assert.IsNull(agent.Motion.Vector2);
            Assert.AreEqual(7, agent.Motion.Byte0);
            Assert.AreEqual(9, agent.Motion.Byte1);
        }

        [TestMethod]
        public void Transform_IsChangeTracked()
        {
            var store = new AgentStateStore();
            DecodedMessage message = Transform(
                1, new Vector3(0, 0, 1), new Vector3(1, 0, 0),
                new Vector4(1, 1, 1, 0), 0, null, null, 0, 0);

            Assert.AreEqual(3, store.Apply(message)); // 2 orientation + vector0
            Assert.AreEqual(0, store.Apply(message));
            Assert.AreEqual(1, store.AgentCount);
        }

        [TestMethod]
        public void Transform_Malformed_Throws()
        {
            var store = new AgentStateStore();
            var empty = new DecodedMessage(AgentStateStore.TransformMessageId, 0, 0, new List<DecodedField>());
            Assert.ThrowsExactly<FormatException>(() => store.Apply(empty));

            // A motion block with the wrong element count is rejected.
            var badMotion = new DecodedMessage(AgentStateStore.TransformMessageId, 0, 0, new List<DecodedField>
            {
                new DecodedField(1, true, (ulong)AgentStateStore.TransformMessageId),
                new DecodedField(3, true, 1UL),
                new DecodedField(4, true, 0UL),
                new DecodedField(2, true, 0UL),
                new DecodedField(4, true, 0UL),
                new DecodedField(0x14, true, new byte[] { 1 }),
                new DecodedField(0x0f, true, new[] { Vec4(0, 0, 0, 0) }),
                Vec3(0, 0, 0),
                Vec3(0, 0, 0),
                new DecodedField(0x0f, false, null),
            });
            Assert.ThrowsExactly<FormatException>(() => store.Apply(badMotion));
        }

        [TestMethod]
        public void Lifecycle_GetOrCreate_Remove()
        {
            var store = new AgentStateStore();
            var id = new AgentNetworkId(0x99);
            AgentState agent = store.GetOrCreate(id);
            Assert.AreSame(agent, store.GetOrCreate(id));
            Assert.AreEqual(1, store.AgentCount);

            Assert.IsTrue(store.Remove(id));
            Assert.IsFalse(store.Remove(id));
            Assert.AreEqual(0, store.AgentCount);
        }

        [TestMethod]
        public void NonModelledMessage_IsIgnored()
        {
            var store = new AgentStateStore();
            var other = new DecodedMessage(0x33, 0, 0, new List<DecodedField>());
            Assert.AreEqual(0, store.Apply(other));
            Assert.AreEqual(0, store.AgentCount);
        }
    }
}
