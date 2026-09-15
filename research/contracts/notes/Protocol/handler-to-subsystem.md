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

## Message family `0x200..0x207` - PvP gear provider

Every handler resolves the target with `GetPlayerByListIndex(record+0x02)` -> `ChCliPlayer`, then acts
on the provider at **`ChCliPlayer + 0x97B0`** (`PvpGearProvider`, layout in
[../UI/Widgets/pvp-equipment-state.md](../UI/Widgets/pvp-equipment-state.md)). Record offsets:
`+0x02` index, `+0x06`/`+0x0A` rank ids, `+0x0E` flag, `+0x0F` rune, `+0x13` relic, `+0x17` amulet,
`+0x1B` pointer to four sigil ids (the `0x204` chain's `defSize 0x27`).

| Msg | Handler | Resolve | Setter | Provider write |
| --- | --- | --- | --- | --- |
| `0x200` | `FUN_141253c10` | content `0x23` on `+0x06` | `FUN_1411f77c0` | `+0x40` rune |
| `0x201` | `FUN_141254500` | content `0x23` on `+0x06` | `FUN_1411f79f0` | `+0x48` relic |
| `0x202` | `FUN_141253cc0` | content `0x36` on `+0x06` | `FUN_1411f7800` | `+0x50` amulet |
| `0x203` | `FUN_141253d70` | `+0x58` request `0x71` on `+0x06` | `FUN_1411f7840` | `+0x60` hero; `+0x70` bit 1 |
| `0x204` | `FUN_141253e30` | `+0x1f8` ranks `+0x06`/`+0x0A`; `0x23`/`0x36` gear | `FUN_1411f78e0`, `FUN_1411f7800`, `FUN_1411f79f0`, `FUN_1411f77c0`, `FUN_1411f7a40` | `+0x68`/`+0xB0` ranks, `+0x50`/`+0x48`/`+0x40`, `+0xB8+idx*8` sigils; `+0x70` bit 0 |
| `0x205` | `FUN_141254130` | - | `FUN_1411b64b0` | destroys the provider (`+0x97B0` cleared) |

Setter bodies (build-local, each stores `def` then fires a provider notification):

| Setter | Write |
| --- | --- |
| `FUN_1411f77c0` | `provider+0x40 = def` (rune) |
| `FUN_1411f79f0` | `provider+0x48 = def` (relic) |
| `FUN_1411f7800` | `provider+0x50 = def` (amulet) |
| `FUN_1411f7840` | `provider+0x60 = heroDef`; `provider+0x70` bit 1 = flag |
| `FUN_1411f78e0` | `provider+0xB0` = rank(`+0x06`), `provider+0x68` = rank(`+0x0A`); `provider+0x70` bit 0 = flag |
| `FUN_1411f7a40` | assert index `< 4`; `provider+0xB8 + idx*8 = def` (sigils) |

These offsets are exactly the recovered `PvpGearProvider` layout, so the family is now traced from the
wire record to the provider fields. `0x204` also calls `FUN_1411b6290` (ensure manager) before the
provider exists; `0x205` calls `FUN_1411b64b0` (destroy) after resolution.

## Message family `0x25B..0x265` - per-player `ChCliSkill`

All resolve a target with `GetPlayerByListIndex` (record field varies, see below) and then call a
`ChCliSkill` method at `ChCliPlayer + 0x9BD8`. Handler addresses and the `ChCliSkill` offset each
method writes (build-local):

| Msg | Handler | Method | Writes / effect |
| --- | --- | --- | --- |
| `0x25B` | `FUN_1412573f0` | `FUN_141224f70` | `+0x58 = rec+2`, `+0x5C = rec+6`; observer `+0xB0` |
| `0x25C` | `FUN_141257460` | `FUN_141224b50` | looks up `ChCliContext+0x390` by `rec+2`, then observer `+0xB0` |
| `0x25D` | `FUN_1412574d0` | `FUN_141224bb0` | `+0x20` = skill id (from `skillDef+0x28`) |
| `0x25E` | `FUN_141257590` | `FUN_141224c00` | clears `+0x28`/`+0x30`, resets container `+0x40` |
| `0x25F` | `FUN_1412575f0` | `FUN_141224c40` | keyed table at `+0x40`/`+0x48`, entry `{skillId, rec+0x0A}`; observer `+0xB0` |
| `0x260` | `FUN_1412576b0` | `FUN_141224d90` | keyed table `+0x08`/`+0x10` (count `+0x0C`), entries `{id, defId}`; skillbar notify |
| `0x261` | `FUN_141257720` | `FUN_141224ec0` | add to container `+0x40`; clear a bit in the bitmap at `+0x28` keyed by skill id; skillbar + observer |
| `0x262` | `FUN_1412577e0` | not `ChCliSkill` | resolves an agent (`FUN_14101ee40(rec+2)`) and drives the attack-target path (`FUN_1411d7350`/`FUN_1411d7390`) |
| `0x263` | `FUN_141257960` | `FUN_141224fb0` | keyed table `+0x08`/`+0x10`, entry `{skillIdA, skillIdB}`; also `ChCliPlayer+0x618`; skillbar notify |
| `0x264` | `FUN_141257a20` | `FUN_141225160` | `+0x60`/`+0x88` at `slot*8` (context A/B); observer + skillbar |
| `0x265` | `FUN_141257ac0` | `FUN_141225220` | add to container `+0x40`; `+0x20` = skill id; observer `+0xB0` with the two flag bits |

The target index field is not uniform across the family: `0x25B` uses `rec+0x0A`, `0x25C` uses
`rec+0x12`, `0x25D/0x25F/0x261` use `rec+6`, `0x25E/0x260` use `rec+2`, `0x263` uses `rec+0x0A`,
`0x264` uses `rec+8`, `0x265` uses `rec+7`. Skill ids are resolved through
`CnContext+0xe0` vtable `+0x230` (type `0x40`) except `0x264` (`+0x238`, type `0x41`).

So the family populates four distinct `ChCliSkill` storages:

- `+0x58`/`+0x5C` - two scalars (`0x25B`);
- `+0x20` - the current skill id (`0x25D`, `0x265`);
- `+0x08`/`+0x10` (+ count `+0x0C`) - a keyed table of `{id, value}` 12-byte entries (`0x260`, `0x263`);
- `+0x40`/`+0x48` - another keyed container plus the `+0x28` bitmap (`0x25E`, `0x25F`, `0x261`, `0x265`);
- `+0x60`/`+0x88` - the five configured standard skills (`0x264`).

Every method ends by notifying the `ChCliSkill+0xB0` observer list, and most also notify the owned
`ChCliSkillbar` (through `ChCliPlayer`'s character link).

## Captured-stream families

The message ids actually observed in the private captures (Addenda 14, 20), traced to their
subsystem accessors. `ctx = FUN_1409b4820()`.

| Msg(s) | Handler(s) | Subsystem | Effect |
| --- | --- | --- | --- |
| `0x21 0x23 0x33 0x34 0x39 0x47 0x4F 0x54` | `FUN_14102e150`/`290`/`ec50`/`ecf0`/`f010`/`f860`/`f9d0`/`fdc0` | `*(ctx+0x28)+0x30` | allocate a fixed-size object (`0x70..0xe0`), build it with a per-message `FUN_141030xxx`, then register via `FUN_14102aac0` keyed by `u16 @rec+2` in the table at `+0xd0`; `FUN_14102aac0` can emit outbound via `FUN_140fea110` |
| `0x2DE..0x2E3` | `FUN_1412c19a0`/`1a10`/`1c00`/`1c90` | `*(ctx+0xb8)` (combatant, `CmbtCliMsg.cpp`) | resolve combatant via `FUN_1412bd1f0`, then apply combat state (`FUN_1412c0470`), buff (`FUN_1412c0530`, content `0x40`), or agent-target ops (`FUN_1412c0b20`/`0cc0`); agents via `FUN_14101ee40(rec+6)` |
| `0x312` | `FUN_1413582c0` | `FUN_141345bd0()` | lookup `FUN_141346370(rec+2)`, then vtable `+0x30 (..., rec+6 != 0)` |
| `0x315` | `FUN_141357f20` | `FUN_141345bd0()` | `FUN_141345740(manager, rec)` |
| `0x40F 0x410 0x415 0x417 0x428` | `FUN_1414107d0`/`810`/`ba0`/`e40`/`11620` | `*(ctx+0x1c8)` | WvW/match config: scalar fields (`+0x30`, `+0x1d0`, `+0x1f0`, `+0x22c`, flag word `+0x150`), and keyed string tables at `+0x118` (stride `0x38`) and `+0x130` (stride `0x28`) |
| `0x00B` | `FUN_1417ec460` | `*(ctx+0x60)` | `FUN_1417ec210(*(ctx+0x60)+8)` |
| `0x010` | `FUN_1417ec5f0` | - | no-op handler (only reads the context) |
| `0x264` (triggered) | `FUN_141257a20` | `ChCliPlayer + 0x9BD8` | `ChCliSkill + 0x60/+0x88` (see above) |

So the **observed inbound stream is dominated by content-definition registration
(`*(ctx+0x28)+0x30`), combat/buff state (`*(ctx+0xb8)`) and WvW match configuration
(`*(ctx+0x1c8)`)**, not the character skill/equipment subsystems. Those are reached only for the
specifically triggered `0x264`.

Context accessors confirmed so far: `ctx+0x28` -> `+0x30` definition registry; `ctx+0x60` -> object;
`ctx+0x98` `ChCliContext`; `ctx+0xb8` combatant manager; `ctx+0xe0` `CnContext`; `ctx+0x1c8` WvW/match
object.

## The `*(ctx+0x28)+0x30` registry is the agent world (`AgWorld`)

`FUN_141029e20()` returns `*(ctx+0x28) + 0x30`; `ctx` is the thread-local client context
(`FUN_1409b4820`). The registrar chain shows what it is:

```text
FUN_14102aac0(registry, 0, key = u16@rec+2, seqArg = u32@rec+4/+5, obj):
  existing = registry+0xd0[ key ]   (count registry+0xdc, guarded)
  FUN_1410344b0(obj, registry, existing)   // obj+0x58 = registry, obj+0x20 = existing
  ...
  FUN_14101e7c0(obj, key, registry+0x1bc, flag)   // obj+0x18 = id (clamped), obj+0x1c = flag|seq
     -> if obj+0x8 == 0: FUN_14102ad20(registry, obj)   // insert
  ...
  FUN_14101e7c0 also drives FUN_14101e4a0 (list remove) / FUN_14102ad20 (list insert)
```

`FUN_14102ad20(registry, obj)` asserts from `AgWorld.cpp:0x532` and inserts `obj` into the list at
`registry+0x150` (`FUN_14101e080`). So the registry is the **agent world (`AgWorld`)**; the messages
insert typed objects into an `AgWorld` list. Every object carries a network id (`+0x18`), a sequence
and flag word (`+0x1c`), a registry back-link (`+0x58`) and a related-object link (`+0x20`).

The per-message builders turn the decoded record into a typed object:

| Msg | Builder | Kind / type bits | vtable |
| --- | --- | --- | --- |
| `0x21` | `FUN_141030410` | kind `0`, `(rec+8) \| 0x8810800` | `PTR_FUN_142114dc8` |
| `0x23` | `FUN_141030750` | kind `3`, `(rec+8) \| 0x8801000` | `PTR_FUN_142114ea8` |

`FUN_141030750` reads a placement: two `i16` at `rec+0x1e`/`rec+0x20` as angles
(`value * π / 32766`, `0x7fff` = infinity), a sub-record at `rec+0x16`, and a count/list at
`rec+0x0d`/`rec+0x0e` — i.e. a world transform/placement definition.

Reliability/ordering lives in `FUN_14102ad80(registryWindow, seq, base)`, a `/0x28`-sized sliding
window with reorder/late-handling, and `FUN_14102aac0` can emit an outbound message through
`FUN_140fea110` for `dispatchType == 1` — the client requests/acks content rather than only
consuming it.

Observed `AgWorld` fields (build-local): `+0x10`, key table `+0xd0`/count `+0xdc`, insert list
`+0x150`, `+0x178`, counter `+0x1bc`, `+0x1cc`, `+0x1d0`, `+0x1d4`, `+0x1f8`, `+0x20c`.

## Open

- The `AgWorld` object kinds/vtables and the full list at `+0x150`.
- The identity of `*(ctx+0x28)` (the `AgWorld` owner).
- The `CmbtCli` combatant and buff layouts behind `FUN_1412c0470`/`0530`.
- Which subsystem `FUN_141345bd0` is (`0x312`/`0x315`).
- What selects `ChCliPlayer +0x18` vs `+0x20`.
- The `ChCliSkillbar` internal layout (slots, selected skill) behind `FUN_1411f6d20`.
- The observer-list helpers (`FUN_141222650`, `FUN_1411cce60`, `FUN_1411ef3b0`) and who watches them.
- The neighboring `0x25B..0x265` family handlers (storage/reader paths unnamed).
