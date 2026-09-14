# Game build

**Confirmed build(s):** `205.299`.<br>
**Status:** build-local static accessor and live value reconstruction.<br>
**Unresolved:** representations and accessor identity in other game builds.

Build `205.299` stores the integer `205299` in `g_GameBuild`. `GetGameBuild` is a leaf accessor
that returns this value through a RIP-relative load. During live confirmation on this build, the
game's Settings UI displayed `Gw2: 205,299`.

The proxy locates the accessor structurally: its RIP-relative target must be 12 bytes before the
static `Gw2-64.exe` name in the associated build-information data. Resolution must produce exactly
one candidate; otherwise the build is reported as unavailable.
