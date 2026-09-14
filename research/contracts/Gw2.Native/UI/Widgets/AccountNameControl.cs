using System.Runtime.InteropServices;

namespace Gw2.Native;

/// <summary>
/// Partial compound account-name control for GameBuild.SupportedGameBuild.
/// Child 0 is the optional alternate name; child 1 is the formatted account name.
/// Observer callbacks receive this + 0x60, not this. Unknown base storage is omitted.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 0x78)]
internal struct AccountNameControl
{
    [FieldOffset(0x00)] internal nint Vtable;
    [FieldOffset(0x18)] internal uint FrameId;
    [FieldOffset(0x50)] internal nint OwnerCallback;
    [FieldOffset(0x58)] internal nint TextListenerVtable;
    [FieldOffset(0x60)] internal nint ContactObserverVtable;
    [FieldOffset(0x68)] internal Guid AccountIdentity;
}
