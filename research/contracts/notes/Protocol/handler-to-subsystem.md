# Handler-to-subsystem traces

**Build:** `205.780` (Gw2-64.exe, image SHA-256
`D2AE84876A0B93277FCCB368969046B848BB0403FD09DB420389C813D2459B23`).

**Status:** in progress. This note traces a decoded message through its handler into the native
subsystems whose layouts are already recovered. It complements the wire-level notes
([inbound-framing.md](inbound-framing.md), [schema-registry.md](schema-registry.md)) with the
**control flow after dispatch**.

Addresses are build-local coordinates; the durable identity is the named routine plus the offsets.

## Method

For each message id: locate the recv handler, read the resolved record fields it uses, then follow
every call until it writes a field of a layout we already know (e.g. `ChCliPlayer`, `ChCliSkill`,
`ChCliSkillbar`, `PvpGearProvider`). Record each write as `subsystem + offset <- source`.

## Common plumbing

| Routine | Role |
| --- | --- |
| `FUN_1409b4820` | thread-local client context accessor (returns the object holding `+0x98` and `+0xe0`) |
| `ctx+0x98`, vtable `+0x68` | local/owned `ChCliPlayer*` (`ChCliContext`) |
| `ctx+0x98`, vtable `+0x110` | `GetPlayerByListIndex(u32)` -> `ChCliPlayer*` |
| `ctx+0xe0` | `CnContext*` (native content resolver) |
| `CnContext` vtable `+0x230` | resolve content type `0x40` |
| `CnContext` vtable `+0x238` | resolve content type `0x41` |

The resolver boundary is described in [../UI/Widgets/remote-equipped-skills.md](../UI/Widgets/remote-equipped-skills.md).

## Message `0x264` - per-player configured skill

Handler `FUN_141257a20` (`ChCliMsg.cpp`); record `u16 id, u32 skillContentId, u8 slot, u8 context,
u32 playerListIndex` (`defSize 0x0C`).

```text
FUN_141257a20(decoded):
  ctx        = FUN_1409b4820()
  player     = ctx+0x98 vtable+0x110 ( playerListIndex )        // ChCliPlayer*
  assert player (ChCliMsg.cpp:0x2460)
  skillDef   = skillContentId ? ctx+0xe0 vtable+0x238 ( skillContentId ) : 0
  ChCliSkill::SetConfiguredSkill( player + 0x9BD8, skillDef, slot, context )
```

Setter `FUN_141225160(ChCliSkill*, skillDef, slot, context)` (`ChCliSkill.cpp`):

```text
assert(slot <= 4 || slot == 0x15)
if (!classify(context))   *(ChCliSkill + 0x60 + slot*8) = skillDef   // context array A
else                      *(ChCliSkill + 0x88 + slot*8) = skillDef   // context array B
FUN_141222650(ChCliSkill + 0xB0, ...)                                // observer list notify
FUN_1411f4aa0( *(ChCliPlayer + 0x20) + 0x520 )                       // owned skillbar notify
```

`classify` is `FUN_1411c0da0(context) = ((context - 1) & 0xFFFFFFFD) == 0`, i.e. true only for
`context` in `{1, 3}` -> context array B (`+0x88`), else A (`+0x60`). This matches the recovered
`+0x60`/`+0x88` five-entry arrays in
[../UI/Widgets/remote-equipped-skills.md](../UI/Widgets/remote-equipped-skills.md).

Terminal subsystems:

- **`ChCliSkill + 0x60`/`+0x88`** (the known five configured-skill pointers) — the record's
  `skillContentId` and `slot` land here.
- **`ChCliSkillbar`** (`*(ChCliPlayer+0x20)+0x520`) via `vtable+0x1f8`, a notification/rebuild.
- **observer list** at `ChCliSkill + 0xB0` (`FUN_141222650` inserts/invokes watchers).

## Message `0x27C` - runtime skillbar slot update

Handler `FUN_1412588a0` (`ChCliMsg.cpp`); record fields `+0x02 skillContentId`, `+0x06 slot`.

```text
FUN_1412588a0(decoded):
  ctx      = FUN_1409b4820()
  player   = ctx+0x98 vtable+0x68 ()                     // local/owned ChCliPlayer*
  if player != 0:
    character = *(ChCliPlayer + 0x18)                    // ChCliCharacter*
    if character != 0:
      skillDef = ctx+0xe0 vtable+0x230 ( skillContentId )   // content type 0x40
      assert skillDef (ChCliMsg.cpp:0x2710)
      FUN_1411f6d20( *(character + 0x520), &skillDef, slot, &arg )   // ChCliSkillbar setter
```

`FUN_1411f6d20` (`ChCliSkillbar.cpp`) writes the skillbar's own storage
(`ChCliSkillbar + 0x288` slot, `+0x270`) and fires its observer lists (`+0xF0`/`+0xF8` via
`FUN_1411cce60`/`FUN_1411ef3b0`). **`ChCliSkillbar`'s internal slot layout is not yet recovered**, so
`0x27C` reaches the subsystem but the trace stops at the setter.

## Difference between the two paths

Both reach a `ChCliSkillbar`, but through different `ChCliPlayer` pointers:

| Message | Character link | Content type |
| --- | --- | --- |
| `0x264` | `ChCliPlayer + 0x20` (`Character20`) | `0x41` |
| `0x27C` | `ChCliPlayer + 0x18` (`Character`) | `0x40` |

`ChCliPlayer` carries two character pointers (`+0x18 Character`, `+0x20 Character20`); their
distinction is unresolved and is the main open question this trace raises.

## Open

- What selects `ChCliPlayer +0x18` vs `+0x20`.
- The `ChCliSkillbar` internal layout (slots, selected skill) behind `FUN_1411f6d20`.
- The observer-list helpers (`FUN_141222650`, `FUN_1411cce60`, `FUN_1411ef3b0`) and who watches them.
- The neighboring `0x25B..0x265` family handlers (storage/reader paths unnamed).
