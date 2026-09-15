using System;
using System.Collections.Generic;
using Gw2.Protocol.MsgPack;

namespace Gw2.Protocol.State
{
    /// <summary>
    /// Applies decoded server-to-client messages to a minimal per-player state for build 205.780.
    /// <para>
    /// Players are keyed by <see cref="PlayerListIndex"/> and created on first reference: the traced
    /// <c>0x264</c> path resolves a player through <c>GetPlayerByListIndex</c> without carrying a
    /// separate spawn, so a reducer cannot wait for one. <see cref="Remove"/> models a despawn.
    /// </para>
    /// <para>
    /// This is a bounded state update, not a wire codec: it consumes already-decoded
    /// <see cref="DecodedMessage"/>s and never touches transport or schema.
    /// </para>
    /// </summary>
    public sealed class PlayerStateStore
    {
        /// <summary>Per-player configured standard-skill assignment (<c>ChCliSkillMessageId</c>).</summary>
        public const int ConfiguredSkillUpdateMessageId = 0x264;

        /// <summary>Player roster add (<c>ChCliContext::PlayerCreate</c>).</summary>
        public const int PlayerAddMessageId = 0x1AB;

        /// <summary>Player roster remove (<c>ChCliContext::PlayerRemove</c>).</summary>
        public const int PlayerRemoveMessageId = 0x1AD;

        /// <summary>PvP rune update (<c>ChCliPvp</c>).</summary>
        public const int PvpRuneUpdateMessageId = 0x200;

        /// <summary>PvP relic update.</summary>
        public const int PvpRelicUpdateMessageId = 0x201;

        /// <summary>PvP amulet update.</summary>
        public const int PvpAmuletUpdateMessageId = 0x202;

        /// <summary>PvP hero (Mist Champion) update.</summary>
        public const int PvpHeroUpdateMessageId = 0x203;

        /// <summary>PvP full rank and equipment update.</summary>
        public const int PvpFullUpdateMessageId = 0x204;

        /// <summary>PvP gear provider destroy.</summary>
        public const int PvpDestroyMessageId = 0x205;

        /// <summary>PvP incremental rank update.</summary>
        public const int PvpIncrementalRankMessageId = 0x206;

        /// <summary>PvP combined rank update.</summary>
        public const int PvpCombinedRankMessageId = 0x207;

        private readonly Dictionary<uint, PlayerState> _players = new Dictionary<uint, PlayerState>();

        /// <summary>The tracked players.</summary>
        public IReadOnlyCollection<PlayerState> Players => _players.Values;

        public int PlayerCount => _players.Count;

        /// <summary>Return the player for <paramref name="index"/>, creating it if unseen.</summary>
        public PlayerState GetOrCreate(PlayerListIndex index)
        {
            if (!_players.TryGetValue(index.Value, out PlayerState player))
            {
                player = new PlayerState(index);
                _players.Add(index.Value, player);
            }
            return player;
        }

        public bool TryGet(PlayerListIndex index, out PlayerState player) =>
            _players.TryGetValue(index.Value, out player);

        /// <summary>Forget a player (despawn). Returns true when it existed.</summary>
        public bool Remove(PlayerListIndex index) => _players.Remove(index.Value);

        public void Clear() => _players.Clear();

        /// <summary>
        /// Apply one decoded message. Returns the number of state changes (0 when the message is not
        /// modelled here, or changed nothing). Throws <see cref="FormatException"/> on a modelled
        /// message whose record shape is wrong (fail closed).
        /// </summary>
        public int Apply(DecodedMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            switch (message.MessageId)
            {
                case PlayerAddMessageId:
                    return ApplyPlayerAdd(message);
                case PlayerRemoveMessageId:
                    return ApplyPlayerRemove(message);
                case ConfiguredSkillUpdateMessageId:
                    return ApplyConfiguredSkillUpdate(message);
                case PvpRuneUpdateMessageId:
                    return ApplyPvpGear(message, PvpGearField.Rune);
                case PvpRelicUpdateMessageId:
                    return ApplyPvpGear(message, PvpGearField.Relic);
                case PvpAmuletUpdateMessageId:
                    return ApplyPvpGear(message, PvpGearField.Amulet);
                case PvpHeroUpdateMessageId:
                    return ApplyPvpHero(message);
                case PvpFullUpdateMessageId:
                    return ApplyPvpFull(message);
                case PvpDestroyMessageId:
                    return ApplyPvpDestroy(message);
                case PvpIncrementalRankMessageId:
                    return ApplyPvpIncrementalRank(message);
                case PvpCombinedRankMessageId:
                    return ApplyPvpCombinedRank(message);
                default:
                    return 0;
            }
        }

        /// <summary>Apply a batch; returns the total number of state changes.</summary>
        public int Apply(IReadOnlyList<DecodedMessage> messages)
        {
            if (messages == null) throw new ArgumentNullException(nameof(messages));
            int changes = 0;
            for (int i = 0; i < messages.Count; i++) changes += Apply(messages[i]);
            return changes;
        }

