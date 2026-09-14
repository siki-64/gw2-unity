using System.Runtime.InteropServices;

namespace Gw2.Contracts;

/// <summary>
/// Partial LbEntry control state for GameBuild.SupportedGameBuild.
/// Recovered from allocation/destruction and bind accesses; gaps are unmodeled native storage.
/// See re/notes/UI/Widgets/ranked-leaderboard.md. Does not own the name color.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 0xD0)]
internal struct LbEntry
{
    [FieldOffset(0x00)] internal nint Vtable;
    [FieldOffset(0x18)] internal uint FrameId;
    [FieldOffset(0x50)] internal nint OwnerCallback;
    [FieldOffset(0x58)] internal nint TextListenerVtable;
    [FieldOffset(0x60)] internal nint AccountNameOwnerVtable;
    [FieldOffset(0x68)] internal nint GridListenerVtable;
    [FieldOffset(0x70)] internal uint Flags;
    [FieldOffset(0x78)] internal nint Grid;
    [FieldOffset(0x80)] internal uint Rank;
    [FieldOffset(0x88)] internal nint Entry;
    // Board/column-selection key, distinct from AccountNameControl.AccountIdentity.
    [FieldOffset(0x90)] internal Guid BoardId;
    [FieldOffset(0xA0)] internal nint FirstColumn;
    [FieldOffset(0xB0)] internal nint OwnedBuffer;
}
