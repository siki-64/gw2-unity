using System;

namespace Gw2.Protocol.State
{
    /// <summary>
    /// Minimal per-player state for build 205.780: the two context arrays of configured standard
    /// skills (message <c>0x264</c>), five slots each. Identifiers stay typed — the player is keyed
    /// by <see cref="PlayerListIndex"/> and values are <see cref="SkillContentId"/>.
    /// <para>
    /// Contexts <c>0</c>/<c>2</c> write the same native array (A); <c>1</c>/<c>3</c> write array B, so
    /// the code cannot distinguish <c>A0</c> from <c>A2</c> after the fact.
    /// </para>
    /// </summary>
    public sealed class PlayerState
    {
        private readonly SkillContentId[] _arrayA = new SkillContentId[ConfiguredSkills.SlotCount];
        private readonly SkillContentId[] _arrayB = new SkillContentId[ConfiguredSkills.SlotCount];
        private SkillContentId _mechanicA;
        private SkillContentId _mechanicB;

        public PlayerState(PlayerListIndex index)
        {
            Index = index;
        }

        /// <summary>The list index this state is keyed by.</summary>
        public PlayerListIndex Index { get; }

        /// <summary>Player name from the roster message (<c>0x1AB</c>); null until seen.</summary>
        public string Name { get; private set; }

        /// <summary>
        /// The opaque 16-byte key the roster message carries and the native constructor copies into
        /// the player object. Its meaning (account/character identity bridge) is unresolved; it is
        /// preserved rather than named.
        /// </summary>
        public byte[] Key { get; private set; }

        /// <summary>The roster message's trailing flag word (<c>0x1AB</c> field 4), semantics unresolved.</summary>
        public uint AddFlags { get; private set; }

        /// <summary>PvP gear provider state (<c>ChCliPlayer +0x97B0</c>, messages <c>0x200..0x207</c>).</summary>
        public PlayerPvpEquipment Pvp { get; } = new PlayerPvpEquipment();

        /// <summary>Apply a roster identity; returns true when the stored identity changed.</summary>
        internal bool SetIdentity(string name, ReadOnlySpan<byte> key, uint addFlags)
        {
            bool changed = Name != name || AddFlags != addFlags || !KeyMatches(key);
            Name = name;
            AddFlags = addFlags;
            Key = key.ToArray();
            return changed;
        }

        private bool KeyMatches(ReadOnlySpan<byte> key)
        {
            if (Key == null || Key.Length != key.Length) return false;
            for (int i = 0; i < Key.Length; i++)
                if (Key[i] != key[i]) return false;
            return true;
        }

        /// <summary>
        /// The skill configured for <paramref name="context"/>/<paramref name="slot"/>
        /// (<see cref="SkillContentId.None"/> when unset).
        /// </summary>
        public SkillContentId GetConfiguredSkill(SkillContextCode context, ConfiguredSkillSlot slot)
        {
            ConfiguredSkillArray array = ConfiguredSkills.ArrayFor(context);
            if (slot == ConfiguredSkillSlot.ProfessionMechanic)
                return array == ConfiguredSkillArray.B ? _mechanicB : _mechanicA;
            if (!ConfiguredSkills.IsValidSlot((byte)slot))
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "invalid configured-skill slot");
            return SlotArray(array)[(int)slot];
        }

        /// <summary>Convenience overload for a server-field slot byte.</summary>
        public SkillContentId GetConfiguredSkill(SkillContextCode context, byte slot) =>
            GetConfiguredSkill(context, ToSlot(slot));

        /// <summary>Set a configured skill; returns true when the stored value changed.</summary>
        public bool SetConfiguredSkill(SkillContextCode context, ConfiguredSkillSlot slot, SkillContentId skill)
        {
            ConfiguredSkillArray array = ConfiguredSkills.ArrayFor(context);
            if (slot == ConfiguredSkillSlot.ProfessionMechanic)
            {
                SkillContentId previous = array == ConfiguredSkillArray.B ? _mechanicB : _mechanicA;
                if (array == ConfiguredSkillArray.B) _mechanicB = skill;
                else _mechanicA = skill;
                return !previous.Equals(skill);
            }

            SkillContentId[] store = SlotArray(array);
            SkillContentId old = store[(int)slot];
            store[(int)slot] = skill;
            return !old.Equals(skill);
        }

        /// <summary>Set a configured skill from a server-field slot byte.</summary>
        public bool SetConfiguredSkill(SkillContextCode context, byte slot, SkillContentId skill) =>
            SetConfiguredSkill(context, ToSlot(slot), skill);

        private static ConfiguredSkillSlot ToSlot(byte slot)
        {
            if (!ConfiguredSkills.IsValidSlot(slot))
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "invalid configured-skill slot");
            return (ConfiguredSkillSlot)slot;
        }

        private SkillContentId[] SlotArray(ConfiguredSkillArray array) =>
            array == ConfiguredSkillArray.B ? _arrayB : _arrayA;
    }
}