        // Chain (build 205.780): MP_MSGID, playerId (varint u32), name (0x0d utf-16 cstring),
        // key (0x0b 16-byte blob), flags (varint u32).
        private int ApplyPlayerAdd(DecodedMessage message)
        {
            IReadOnlyList<DecodedField> fields = message.Fields;
            if (fields == null || fields.Count < 5)
                throw new FormatException(
                    $"msgpack: 0x1ab record has {fields?.Count ?? 0} fields, expected 5");

            var index = new PlayerListIndex((uint)AsUInt(fields[1], "playerId"));
            string name = AsString(fields[2], "name");
            byte[] key = AsBytes(fields[3], "key", 0x10);
            uint flags = (uint)AsUInt(fields[4], "flags");

            PlayerState player = GetOrCreate(index);
            return player.SetIdentity(name, key, flags) ? 1 : 0;
        }

        // Chain (build 205.780): MP_MSGID, playerId (varint u32).
        private int ApplyPlayerRemove(DecodedMessage message)
        {
            IReadOnlyList<DecodedField> fields = message.Fields;
            if (fields == null || fields.Count < 2)
                throw new FormatException(
                    $"msgpack: 0x1ad record has {fields?.Count ?? 0} fields, expected 2");

            var index = new PlayerListIndex((uint)AsUInt(fields[1], "playerId"));
            return Remove(index) ? 1 : 0;
        }

        // Chain (build 205.780): MP_MSGID, skillContentId (varint u32), slot (u8), context (u8),
        // playerListIndex (varint u32). See protocol/messages/205780/0x264.json.
        private int ApplyConfiguredSkillUpdate(DecodedMessage message)
        {
            IReadOnlyList<DecodedField> fields = message.Fields;
            if (fields == null || fields.Count < 5)
                throw new FormatException(
                    $"msgpack: 0x264 record has {fields?.Count ?? 0} fields, expected at least 5");

            var skill = new SkillContentId((uint)AsUInt(fields[1], "skillContentId"));
            byte slot = (byte)AsUInt(fields[2], "configuredSkillSlot");
            byte context = (byte)AsUInt(fields[3], "skillContextCode");
            var index = new PlayerListIndex((uint)AsUInt(fields[4], "playerListIndex"));

            if (!ConfiguredSkills.IsValidSlot(slot))
                throw new FormatException($"msgpack: 0x264 has invalid configured-skill slot {slot}");

            PlayerState player = GetOrCreate(index);
            bool changed = player.SetConfiguredSkill((SkillContextCode)context, slot, skill);
            return changed ? 1 : 0;
        }

        private enum PvpGearField
        {
            Rune,
            Relic,
            Amulet,
        }

        // Incremental gear records: [msgid, playerListIndex (varint), gearId (varint)].
        private int ApplyPvpGear(DecodedMessage message, PvpGearField field)
        {
            IReadOnlyList<DecodedField> fields = message.Fields;
            if (fields == null || fields.Count < 3)
                throw new FormatException(
                    $"msgpack: 0x{message.MessageId:x} record has {fields?.Count ?? 0} fields, expected 3");

            PlayerState player = GetOrCreate(new PlayerListIndex((uint)AsUInt(fields[1], "playerListIndex")));
            uint id = (uint)AsUInt(fields[2], "gearId");
            PlayerPvpEquipment pvp = player.Pvp;
            int changes = pvp.EnsureProvider() ? 1 : 0;
            switch (field)
            {
                case PvpGearField.Rune: changes += pvp.SetRune(new PvpRuneId(id)) ? 1 : 0; break;
                case PvpGearField.Relic: changes += pvp.SetRelic(new PvpRelicId(id)) ? 1 : 0; break;
                case PvpGearField.Amulet: changes += pvp.SetAmulet(new PvpAmuletId(id)) ? 1 : 0; break;
            }
            return changes;
        }

        // Hero record: [msgid, playerListIndex, heroId, flag (u8)].
        private int ApplyPvpHero(DecodedMessage message)
        {
            IReadOnlyList<DecodedField> fields = message.Fields;
            if (fields == null || fields.Count < 4)
                throw new FormatException(
                    $"msgpack: 0x203 record has {fields?.Count ?? 0} fields, expected 4");

            PlayerState player = GetOrCreate(new PlayerListIndex((uint)AsUInt(fields[1], "playerListIndex")));
            PlayerPvpEquipment pvp = player.Pvp;
            int changes = pvp.EnsureProvider() ? 1 : 0;
            changes += pvp.SetHero(new PvpHeroId((uint)AsUInt(fields[2], "heroId"))) ? 1 : 0;
            changes += pvp.SetFlag(PlayerPvpEquipment.HeroFlagBit, AsUInt(fields[3], "flag") != 0) ? 1 : 0;
            return changes;
        }

