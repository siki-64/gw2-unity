# Remote PvP equipped skills

**Confirmed build(s):** `207.032`.<br>
**Status:** build-local configured-skill layout and handler reconstruction.<br>
**Unresolved:** spectator-reader confirmation for the remaining context distinctions and cross-build validity.<br>
**Address scope:** runtime access uses the discovered `ContextCollection` anchor and Native
intra-object offsets. Build-local code coordinates are not part of the access contract.

Build target: `207.032`.

This note records the per-player configured-skill path used for remote players.
It is separate from both the PvP equipment provider messages (`0x200..0x207`)
and the current character's runtime `ChCliSkillbar`.

## Recovered player skill state

`ChCliPlayer` embeds `ChCliSkill` at `+0x9BD8`. Two previously unresolved
five-qword regions are now identified as configured standard-skill definition
pointer arrays:

| ChCliSkill offset | Size | Recovered role |
| ---: | ---: | --- |
| `+0x60` | `0x28` | five configured standard-skill definition pointers, context A |
| `+0x88` | `0x28` | five configured standard-skill definition pointers, context B |

The native setter validates the slot with `ConstCharSkillSlotIsStandard`.
The implementation is an unsigned `slot <= 4` check, proving five standard
configured positions. These positions are the configured-skill class used for
heal, utility, and elite selections. `Gw2.Contracts` records these positions as
`HealSkill`, `UtilitySkill1`, `UtilitySkill2`, `UtilitySkill3`, and
`EliteSkill`. The separately accepted native `0x15` slot is the F1 profession-
mechanic slot (`ProfessionMechanic`), covering replaceable mechanics such as
pets and revenant legend skills. Spectator F2-F4 entries are a separate,
currently buggy payload form: they arrive without a usable slot field, so
they must not be forced into the five-slot update struct.

`SkillContentId : uint` is the native content-id enum scaffold. Individual
members should be added only from validated native `SkillDefinition.Id`
captures; public API ids must not be assumed equivalent.

## Native content resolver: validated boundary

`ContextCollection +0xE0` is a `CnContext*`. Build `207.032` constructs this
object with `0x58A8` bytes and keeps two `CnData*` source pointers at `+0x08`
and `+0x10`. Live memory confirms the static vtable at `0x142206160` and that
the first source provides the populated content tables.

The actual vtable alignment is important. The following build-local thunks
are confirmed from that live vtable:

| CnContext slot | Thunk | fixed content type |
| ---: | --- | ---: |
| `+0x230` | `0x1412DEE20` | `0x40` |
| `+0x238` | `0x1412DEE40` | `0x41` |
| `+0x258` | `0x1412DEE60` | `0x182` |
| `+0x260` | `0x1412DEE70` | `0x183` |

The direct thunks delegate to virtual `+0x70`, whose implementation
(`sub_1412DEB70 -> sub_1412E3870`) packs a 10-bit type with a 22-bit query
value, rejects values with bits above `0x3F_FFFF`, and looks them up in the
`CnData +0xB8` hash map. The distinct virtual `+0x58` resolver uses the map
at `CnData +0xD0`.

Virtual `+0x48` (`sub_1412DE9F0 -> sub_1412E32F0`) iterates a type's native
array. Live data shows populated type-`0x40`, type-`0x41`, and type-`0x182`
arrays, but iteration alone has not demonstrated that an element's `+0x28`
key is the incoming configured-skill packet ID. `ChCliSkill.cpp` uses that
field as a native key (`sub_141224EC0`, `sub_141224FB0`, and
`sub_141225220`); Native therefore exposes it only as `ContentKey`.

A live trace at `0x141257A20` captured calls but did not independently verify
the assumed compact configured-skill record offsets. Do not generate
`SkillContentId` enum members from `ContentKey`, or from the `0x182/0x183`
tables, until a packet-dispatch trace binds an incoming ID to the resulting
definition pointer. Localized-name recovery remains separate.

### Message decoder boundary

The native message path is now located in `MsgConn.cpp`. `sub_140FE9E50`
(`MsgConnDispatch`) handles raw/packed input and calls `sub_140FE9120`
(`DispatchStream`) for packed streams. When no handler is cached,
`sub_140FE9120` calls `sub_140FEE920` with `srcBytes = 2` and `dstBytes = 4`;
the four-byte result is the message ID. It resolves that ID through the
connection's handler table (`sub_140FED3B0`) and stores it at `msgConn + 0x40`
at `0x140FE91E4` before invoking the schema decoder (`sub_140FEBC30`) and the
handler vtable.

