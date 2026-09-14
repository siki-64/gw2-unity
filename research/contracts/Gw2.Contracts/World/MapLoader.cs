using System.Runtime.InteropServices;

namespace Gw2.Contracts;

internal static class MapLoaderConstants
{
    internal const uint DefaultModelStreamTimeoutMs = 60_000;
    internal const uint DefaultMapAssetStreamTimeoutMs = 15_000;
    internal const uint DefaultAgentStreamTimeoutMs = 120_000;
}

[StructLayout(LayoutKind.Explicit)]
internal struct MapLoader
{
    [FieldOffset(0x2B8)] internal nint MapDefinition;
    [FieldOffset(0x2C0)] internal uint State;
    [FieldOffset(0x2E8)] internal uint ModelStreamTimeoutMs;
    [FieldOffset(0x2F0)] internal uint MapAssetStreamTimeoutMs;
    [FieldOffset(0x2F8)] internal uint AgentStreamTimeoutMs;
}
