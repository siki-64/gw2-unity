using System.Runtime.InteropServices;

namespace Gw2.Contracts;

/// <summary>Partial PvP rating page state for GameBuild.SupportedGameBuild; unknown bases omitted.</summary>
[StructLayout(LayoutKind.Explicit, Size = 0x88)]
internal struct PvpRatingLeaderboard
{
    [FieldOffset(0x00)] internal nint Vtable;
    [FieldOffset(0x18)] internal uint FrameId;
    [FieldOffset(0x50)] internal nint OwnerCallback;
    [FieldOffset(0x58)] internal nint LeaderboardObserverVtable;
    [FieldOffset(0x60)] internal nint Interface060Vtable;
    [FieldOffset(0x68)] internal nint Interface068Vtable;
    // Initialized to 4; modes 0 and 2 use ten-entry pages, mode 1 uses an account request.
    [FieldOffset(0x70)] internal uint Mode;
    [FieldOffset(0x78)] internal nint Leaderboard;
    [FieldOffset(0x80)] internal uint PageIndex;
}