This is confirmed dynamically on build `207.032`: a trace at the ID-store
instruction captured live decoded IDs `0x2DE`, `0x2E3`, `0x25`, `0x178`,
`0x292`, `0x97`, and `0x2DF`. A 30-second filtered trace for `0x27C` saw no
such packet during the idle sample, so the runtime-skillbar message still
needs an in-game triggering event. The decoder is therefore ready for a
targeted capture of `0x264`/`0x27C`, but that capture has not yet supplied new
`SkillContentId` enum members.

The fourth setter argument selects which five-entry array is written. Its
classifier returns true for native values `1` and `3`; the exact enum meaning
has not been recovered. `ChCliSkillContextCode` records only the observed
array-selection behavior, so the arrays remain named context A/B rather than
terrestrial/aquatic. Raw values outside the enum remain possible.

## Message 0x264: per-player configured skill

Recovered packed record:

```text
+0x00  uint16  message id = ChCliSkillMessageId.ConfiguredSkillUpdate (0x264)
+0x02  uint32  SkillContentId native skill content id
+0x06  uint8   ChCliConfiguredSkillSlot standard slot (0..4)
+0x07  uint8   ChCliSkillContextCode selector; high-level meaning unresolved
+0x08  uint32  PlayerListIndex
size           0x0C
```

The handler resolves `PlayerListIndex` through
`ChCliContext::GetPlayerByListIndex`, resolves the non-zero skill content id
to a skill definition, then calls the configured-skill setter on
`ChCliPlayer +0x9BD8`.

The key call sequence is:

```text
payload +0x08
  -> ChCliContext::GetPlayerByListIndex
  -> ChCliPlayer*

payload +0x02
  -> skill definition resolver
  -> SkillDefinition*

ChCliPlayer +0x9BD8
  -> ChCliSkill::SetConfiguredSkill
       slot    = payload +0x06
       context = payload +0x07
       value   = resolved skill definition
```

### ChCliSkill::SetConfiguredSkill

The function takes the effective arguments:

```text
RCX = ChCliSkill*
RDX = resolved skill definition pointer
R8D = standard configured-skill slot
R9D = skill context
```

After the `slot <= 4` validation it classifies the context and writes either:

```text
ChCliSkill +0x60 + slot*8
```

or:

```text
ChCliSkill +0x88 + slot*8
```

The setter also follows the object's native notification path after updating
the selected definition.

## Neighboring ChCliSkill message family

The configured-skill handler is part of the adjacent `0x25B..0x265`
per-player skill-state family. Several neighboring messages also resolve
`PlayerListIndex` and mutate `ChCliPlayer +0x9BD8`, but they do not directly
assign the five configured selections.

Current useful distinctions:

| Message | Recovered role |
| ---: | --- |
| `0x25B` | writes the two dwords at `ChCliSkill +0x58/+0x5C` |
| `0x25D` | resolves a skill content id into a separate ChCliSkill table/collection |
| `0x263` | resolves two skill content ids into a separate mapping path |
| **`0x264`** | **assigns one of five configured standard skill definitions for a player** |
| `0x265` | resolves one skill content id plus a two-bit flag form into another ChCliSkill operation |

The remaining family members should stay semantically unnamed until their
storage/reader paths are recovered.

## Message 0x27C: runtime skillbar slot update

A separate message updates the current character's runtime combat skillbar:

```text
message  ChCliSkillMessageId.RuntimeSkillbarSlotUpdate (0x27C)
```

Recovered fields used by the handler:

```text
+0x02  uint32 skill content id
+0x06  uint8  runtime skillbar slot
```

The path is:

```text
current/owned player
  -> ChCliPlayer +0x18
  -> ChCliCharacter
  -> ChCliCharacter +0x520
  -> ChCliSkillbar
  -> runtime slot setter
```

This is intentionally kept separate from message `0x264`. The runtime
`ChCliSkillbar` can reflect transformations and other temporary combat state,
while `0x264` writes persistent per-player configured selections that are
available on remote roster entries.

## Spectator relevance

The static evidence now proves that `0x264` is a remote-capable configured
skill payload because it carries `PlayerListIndex` and writes the five
standard selections into the player object. This makes it the strongest
candidate for the heal/utility/elite data displayed by the PvP spectator UI.

The final reader-side link from the spectator widget to
`ChCliSkill +0x60/+0x88` is not yet recovered. Until that reader is traced,
the following remain provisional:

- exact native meaning of payload `+0x07`;
- exact slot-to-heal/utility/elite label mapping;
- which of the two context arrays the spectator panel selects;
- whether a bulk/full-state companion message exists in addition to `0x264`
  incremental assignments.

Those unknowns do not affect the recovered payload layout or the storage
offsets above.
