# Native player-name resolution

**Confirmed build(s):** `207.032`.<br>
**Status:** build-local live reconstruction of the player-wrapper to character/name relationship.<br>
**Unresolved:** stale-entry handling, unresolved aliases, and cross-build validity.

Build-`207.032` live inspection confirms the native player/name relationship.

```text
ContextCollection +0x98
    └─ ChCliContext
        ├─ +0x60 → ChCliCharacter* character array
        └─ +0x80 → ChCliPlayer* player-wrapper array
                         ├─ +0x18 → ChCliCharacter*
                         └─ +0x68 → UTF-16 display-name pointer
```

The `+0x60` array is sparse. The `+0x80` array is bounded by `+0x8C`; its entries are the native
player wrappers returned by the `ChCliContext` vtable `+0x110` lookup. To resolve a name, iterate the
character array, build a map from each player wrapper's `+0x18` character pointer to its `+0x68` name
pointer, then join the character pointer against that map.

The live capture showed the same `ChCliCharacter*` in both arrays and a valid UTF-16 string at the
player wrapper's name pointer. This is native memory, independent of MumbleLink and `NameRenderCtx`.

## Full native object

The player-list entry is the full native `ChCliPlayer`, not a small name-record wrapper. In build
`207.032`, the allocator passes `0xA178` as the object size, calls the constructor that installs
the `ChCliPlayer` vtable, and the matching deleting path passes the same size. The player object
also owns the recovered progress, reward-track, and specialization regions represented by the
overlay in `Gw2.Contracts/Character/ChCliPlayer.cs`. Its PvP loadout region is a separate
`ChCliCharacterContext` subobject at `ChCliPlayer +0x4F18`.

The native vtable exposes two adjacent character-link getters: slot `+0x78` reads `this+0x18`, and
slot `+0x80` reads `this+0x20`. Current live entries contain the same `ChCliCharacter*` in both
fields. The `+0x18` link is the canonical name-resolution link; `+0x20` remains an unresolved alias
and is separate from the embedded PvP context at `ChCliPlayer +0x4F18` used by spectator gear.

The player-list index is also recoverable. The creation helper bounds-checks the requested index
against `ChCliContext +0x8C`, passes it as `edx` to `ChCliPlayer::Ctor`, and the constructor stores
that argument at `+0x74`. Several live entries confirm it equals the containing-array index (for
example `0x80` and `0x8E`). The adjacent `+0x70` low dword was zero in those captures and remains
unnamed. The constructor writes `+0x60` and `+0x62` as separate 16-bit fields; their gameplay
meanings remain unresolved.

The same constructor calls the PvP-context constructor with `this +0x4F18`. That context initializes
nine `0x730`-byte loadouts and fields through `+0x4100`; the parent then constructs additional
subobjects at `+0x9020` and `+0x9080` before its inline `ChCliProgress` at `+0x9758`.

The player vtable’s `+0x88` method is the native name accessor: it first attempts a character-derived
name and falls back to the direct `+0x68` pointer. That gives us both a safe pointer-level join for
all wrappers and a native accessor to follow when the direct pointer is absent or stale.
