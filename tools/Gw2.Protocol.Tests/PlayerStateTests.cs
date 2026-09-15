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
    // Minimal per-player state: configured-skills from 0x264.
    [TestClass]
    public class PlayerStateTests
    {

        private static ProtocolSchemaCorpus Corpus() => ProtocolSchemaCorpus.FromJson(
            Fixtures.ReadText("chains-recv.json"),
            Fixtures.ReadText("chains-send.json"));

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

        private static DecodedMessage PlayerAdd(uint playerId, string name, byte[] key, uint flags)
        {
            var fields = new List<DecodedField>
            {
                new DecodedField(1, true, (ulong)PlayerStateStore.PlayerAddMessageId),
                new DecodedField(4, true, (ulong)playerId),
                new DecodedField(0x0d, true, name),
                new DecodedField(0x0b, true, key),
                new DecodedField(4, true, (ulong)flags),
            };
            return new DecodedMessage(PlayerStateStore.PlayerAddMessageId, 0, 0, fields);
        }

        private static DecodedMessage PlayerRemove(uint playerId) =>
            new DecodedMessage(PlayerStateStore.PlayerRemoveMessageId, 0, 0, new List<DecodedField>
            {
                new DecodedField(1, true, (ulong)PlayerStateStore.PlayerRemoveMessageId),
                new DecodedField(4, true, (ulong)playerId),
            });

        private static byte[] Seq16(byte start)
        {
            var b = new byte[16];
            for (int i = 0; i < b.Length; i++) b[i] = (byte)(start + i);
            return b;
        }

        private static DecodedMessage PvpIncremental(int id, uint index, uint gearId) =>
            new DecodedMessage(id, 0, 0, new List<DecodedField>
            {
                new DecodedField(1, true, (ulong)id),
                new DecodedField(4, true, (ulong)index),
                new DecodedField(4, true, (ulong)gearId),
            });

        private static DecodedMessage PvpHero(uint index, uint heroId, byte flag) =>
            new DecodedMessage(PlayerStateStore.PvpHeroUpdateMessageId, 0, 0, new List<DecodedField>
            {
                new DecodedField(1, true, (ulong)PlayerStateStore.PvpHeroUpdateMessageId),
                new DecodedField(4, true, (ulong)index),
                new DecodedField(4, true, (ulong)heroId),
                new DecodedField(2, true, (ulong)flag),
            });

        private static DecodedMessage PvpFull(uint index, uint rank06, uint rank0A, byte flag,
            uint rune, uint relic, uint amulet, uint[] sigils)
        {
            var items = new List<DecodedField[]>(sigils.Length);
            foreach (uint s in sigils) items.Add(new[] { new DecodedField(4, true, (ulong)s) });
            return new DecodedMessage(PlayerStateStore.PvpFullUpdateMessageId, 0, 0, new List<DecodedField>
            {
                new DecodedField(1, true, (ulong)PlayerStateStore.PvpFullUpdateMessageId),
                new DecodedField(4, true, (ulong)index),
                new DecodedField(4, true, (ulong)rank06),
                new DecodedField(4, true, (ulong)rank0A),
                new DecodedField(2, true, (ulong)flag),
                new DecodedField(4, true, (ulong)rune),
                new DecodedField(4, true, (ulong)relic),
                new DecodedField(4, true, (ulong)amulet),
                new DecodedField(0x10, true, items),
                new DecodedField(4, true, 0UL),
            });
        }

        private static DecodedMessage PvpDestroy(uint index) =>
            new DecodedMessage(PlayerStateStore.PvpDestroyMessageId, 0, 0, new List<DecodedField>
            {
                new DecodedField(1, true, (ulong)PlayerStateStore.PvpDestroyMessageId),
                new DecodedField(4, true, (ulong)index),
            });

        private static DecodedMessage PvpRank(int id, uint index, uint rank) =>
            new DecodedMessage(id, 0, 0, new List<DecodedField>
            {
                new DecodedField(1, true, (ulong)id),
                new DecodedField(4, true, (ulong)index),
                new DecodedField(4, true, (ulong)rank),
            });

        private static DecodedMessage PvpCombinedRank(uint index, uint rank06, uint rank0A, byte flag) =>
            new DecodedMessage(PlayerStateStore.PvpCombinedRankMessageId, 0, 0, new List<DecodedField>
            {
                new DecodedField(1, true, (ulong)PlayerStateStore.PvpCombinedRankMessageId),
                new DecodedField(4, true, (ulong)index),
                new DecodedField(4, true, (ulong)rank06),
                new DecodedField(4, true, (ulong)rank0A),
                new DecodedField(2, true, (ulong)flag),
            });

        // Full pipeline: captured wire -> cipher -> frame -> schema -> state.
        [TestMethod]
        public void Replay_CapturedWire_UpdatesConfiguredSkills()
        {
            using var doc = JsonDocument.Parse(Fixtures.ReadBytes("0x264-wire.json"));
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
        public void Roster_AddSetsIdentity()
        {
            var store = new PlayerStateStore();
            byte[] key = Seq16(0xA0);
            Assert.AreEqual(1, store.Apply(PlayerAdd(0x4E, "Arena.1234", key, 1)));
            Assert.AreEqual(1, store.PlayerCount);

            Assert.IsTrue(store.TryGet(new PlayerListIndex(0x4E), out PlayerState player));
            Assert.AreEqual("Arena.1234", player.Name);
            Assert.AreEqual(1u, player.AddFlags);
            CollectionAssert.AreEqual(key, player.Key);
        }

        [TestMethod]
        public void Roster_AddThenSkillUpdate_ShareThePlayer()
        {
            var store = new PlayerStateStore();
            store.Apply(PlayerAdd(1, "Arena.5678", new byte[16], 0));
            store.Apply(ConfiguredSkillUpdate(0x1206, 2, 0, 1));

            Assert.IsTrue(store.TryGet(new PlayerListIndex(1), out PlayerState player));
            Assert.AreEqual("Arena.5678", player.Name);
            Assert.AreEqual(new SkillContentId(0x1206),
                player.GetConfiguredSkill(SkillContextCode.A0, ConfiguredSkillSlot.UtilitySkill2));
        }

        [TestMethod]
        public void Roster_RemoveClearsThePlayer()
        {
            var store = new PlayerStateStore();
            store.Apply(PlayerAdd(9, "X", new byte[16], 0));
            Assert.AreEqual(1, store.Apply(PlayerRemove(9)));
            Assert.AreEqual(0, store.PlayerCount);
            // Removing an unknown player is a no-op, not an error.
            Assert.AreEqual(0, store.Apply(PlayerRemove(9)));
        }

        [TestMethod]
        public void Roster_AddIsIdempotent()
        {
            var store = new PlayerStateStore();
            DecodedMessage message = PlayerAdd(3, "N", new byte[16], 0);
            Assert.AreEqual(1, store.Apply(message));
            Assert.AreEqual(0, store.Apply(message));
        }

        [TestMethod]
        public void Roster_Malformed_Throws()
        {
            var store = new PlayerStateStore();
            var empty = new DecodedMessage(PlayerStateStore.PlayerAddMessageId, 0, 0, new List<DecodedField>());
            Assert.ThrowsExactly<FormatException>(() => store.Apply(empty));
            // The key must be exactly 16 bytes.
            Assert.ThrowsExactly<FormatException>(() => store.Apply(PlayerAdd(1, "N", new byte[15], 0)));
        }

        [TestMethod]
        public void Pvp_IncrementalGear_SetsProvider()
        {
            var store = new PlayerStateStore();
            store.Apply(PvpIncremental(PlayerStateStore.PvpRuneUpdateMessageId, 5, 21194));
            store.Apply(PvpIncremental(PlayerStateStore.PvpRelicUpdateMessageId, 5, 106573));
            store.Apply(PvpIncremental(PlayerStateStore.PvpAmuletUpdateMessageId, 5, 34));

            Assert.IsTrue(store.TryGet(new PlayerListIndex(5), out PlayerState player));
            Assert.IsTrue(player.Pvp.HasProvider);
            Assert.AreEqual(new PvpRuneId(21194), player.Pvp.Rune);
            Assert.AreEqual(new PvpRelicId(106573), player.Pvp.Relic);
            Assert.AreEqual(new PvpAmuletId(34), player.Pvp.Amulet);
        }

        [TestMethod]
        public void Pvp_Hero_SetsFlagBit()
        {
            var store = new PlayerStateStore();
            store.Apply(PvpHero(7, 9, 1));

            Assert.IsTrue(store.TryGet(new PlayerListIndex(7), out PlayerState player));
            Assert.AreEqual(new PvpHeroId(9), player.Pvp.Hero);
            Assert.AreEqual(PlayerPvpEquipment.HeroFlagBit, player.Pvp.Flags & PlayerPvpEquipment.HeroFlagBit);
        }

        [TestMethod]
        public void Pvp_Full_AppliesRanksGearAndSigils()
        {
            var store = new PlayerStateStore();
            int changes = store.Apply(PvpFull(
                2, 42, 18, 1, 70651, 106573, 34, new uint[] { 21152, 21150, 81207, 81268 }));
            Assert.AreEqual(11, changes);

            Assert.IsTrue(store.TryGet(new PlayerListIndex(2), out PlayerState player));
            PlayerPvpEquipment pvp = player.Pvp;
            Assert.IsTrue(pvp.HasProvider);
            Assert.AreEqual(new PvpRankId(42), pvp.CombinedRank06);
            Assert.AreEqual(new PvpRankId(18), pvp.CombinedRank0A);
            Assert.AreEqual(PlayerPvpEquipment.CombinedFlagBit, pvp.Flags & PlayerPvpEquipment.CombinedFlagBit);
            Assert.AreEqual(new PvpRuneId(70651), pvp.Rune);
            Assert.AreEqual(new PvpRelicId(106573), pvp.Relic);
            Assert.AreEqual(new PvpAmuletId(34), pvp.Amulet);
            Assert.AreEqual(new PvpSigilId(21152), pvp.GetSigil(0));
            Assert.AreEqual(new PvpSigilId(81268), pvp.GetSigil(3));
        }

        [TestMethod]
        public void Pvp_Destroy_ClearsProvider()
        {
            var store = new PlayerStateStore();
            store.Apply(PvpIncremental(PlayerStateStore.PvpRuneUpdateMessageId, 5, 21194));
            Assert.AreEqual(1, store.Apply(PvpDestroy(5)));

            Assert.IsTrue(store.TryGet(new PlayerListIndex(5), out PlayerState player));
            Assert.IsFalse(player.Pvp.HasProvider);
            Assert.IsTrue(player.Pvp.Rune.IsNone);
        }

        [TestMethod]
        public void Pvp_Ranks()
        {
            var store = new PlayerStateStore();
            store.Apply(PvpRank(PlayerStateStore.PvpIncrementalRankMessageId, 1, 29));
            store.Apply(PvpCombinedRank(1, 45, 6, 0));

            Assert.IsTrue(store.TryGet(new PlayerListIndex(1), out PlayerState player));
            Assert.AreEqual(new PvpRankId(29), player.Pvp.IncrementalRank);
            Assert.AreEqual(new PvpRankId(45), player.Pvp.CombinedRank06);
            Assert.AreEqual(new PvpRankId(6), player.Pvp.CombinedRank0A);
        }

        [TestMethod]
        public void Pvp_Malformed_Throws()
        {
            var store = new PlayerStateStore();
            var bad = new DecodedMessage(PlayerStateStore.PvpRuneUpdateMessageId, 0, 0, new List<DecodedField>());
            Assert.ThrowsExactly<FormatException>(() => store.Apply(bad));
            Assert.ThrowsExactly<FormatException>(() =>
                store.Apply(PvpFull(1, 1, 2, 0, 3, 4, 5, new uint[] { 1, 2, 3 })));
        }

        [TestMethod]
        public void Pvp_SharesTheRosterPlayer()
        {
            var store = new PlayerStateStore();
            store.Apply(PlayerAdd(4, "Arena.9", new byte[16], 0));
            store.Apply(PvpIncremental(PlayerStateStore.PvpRuneUpdateMessageId, 4, 21194));

            Assert.AreEqual(1, store.PlayerCount);
            Assert.IsTrue(store.TryGet(new PlayerListIndex(4), out PlayerState player));
            Assert.AreEqual("Arena.9", player.Name);
            Assert.AreEqual(new PvpRuneId(21194), player.Pvp.Rune);
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
