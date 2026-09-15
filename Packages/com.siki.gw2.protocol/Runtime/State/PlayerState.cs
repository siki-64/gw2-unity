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
