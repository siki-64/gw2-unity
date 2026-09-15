# Schema registry

**Build:** `207.032` (Gw2-64.exe, image identity in protocol/catalog.json).

**Status:** registry and schema format recovered; the corpora are extracted and used by the runtime
decoder. A fresh re-derivation for the current image, from string/assert anchors, re-recovered the
registry record shape, `MsgPackFieldDef` stride/offsets and field-type cases; see
[msg-schema-207032.md](msg-schema-207032.md). Ids, addresses and corpora are build-local and must
not be carried between builds.

## Registration (`MsgChannel.cpp`)

59 registration sites call three registrars: `RegisterRecvOnly` (21), `RegisterTablePair` (36),
`RegisterBidirectional` (2). Each `RegisterTablePair` site carries two tables:

| Table | Shape | Installer | Record array | Confirmed fields |
| --- | --- | --- | --- | --- |
| A | flat pointer array (8-byte stride) | `FUN_140fecf00` | base `+0x50`, count `+0x5c`, 16-byte records | `{+0x00 flags, +0x08 defArray}` — **send** |
| B | pair array (16-byte stride) | `FUN_140fecd10` | base `+0x70`, count `+0x7c`, 32-byte records | `{+0x00 flags, +0x08 defArray, +0x10 dispatchType, +0x18 handlerFn}` — **recv** |

`RegisterRecvOnly` installs only a table-B (recv) pairs table. **Table A is send, table B is recv**
(proven from the installers, the recovered evidence).

Startup validation asserts, per entry: `defArray[0].fieldType == MP_MSGID`; `defSize <= 0xFFFF`;
`maxSize <= 0xFFFF`; and `maxSize <= MSG_MAX_BUFFER_SIZE` (`0x2000`). So no message exceeds 8192 bytes
decoded.

## The live registry (`conn+0x18`)

One object holds both arrays; `FUN_140fed3d0` (send) and `FUN_140fed3b0` (recv) index it directly by
id (no hashing). A live read gave: send `base +0x50 / count +0x5c / stride 0x10`; recv
`base +0x70 / count +0x7c / stride 0x20`.

**Mapping an id to its handler:** the *live* registry is id-indexed, but the *static* installation
table in the image (e.g. the `{defArray, handler}` pairs at `142168b60`) is written in source order,
**not** id order. Resolve an id by matching the entry's defArray pointer to `live_ids.csv`, never by
the entry's position in the array.

### It is a shared, refcounted global — not per-connection

`conn+0x18` is not allocated per connection. The constructor binds it from
`FUN_140fed450(protocol, isClient)`, which walks a **global list** (`DAT_142890078`) under a critical
section (`DAT_142890034`), matches on `(entry+0x20 == protocol, entry+0x24 == isClient)`, and returns
the shared entry with its refcount incremented (`FUN_1409b48c0`). So there are only a handful of
registries — keyed by **(protocol, client/server role)** — reused by every connection with the same
key. A miss returns `0` (the caller then has no recv/send tables).

The registry object also carries a **flood-policy callback at `+0x28`**: `FUN_140fed430` invokes it
(directly, `0xFFFFFFFF` when absent) and `Msg::DispatchStream` compares the per-window receive count
against it to raise `ERR_FLOODING` (`MsgConn.cpp:0xb32`, 1000-tick window).

Both tables are the same object: recv at `+0x70`/`+0x7c` (32-byte records), send at `+0x50`/`+0x5c`
(16-byte records). This is why the committed corpus has a **recv** and a **send** variant for the
same id space.

There is no global collection of *connections* at the game layer: the game client holds one game
connection (`DAT_1426632d0`, `GcGameCmd.cpp`, see [session-state.md](session-state.md)); the PortalCli
service instead keeps a `s_socketManager` singleton.

## The schema: `MsgPackFieldDef`

Stride `0x28`. Offsets (corrected, the recovered evidence):

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

### Per-type wire reads (`MsgPack_ReadFields`, verbatim from the switch)

| `fieldType` | Wire read | `param` role |
| --- | --- | --- |
| `1`, `3` | 2 bytes | - |
| `2` | 1 byte | - |
| `5`, `7`, `0x1a` | 8 bytes | - |
| `6`, `0x19`, `0x17` | 4 bytes | - |
| `8` | `0xc` bytes | - |
| `9`, `0xb` | `0x10` bytes | - |
| `0xc` | `0x1c` bytes | - |
| `4` | base-128 varint, 1..5 bytes | - |
| `0xa` | `0xc` header + varint count | - |
| `0xd` | NUL-terminated utf-16 (u16 scan) | **max** u16 elements incl. NUL |
| `0xe` | NUL-terminated 8-bit string | **max** bytes incl. NUL |
| `0xf` | 1-byte present + subchain | - |
| `0x10` | `param` sub-structs | fixed element count |
| `0x11` | u8 count + sub-structs | **max** count |
| `0x12` | u16 count + sub-structs | **max** count |
| `0x13` | `param` raw bytes | exact byte length |
| `0x14` | u8 length + bytes | **max** length (low u16) |
| `0x15` | u16 length + bytes | **max** length (low u16) |
| `0x16` | (fails; no data path) | - |
| `0`, `0x18` | chain end | - |

The reader enforces `param` as the **maximum** for `0xd/0xe/0x11/0x12/0x14/0x15` (it fails when the
wire count/length exceeds it); `0x10`/`0x13` use `param` as the exact count/length. Every fixed-size
field is bounds-checked against the payload end. `MsgPack_WriteFields` does not bounds-check
(outbound sizes must come from `ComputeMaxSize`). The runtime `MsgPackReader` mirrors this, including
the `param` maximum.

## Corpora

Direction-specific, since inbound and outbound have **different chains per id** (the recovered evidence/27):

| File | Source | Messages |
| --- | --- | --- |
| `protocol/schema/207032/live_ids.csv` | live recv registry | 1241 |
| `protocol/schema/207032/chains-recv.json` | that map + image chains | 1241 |
| `protocol/schema/207032/chains-send.json` | static table A, split by `registrars3.csv countA` | 481 |

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
