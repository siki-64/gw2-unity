# Schema registry

**Build:** `205.780` (Gw2-64.exe, image SHA-256
`D2AE84876A0B93277FCCB368969046B848BB0403FD09DB420389C813D2459B23`).

**Status:** registry and schema format recovered; the corpora are extracted and used by the runtime
decoder. Evidence trail: Addenda 2, 3, 6, 7, 8, 18, 19, 21, 26, 27; see
[msg-dispatch-addenda.md](msg-dispatch-addenda.md).

## Registration (`MsgChannel.cpp`)

59 registration sites call three registrars: `RegisterRecvOnly` (21), `RegisterTablePair` (36),
`RegisterBidirectional` (2). Each `RegisterTablePair` site carries two tables:

| Table | Shape | Installer | Record array | Confirmed fields |
| --- | --- | --- | --- | --- |
| A | flat pointer array (8-byte stride) | `FUN_140fecf00` | base `+0x50`, count `+0x5c`, 16-byte records | `{+0x00 flags, +0x08 defArray}` — **send** |
| B | pair array (16-byte stride) | `FUN_140fecd10` | base `+0x70`, count `+0x7c`, 32-byte records | `{+0x00 flags, +0x08 defArray, +0x10 dispatchType, +0x18 handlerFn}` — **recv** |

`RegisterRecvOnly` installs only a table-B (recv) pairs table. **Table A is send, table B is recv**
(proven from the installers, Addendum 7).

Startup validation asserts, per entry: `defArray[0].fieldType == MP_MSGID`; `defSize <= 0xFFFF`;
`maxSize <= 0xFFFF`; and `maxSize <= MSG_MAX_BUFFER_SIZE` (`0x2000`). So no message exceeds 8192 bytes
decoded.

## The live registry (`conn+0x18`)

One object holds both arrays; `FUN_140fed3d0` (send) and `FUN_140fed3b0` (recv) index it directly by
id (no hashing). A live read gave: send `base +0x50 / count +0x5c / stride 0x10`; recv
`base +0x70 / count +0x7c / stride 0x20`.

## The schema: `MsgPackFieldDef`

Stride `0x28`. Offsets (corrected, Addendum 8):

| Offset | Field |
| --- | --- |
| `0x00` | `fieldType` |
| `0x10` | `param` (element/array count; for `MP_MSGID`, the id) |
| `0x18` | `refTypeDef` (nested chain) |
| `0x20` | `defSize` (cached) |
| `0x24` | `maxSize` (cached) |
| `0x28` | `nextDef` |

Size table at `142109500`: 27 `{size, flags}` entries, indexed by `fieldType * 8`. `flags != 0` is a
fixed-size scalar (`size` is its output width); `flags == 0` is composite, with `maxSize` computed by
`MsgPack_ComputeMaxSize`.

Field types (`fieldType`): names confirmed from asserts are `MP_MSGID` `1`, `MP_OPTIONAL` `4`,
`MP_SRV_ALIGN` `0x16`, `MP_SRV_END` `0x18`. The rest are inferred from decode behaviour: `2` u8,
`3` u16, `4` varint, `5/7/0x1a` 8-byte, `6` u32, `8` 12-byte, `9/0xb` 16-byte, `0xa` array header +
count, `0xc` 28-byte, `0xd` utf-16 cstring, `0xe` utf-8 cstring, `0xf` optional, `0x10/0x11/0x12`
struct/string arrays, `0x13` fixed bytes, `0x14/0x15` length-prefixed bytes. **Values are
build-local.**

`fieldType 4` / `0x0a` carry a base-128 varint (1..5 bytes, little-endian, LSB group first).
`MsgPack_ReadFields` bounds-checks; `MsgPack_WriteFields` does not (outbound sizes must come from
`ComputeMaxSize`).

## Corpora

Direction-specific, since inbound and outbound have **different chains per id** (Addendum 26/27):

| File | Source | Messages |
| --- | --- | --- |
| `protocol/schema/205780/live_ids.csv` | live recv registry | 1241 |
| `protocol/schema/205780/chains-recv.json` | that map + image chains | 1241 |
| `protocol/schema/205780/chains-send.json` | static table A, split by `registrars3.csv countA` | 481 |

`0x264` (recv) is `MP_MSGID(0x264), varint, u8, u8, varint` (`defSize 0x0C`, `maxSize 0x0E`).
`0x120` is recv `1,4,4,2,4` but send `1,4,2`.

## Validation

- `maxSize <= 0x2000` holds for every live chain; the live map resolves colliding ids
  (`collisions.csv`, 194 ids whose static first-row chain was wrong).
- The static send split uses `registrars3.csv countA`; the live send array is sparse (array size 525,
  481 populated), so the static set is not guaranteed complete.

## Open

- Recalling the live send array's populated slots to confirm the 481 ids.
- Whether any message uses the `MP_ARRAY` envelope form (not unwrapped by the extractor).
