namespace Gw2.Native;

public static class GameBuild
{
    public const uint SupportedGameBuild = 205780;

    public static bool IsSupported(uint gameBuild) =>
        gameBuild == SupportedGameBuild;
}
