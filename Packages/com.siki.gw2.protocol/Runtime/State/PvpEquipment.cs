using System;
using System.Collections.Generic;

namespace Gw2.Protocol.State
{
    // The PvP gear definition ids are kept as separate types so their id spaces cannot be mixed.
    // The native resolver request code differs per kind (0x23 rune/relic/sigil, 0x36 amulet,
    // 0x71 hero); a resolved PvP-rank definition is a different namespace again. These are content
    // ids, not public API ids.

    /// <summary>A PvP rune content id (<c>ChCliPvp</c> update <c>0x200</c>, request code <c>0x23</c>).</summary>
    public readonly struct PvpRuneId : IEquatable<PvpRuneId>
    {
        public PvpRuneId(uint value) { Value = value; }
        public uint Value { get; }
        public bool IsNone => Value == 0;
        public bool Equals(PvpRuneId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PvpRuneId other && Equals(other);
        public override int GetHashCode() => (int)Value;
        public override string ToString() => "0x" + Value.ToString("x");
    }

    /// <summary>A PvP relic content id (<c>0x201</c>, request code <c>0x23</c>).</summary>
    public readonly struct PvpRelicId : IEquatable<PvpRelicId>
    {
        public PvpRelicId(uint value) { Value = value; }
        public uint Value { get; }
        public bool IsNone => Value == 0;
        public bool Equals(PvpRelicId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PvpRelicId other && Equals(other);
        public override int GetHashCode() => (int)Value;
        public override string ToString() => "0x" + Value.ToString("x");
    }

    /// <summary>A PvP amulet content id (<c>0x202</c>, request code <c>0x36</c>).</summary>
    public readonly struct PvpAmuletId : IEquatable<PvpAmuletId>
    {
        public PvpAmuletId(uint value) { Value = value; }
        public uint Value { get; }
        public bool IsNone => Value == 0;
        public bool Equals(PvpAmuletId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PvpAmuletId other && Equals(other);
        public override int GetHashCode() => (int)Value;
        public override string ToString() => "0x" + Value.ToString("x");
    }

    /// <summary>A PvP sigil content id (<c>0x204</c>, request code <c>0x23</c>).</summary>
    public readonly struct PvpSigilId : IEquatable<PvpSigilId>
    {
        public PvpSigilId(uint value) { Value = value; }
        public uint Value { get; }
        public bool IsNone => Value == 0;
        public bool Equals(PvpSigilId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PvpSigilId other && Equals(other);
        public override int GetHashCode() => (int)Value;
        public override string ToString() => "0x" + Value.ToString("x");
    }

    /// <summary>The selected Mist Champion / PvP Hero definition id (<c>0x203</c>, request code <c>0x71</c>).</summary>
    public readonly struct PvpHeroId : IEquatable<PvpHeroId>
    {
        public PvpHeroId(uint value) { Value = value; }
        public uint Value { get; }
        public bool IsNone => Value == 0;
        public bool Equals(PvpHeroId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PvpHeroId other && Equals(other);
        public override int GetHashCode() => (int)Value;
        public override string ToString() => "0x" + Value.ToString("x");
    }

    /// <summary>A PvP-rank definition id (combined <c>0x204</c>/<c>0x207</c> and incremental <c>0x206</c>).</summary>
    public readonly struct PvpRankId : IEquatable<PvpRankId>
    {
        public PvpRankId(uint value) { Value = value; }
        public uint Value { get; }
        public bool IsNone => Value == 0;
        public bool Equals(PvpRankId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PvpRankId other && Equals(other);
        public override int GetHashCode() => (int)Value;
        public override string ToString() => "0x" + Value.ToString("x");
    }

    /// <summary>
    /// A player's PvP gear provider state (build 205.780, <c>ChCliPlayer +0x97B0</c>), populated by
    /// the server-driven <c>0x200..0x207</c> family. See
    /// <c>research/contracts/notes/UI/Widgets/pvp-equipment-state.md</c>.
    /// <para>
    /// The native provider stores resolved definition pointers; the runtime stores the wire content
    /// ids the handler resolved from them. An absent id is <c>0</c>.
    /// </para>
    /// </summary>
    public sealed class PlayerPvpEquipment
    {
        /// <summary>Number of sigil entries (<c>PvpWeaponUpgradeIndex</c> 0..3).</summary>
        public const int SigilCount = 4;

        /// <summary>Provider flag bit set by the combined record's <c>+0x0E</c> byte.</summary>
        public const uint CombinedFlagBit = 1u;

        /// <summary>Provider flag bit set by the PvP hero update's <c>+0x0A</c> byte.</summary>
        public const uint HeroFlagBit = 2u;

        private readonly PvpSigilId[] _sigils = new PvpSigilId[SigilCount];

        /// <summary>Whether the provider exists (created by any gear update, cleared by <c>0x205</c>).</summary>
        public bool HasProvider { get; private set; }

        public PvpRuneId Rune { get; private set; }
        public PvpRelicId Relic { get; private set; }
        public PvpAmuletId Amulet { get; private set; }
        public PvpHeroId Hero { get; private set; }

        /// <summary>Provider flags (<c>ChCliPlayer +0x97B0 +0x70</c>).</summary>
        public uint Flags { get; private set; }

        /// <summary>Combined-record rank from <c>+0x06</c> (<c>provider +0x68</c>).</summary>
        public PvpRankId CombinedRank06 { get; private set; }

        /// <summary>Combined-record rank from <c>+0x0A</c> (<c>provider +0xB0</c>).</summary>
        public PvpRankId CombinedRank0A { get; private set; }

        /// <summary>Incremental rank from <c>0x206</c> (<c>provider +0x78</c>).</summary>
        public PvpRankId IncrementalRank { get; private set; }

        /// <summary>The four sigil ids in native <c>PvpWeaponUpgradeIndex</c> order.</summary>
        public IReadOnlyList<PvpSigilId> Sigils => _sigils;

        public PvpSigilId GetSigil(int index) => _sigils[index];

        /// <summary>Mark the provider present; returns true when it was created.</summary>
        internal bool EnsureProvider()
        {
            if (HasProvider) return false;
            HasProvider = true;
            return true;
        }

        /// <summary>Destroy the provider and clear its values; returns true when it existed.</summary>
        internal bool DestroyProvider()
        {
            if (!HasProvider) return false;
            HasProvider = false;
            Rune = default;
            Relic = default;
            Amulet = default;
            Hero = default;
            Flags = 0;
            CombinedRank06 = default;
            CombinedRank0A = default;
            IncrementalRank = default;
            Array.Clear(_sigils, 0, _sigils.Length);
            return true;
        }

        internal bool SetRune(PvpRuneId value) { bool c = !Rune.Equals(value); Rune = value; return c; }
        internal bool SetRelic(PvpRelicId value) { bool c = !Relic.Equals(value); Relic = value; return c; }
        internal bool SetAmulet(PvpAmuletId value) { bool c = !Amulet.Equals(value); Amulet = value; return c; }
        internal bool SetHero(PvpHeroId value) { bool c = !Hero.Equals(value); Hero = value; return c; }

        internal bool SetSigil(int index, PvpSigilId value)
        {
            if ((uint)index >= SigilCount) throw new ArgumentOutOfRangeException(nameof(index));
            bool c = !_sigils[index].Equals(value);
            _sigils[index] = value;
            return c;
        }

        internal bool SetCombinedRank06(PvpRankId value) { bool c = !CombinedRank06.Equals(value); CombinedRank06 = value; return c; }
        internal bool SetCombinedRank0A(PvpRankId value) { bool c = !CombinedRank0A.Equals(value); CombinedRank0A = value; return c; }
        internal bool SetIncrementalRank(PvpRankId value) { bool c = !IncrementalRank.Equals(value); IncrementalRank = value; return c; }

        /// <summary>Set or clear a provider flag bit; returns true when it changed.</summary>
        internal bool SetFlag(uint bit, bool on)
        {
            uint next = on ? (Flags | bit) : (Flags & ~bit);
            bool changed = next != Flags;
            Flags = next;
            return changed;
        }
    }
}
