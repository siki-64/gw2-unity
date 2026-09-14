using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit)]
internal struct PlCliContext
{
    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0x360)] internal PlCliUserMap UserMap;
}

[StructLayout(LayoutKind.Explicit, Size = 0x18)]
internal struct PlCliUserMap
{
    [FieldOffset(0x10)] internal nint FullListHead;
}

[StructLayout(LayoutKind.Explicit)]
internal struct PlCliChannel
{
    internal const uint MatchChannelType = 3;

    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0xAA8)] internal nint Members;
    [FieldOffset(0xAB4)] internal uint MemberCount;
    [FieldOffset(0xCA0)] internal uint Type;
}

[StructLayout(LayoutKind.Explicit)]
internal unsafe struct PlCliChannelMember
{
    [FieldOffset(0x078)] internal PlCliUser* User;
}

[StructLayout(LayoutKind.Explicit)]
internal struct PlCliUser
{
    internal const int AccountNameCharacterCapacity = 0x48;

    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0x018)] internal PlCliUserIdentity Identity;
    [FieldOffset(0x038)] internal nint FullListLink;
    [FieldOffset(0x280)] internal AccountNameBuffer AccountName;

    [InlineArray(AccountNameCharacterCapacity)]
    internal struct AccountNameBuffer
    {
        internal char Element0;
    }
}

[StructLayout(LayoutKind.Sequential, Size = 0x10)]
internal struct PlCliUserIdentity
{
    internal uint Part0;
    internal uint Part1;
    internal uint Part2;
    internal uint Part3;
}