        // Full record: [msgid, playerListIndex, rank06, rank0A, flag (u8), rune, relic, amulet,
        // sigils (0x10 x4 of varint), trailing varint].
        private int ApplyPvpFull(DecodedMessage message)
        {
            IReadOnlyList<DecodedField> fields = message.Fields;
            if (fields == null || fields.Count < 9)
                throw new FormatException(
                    $"msgpack: 0x204 record has {fields?.Count ?? 0} fields, expected at least 9");

            PlayerState player = GetOrCreate(new PlayerListIndex((uint)AsUInt(fields[1], "playerListIndex")));
            PlayerPvpEquipment pvp = player.Pvp;
            int changes = pvp.EnsureProvider() ? 1 : 0;
            changes += pvp.SetCombinedRank06(new PvpRankId((uint)AsUInt(fields[2], "rank06"))) ? 1 : 0;
            changes += pvp.SetCombinedRank0A(new PvpRankId((uint)AsUInt(fields[3], "rank0A"))) ? 1 : 0;
            changes += pvp.SetFlag(PlayerPvpEquipment.CombinedFlagBit, AsUInt(fields[4], "flag") != 0) ? 1 : 0;
            changes += pvp.SetRune(new PvpRuneId((uint)AsUInt(fields[5], "rune"))) ? 1 : 0;
            changes += pvp.SetRelic(new PvpRelicId((uint)AsUInt(fields[6], "relic"))) ? 1 : 0;
            changes += pvp.SetAmulet(new PvpAmuletId((uint)AsUInt(fields[7], "amulet"))) ? 1 : 0;
            changes += ApplySigils(pvp, fields[8]);
            return changes;
        }

        private static int ApplySigils(PlayerPvpEquipment pvp, DecodedField field)
        {
            if (!(field.Value is IReadOnlyList<DecodedField[]> items) ||
                items.Count != PlayerPvpEquipment.SigilCount)
                throw new FormatException("msgpack: 0x204 sigil array is not four entries");

            int changes = 0;
            for (int i = 0; i < items.Count; i++)
            {
                DecodedField[] item = items[i];
                if (item == null || item.Length < 1)
                    throw new FormatException("msgpack: 0x204 sigil entry is empty");
                changes += pvp.SetSigil(i, new PvpSigilId((uint)AsUInt(item[0], "sigil"))) ? 1 : 0;
            }
            return changes;
        }

        // Destroy record: [msgid, playerListIndex].
        private int ApplyPvpDestroy(DecodedMessage message)
        {
            IReadOnlyList<DecodedField> fields = message.Fields;
            if (fields == null || fields.Count < 2)
                throw new FormatException(
                    $"msgpack: 0x205 record has {fields?.Count ?? 0} fields, expected 2");

            PlayerListIndex index = new PlayerListIndex((uint)AsUInt(fields[1], "playerListIndex"));
            if (!TryGet(index, out PlayerState player)) return 0;
            return player.Pvp.DestroyProvider() ? 1 : 0;
        }

        // Incremental rank record: [msgid, playerListIndex, rankId].
        private int ApplyPvpIncrementalRank(DecodedMessage message)
        {
            IReadOnlyList<DecodedField> fields = message.Fields;
            if (fields == null || fields.Count < 3)
                throw new FormatException(
                    $"msgpack: 0x206 record has {fields?.Count ?? 0} fields, expected 3");

            PlayerState player = GetOrCreate(new PlayerListIndex((uint)AsUInt(fields[1], "playerListIndex")));
            PlayerPvpEquipment pvp = player.Pvp;
            int changes = pvp.EnsureProvider() ? 1 : 0;
            changes += pvp.SetIncrementalRank(new PvpRankId((uint)AsUInt(fields[2], "rankId"))) ? 1 : 0;
            return changes;
        }

        // Combined rank record: [msgid, playerListIndex, rank06, rank0A, flag (u8)].
        private int ApplyPvpCombinedRank(DecodedMessage message)
        {
            IReadOnlyList<DecodedField> fields = message.Fields;
            if (fields == null || fields.Count < 5)
                throw new FormatException(
                    $"msgpack: 0x207 record has {fields?.Count ?? 0} fields, expected 5");

            PlayerState player = GetOrCreate(new PlayerListIndex((uint)AsUInt(fields[1], "playerListIndex")));
            PlayerPvpEquipment pvp = player.Pvp;
            int changes = pvp.EnsureProvider() ? 1 : 0;
            changes += pvp.SetCombinedRank06(new PvpRankId((uint)AsUInt(fields[2], "rank06"))) ? 1 : 0;
            changes += pvp.SetCombinedRank0A(new PvpRankId((uint)AsUInt(fields[3], "rank0A"))) ? 1 : 0;
            changes += pvp.SetFlag(PlayerPvpEquipment.CombinedFlagBit, AsUInt(fields[4], "flag") != 0) ? 1 : 0;
            return changes;
        }

        private static ulong AsUInt(DecodedField field, string name)
        {
            if (field == null || !(field.Value is ulong value))
                throw new FormatException($"msgpack: field '{name}' is not an unsigned integer");
            return value;
        }

        private static string AsString(DecodedField field, string name)
        {
            if (field == null || !(field.Value is string value))
                throw new FormatException($"msgpack: field '{name}' is not a string");
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
