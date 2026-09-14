namespace Gw2.Contracts;

internal static class InfoBarConstants
{
    internal const float One = 1.0f;
    internal const float UnselectedBrightness = 0.7f;
    internal const float DimDistanceStart = 1000.0f;
    internal const float DimDistanceRange = 5500.0f;
    internal const float DimCap = 0.6f;
    internal const float FadeMultiplier070 = 0.7f;
    internal const float FadeMultiplier090 = 0.9f;
    internal const float BigBarWidthDivisor = 1024.0f;
    internal const float BigBarHeightDivisor = 128.0f;
    internal const float SmallBarWidthDivisor = 216.0f;
    internal const float SmallBarHeightDivisor = 14.0f;
    internal const float AlphaFloor = 0.001f;
    internal const float CreationMaxDistanceSquared = 25_000_000.0f;
    internal const uint ColorRgbMask = 0x00FFFFFF;
    internal const uint ColorAlphaMask = 0xFF000000;
    internal const uint EnemyHealthFirstColor = 0xFFBB2A17;
    internal const uint EnemyHealthSecondColor = 0xFF401508;
    internal const uint EnemyDownstateFirstColor = 0xFF64140A;
    internal const uint EnemyDownstateSecondColor = EnemyHealthSecondColor;
    internal const uint FriendlyHealthFirstColor = 0xFF6AC951;
    internal const uint FriendlyHealthSecondColor = 0xFF0D2501;
    internal const uint FriendlyDownstateFirstColor = 0xFF0F5A0A;
    internal const uint FriendlyDownstateSecondColor = FriendlyHealthSecondColor;
    internal const uint NecromancerShroudFirstColor = 0xFF8B9A77;
    internal const uint NecromancerShroudSecondColor = 0xFF0D2501;
    internal const uint SpecterShroudFirstColor = 0xFFCA66BD;
    internal const uint SpecterShroudSecondColor = 0xFF5E0151;
    internal const uint BarrierColor = 0xC0FAE29A;

    internal static ReadOnlySpan<float> FadeThresholds => [575.0f, 1800.0f, 3200.0f, 3500.0f, 4700.0f, 128.0f];
    internal static ReadOnlySpan<float> BigBarDivisors => [BigBarWidthDivisor, BigBarHeightDivisor];
    internal static ReadOnlySpan<float> SmallBarDivisors => [SmallBarWidthDivisor, SmallBarHeightDivisor];
}
