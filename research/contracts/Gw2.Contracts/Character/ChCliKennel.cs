using System.Runtime.InteropServices;

namespace Gw2.Contracts;

[StructLayout(LayoutKind.Explicit, Size = 0x1B8)]
internal unsafe struct ChCliKennel
{
    [FieldOffset(0x000)] internal Unknown000Storage Unknown000;
    [StructLayout(LayoutKind.Sequential, Size = 0x188)] internal struct Unknown000Storage { }
    [FieldOffset(0x188)] internal ChCliCharacter* OwnerCharacter;
    [FieldOffset(0x190)] internal Unknown190Storage Unknown190;
    [StructLayout(LayoutKind.Sequential, Size = 0x28)] internal struct Unknown190Storage { }
}
