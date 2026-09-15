using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Gw2.Protocol;
using Gw2.Protocol.MsgPack;
using Gw2.Protocol.Schema;
using Gw2.Protocol.State;
using Gw2.Protocol.Transport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Gw2.Protocol.Tests
{
    // Minimal per-player state (build 205.780): configured-skills from 0x264.
    [TestClass]
    public class PlayerStateTests
    {
        private static string Fixture(string name) =>
            Path.Combine(AppContext.BaseDirectory, "fixtures", name);

        private static ProtocolSchemaCorpus Corpus() => ProtocolSchemaCorpus.FromJson(
            File.ReadAllText(Fixture("chains-recv.json")),
            File.ReadAllText(Fixture("chains-send.json")));

        private static TransportCipherState State(JsonElement cs) => TransportCipherState.FromCapturedState(
            Convert.ToInt32(cs.GetProperty("i").GetString(), 16),
            Convert.ToInt32(cs.GetProperty("j").GetString(), 16),
            Convert.FromHexString(cs.GetProperty("sboxHex").GetString()));

        private static DecodedMessage ConfiguredSkillUpdate(uint skill, byte slot, byte context, uint index)
        {
            var fields = new List<DecodedField>
            {
                new DecodedField(1, true, (ulong)PlayerStateStore.ConfiguredSkillUpdateMessageId),
                new DecodedField(4, true, (ulong)skill),
                new DecodedField(2, true, (ulong)slot),
                new DecodedField(2, true, (ulong)context),
                new DecodedField(4, true, (ulong)index),
            };
            return new DecodedMessage(PlayerStateStore.ConfiguredSkillUpdateMessageId, 0, 0, fields);
        }

        // Full pipeline: captured wire -> cipher -> frame -> schema -> state.
        [TestMethod]
        public void Replay_CapturedWire_UpdatesConfiguredSkills()
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(Fixture("0x264-wire.json")));
            JsonElement root = doc.RootElement;
            byte[] wire = Convert.FromHexString(root.GetProperty("wireHex").GetString());

            var codec = new ProtocolCodec(Corpus(), State(root.GetProperty("cipherState")));
            IReadOnlyList<DecodedMessage> messages = codec.DecodeInbound(wire);
            Assert.AreEqual(2, messages.Count);

            Assert.AreEqual(5, messages[0].Fields.Count);
            Assert.AreEqual(0x1206UL, (ulong)messages[0].Fields[1].Value);
            Assert.AreEqual(2UL, (ulong)messages[0].Fields[2].Value);
            Assert.AreEqual(0UL, (ulong)messages[0].Fields[3].Value);
            Assert.AreEqual(1UL, (ulong)messages[0].Fields[4].Value);

            var store = new PlayerStateStore();
            Assert.AreEqual(2, store.Apply(messages));
            Assert.AreEqual(1, store.PlayerCount);

            Assert.IsTrue(store.TryGet(new PlayerListIndex(1), out PlayerState player));
            Assert.AreEqual(new SkillContentId(0x1206),
                player.GetConfiguredSkill(SkillContextCode.A0, ConfiguredSkillSlot.UtilitySkill2));
            Assert.AreEqual(new SkillContentId(0x122B),
                player.GetConfiguredSkill(SkillContextCode.A0, ConfiguredSkillSlot.UtilitySkill1));
            // Untouched slots stay None.
            Assert.IsTrue(player.GetConfiguredSkill(SkillContextCode.A0, ConfiguredSkillSlot.EliteSkill).IsNone);
        }

        [TestMethod]
        public void ContextArrays_AreSeparate()
        {
            var store = new PlayerStateStore();
            store.Apply(ConfiguredSkillUpdate(0x1111, 0, 0, 7)); // context 0 -> array A
            store.Apply(ConfiguredSkillUpdate(0x2222, 0, 1, 7)); // context 1 -> array B

            Assert.IsTrue(store.TryGet(new PlayerListIndex(7), out PlayerState player));
            Assert.AreEqual(new SkillContentId(0x1111), player.GetConfiguredSkill(SkillContextCode.A0, ConfiguredSkillSlot.HealSkill));
            Assert.AreEqual(new SkillContentId(0x2222), player.GetConfiguredSkill(SkillContextCode.B1, ConfiguredSkillSlot.HealSkill));

            // 0 and 2 share array A; 1 and 3 share array B.
            Assert.AreEqual(new SkillContentId(0x1111), player.GetConfiguredSkill(SkillContextCode.A2, ConfiguredSkillSlot.HealSkill));
            Assert.AreEqual(new SkillContentId(0x2222), player.GetConfiguredSkill(SkillContextCode.B3, ConfiguredSkillSlot.HealSkill));
        }

        [TestMethod]
        public void Reprocess_IsIdempotent()
        {
            var store = new PlayerStateStore();
            DecodedMessage message = ConfiguredSkillUpdate(0x1206, 2, 0, 1);
            Assert.AreEqual(1, store.Apply(message));
            Assert.AreEqual(0, store.Apply(message));
        }

        [TestMethod]
        public void InvalidSlot_Throws()
        {
            var store = new PlayerStateStore();
            Assert.ThrowsExactly<FormatException>(() => store.Apply(ConfiguredSkillUpdate(1, 5, 0, 1)));
            Assert.ThrowsExactly<FormatException>(() => store.Apply(ConfiguredSkillUpdate(1, 0x14, 0, 1)));
            // The profession-mechanic slot (0x15) is accepted.
            Assert.AreEqual(1, store.Apply(ConfiguredSkillUpdate(1, 0x15, 0, 1)));
        }

        [TestMethod]
        public void Lifecycle_GetOrCreate_Remove()
        {
            var store = new PlayerStateStore();
            var index = new PlayerListIndex(0x4E);
            PlayerState created = store.GetOrCreate(index);
            Assert.AreSame(created, store.GetOrCreate(index));
            Assert.AreEqual(1, store.PlayerCount);

            Assert.IsTrue(store.Remove(index));
            Assert.IsFalse(store.Remove(index));
            Assert.AreEqual(0, store.PlayerCount);
        }

        [TestMethod]
        public void NonModelledMessage_IsIgnored()
        {
            var store = new PlayerStateStore();
            var other = new DecodedMessage(0x27C, 0, 0, new List<DecodedField>());
            Assert.AreEqual(0, store.Apply(other));
            Assert.AreEqual(0, store.PlayerCount);
        }
    }
}
