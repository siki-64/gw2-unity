using System;

namespace Gw2.Protocol.State
{
    /// <summary>
    /// The player-list index namespace (build 205.780): the index into <c>ChCliContext</c>'s player
    /// list used by the per-player messages (for example <c>0x264</c>), resolved natively by
    /// <c>GetPlayerByListIndex</c>. It is **not** an agent id, a character-list index or a native
    /// content id; keep those namespaces separate.
    /// </summary>
    public readonly struct PlayerListIndex : IEquatable<PlayerListIndex>
    {
        public PlayerListIndex(uint value) { Value = value; }

        public uint Value { get; }

        public bool Equals(PlayerListIndex other) => Value == other.Value;

        public override bool Equals(object obj) => obj is PlayerListIndex other && Equals(other);

        public override int GetHashCode() => (int)Value;

        public override string ToString() => "0x" + Value.ToString("x");
    }

    /// <summary>
    /// The AgWorld object network id (build 205.780): the key at record `+0x02` that the agent-world
    /// content family stores at object `+0x18`. It is a **separate domain** from
    /// <see cref="PlayerListIndex"/> and from a native content id; its relation to `Agent.agentId`
    /// is not established.
    /// </summary>
    public readonly struct AgentNetworkId : IEquatable<AgentNetworkId>
    {
        public AgentNetworkId(uint value) { Value = value; }

        public uint Value { get; }

        public bool Equals(AgentNetworkId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is AgentNetworkId other && Equals(other);

        public override int GetHashCode() => (int)Value;

        public override string ToString() => "0x" + Value.ToString("x");
    }

    /// <summary>
    /// A native skill content id (build 205.780). The configured-skill path resolves it to a skill
    /// definition through the <c>CnContext</c> content resolver (type <c>0x41</c>). Public API ids
    /// must not be assumed equal, and no members are enumerated until a captured packet binds an id
    /// to a definition.
    /// </summary>
    public readonly struct SkillContentId : IEquatable<SkillContentId>
    {
        public SkillContentId(uint value) { Value = value; }

        public uint Value { get; }

        /// <summary><c>0</c> is the "no skill" value (the handler skips resolution for it).</summary>
        public bool IsNone => Value == 0;

        public bool Equals(SkillContentId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is SkillContentId other && Equals(other);

        public override int GetHashCode() => (int)Value;

        public override string ToString() => "0x" + Value.ToString("x");
    }

    /// <summary>
    /// The five configured standard-skill slots plus the F1 profession-mechanic slot
    /// (<c>ChCliConfiguredSkillSlot</c>). The native setter accepts <c>slot &lt;= 4</c> or <c>0x15</c>.
    /// </summary>
    public enum ConfiguredSkillSlot : byte
    {
        HealSkill = 0,
        UtilitySkill1 = 1,
        UtilitySkill2 = 2,
        UtilitySkill3 = 3,
        EliteSkill = 4,
        ProfessionMechanic = 0x15,
    }

    /// <summary>The two configured-skill arrays (native <c>ChCliSkill +0x60</c> / <c>+0x88</c>).</summary>
    public enum ConfiguredSkillArray
    {
        A = 0,
        B = 1,
    }

    /// <summary>
    /// The configured-skill context selector. The native classifier is
    /// <c>((context - 1) &amp; 0xFFFFFFFD) == 0</c> — true only for <c>1</c> and <c>3</c> (array B);
    /// every other value selects array A. The high-level meaning (terrestrial/aquatic) is unresolved.
    /// </summary>
    public enum SkillContextCode : byte
    {
        A0 = 0,
        B1 = 1,
        A2 = 2,
        B3 = 3,
    }

    /// <summary>Configured-skill storage rules recovered from the native setter.</summary>
    public static class ConfiguredSkills
    {
        /// <summary>Number of slots in each configured-skill array.</summary>
        public const int SlotCount = 5;

        /// <summary>Native classifier: context <c>1</c> or <c>3</c> select array B, else array A.</summary>
        public static ConfiguredSkillArray ArrayFor(SkillContextCode context) =>
            ((byte)context == 1 || (byte)context == 3) ? ConfiguredSkillArray.B : ConfiguredSkillArray.A;

        /// <summary>Whether a wire slot byte is accepted by the native setter.</summary>
        public static bool IsValidSlot(byte slot) => slot <= 4 || slot == 0x15;
    }
}
