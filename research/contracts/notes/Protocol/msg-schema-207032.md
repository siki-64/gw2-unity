# Message schema and dispatch: build 207.032 re-derivation

**Confirmed build(s):** `207.032` (Gw2-64.exe, image SHA-256
`612364759DFA24800F4F9802F842323FC21EB127A08DB8132BC12EA74D5FC3D1`).<br>
**Status:** static re-derivation from string/assert anchors and decoded instructions on the new
image. No live capture on this build. Build-local coordinates; not carried from any other build.<br>
**Unresolved:** the live per-connection registry merge and the id corpora are not re-extracted for
207.032; the connection mode/`MsgConn` field offsets, the transport-cipher state offsets and the
`ContextCollection` physical contract are not yet revalidated on this build (see below).

This note records a fresh re-derivation for build 207.032 after the live binary updated. The prior
build's analysis was **not** transferred by Ghidra Version Tracking and was **not** used as the
source of truth: every address below was re-found on the new image from its string/assert anchors.
The superseded topic notes remain in [schema-registry.md](schema-registry.md) and
[msg-dispatch.md](msg-dispatch.md) for subsystems that have not yet been re-derived.

## Method

The MCP string index was incomplete, so anchors were located deterministically from the PE: the
file is mapped to virtual addresses through its section table and the `...\Code\...*.cpp`
source-path and assert literals are searched byte-wise. The referencing code was located by
scanning the executable sections for RIP-relative `LEA`/`MOV` whose target equals the anchor VA.
Functions were then decompiled in place. Absolute VAs are coordinates in 207.032 only.

## Anchor literals (207.032)

| Anchor literal | VA |
| --- | --- |
| `D:\Perforce\Live\NAEU\v2\Code\Gw2\Services\Msg\MsgChannel.cpp` | `14210ba10` |
| `D:\Perforce\Live\NAEU\v2\Code\Gw2\Services\Msg\MsgConn.cpp` | `14210b3b8` |
| `D:\Perforce\Live\NAEU\v2\Code\Gw2\Services\Msg\MsgUtil.cpp` | `14210bd50` |
| `ChCliMsg.cpp` | `14216c160` |
| `defArray[0].fieldType == MP_MSGID` | `14210ba55`, `14210baFF`, `14210bb2e` |
| `MSG_MAX_BUFFER_SIZE` | `14210babF`, `14210bb8b` |

## Registry layout

Both tables confirmed from the installers and the pair/flat validators:

| Table | Shape | Record stride | Base | Count | Record fields |
| --- | --- | --- | --- | --- | --- |
| A (send) | flat | `0x10` | `channel+0x50` | `channel+0x5c` | `{+0x00 u32 flags, +0x08 defArray}` |
| B (recv) | pair | `0x20` | `channel+0x70` | `channel+0x7c` | `{+0x00 u32 flags, +0x08 defArray, +0x10 u32 dispatchType, +0x18 handlerFn}` |

- The send installer writes `base + id*0x10` with `defArray` at `+0x08` (`FUN_140fed960`).
- The recv installers write `base + id*0x20`, `defArray` at `+0x08`, `dispatchType` at `+0x10`
  and `handlerFn` at `+0x18` (`FUN_140fed770` sets `dispatchType = 1`, `FUN_140fed580` sets `0`).
- `Msg::DispatchStream` reads the resolved record's `dispatchType` at `+0x10` and calls the handler
  at `+0x18`; `dispatchType` is a `switch` with cases `0` and `1` (an out-of-range value is fatal).
- The registry object also caches the maximum decoded size at `channel+0x40`.
- The registry is a shared, refcounted global, not per-connection: it is created/looked up by
  `FUN_140fedae0` through a global list at `DAT_1428931a8` keyed on `(protocol, isClient)`; the
  object is `0x88` bytes with vtable `PTR_FUN_14210ba00`. This matches the prior build's model.

## `MsgPackFieldDef` descriptor

The descriptor walk in `FUN_140fea290` (`ComputeDefSize`) and `FUN_140fea310`
(`ComputeMaxSize`) advances `+0x28` per descriptor; `FUN_140feaf40` (`WriteFields`) advances
`param + 0x28` per field. Offsets:

| Offset | Field |
| --- | --- |
| `0x00` | `fieldType` |
| `0x10` | `param` (element/array count; for `MP_MSGID`, the id) |
| `0x18` | `refTypeDef` (nested chain) |
| `0x20` | `defSize` (cached) |
| `0x24` | `maxSize` (cached) |
| `0x28` | `nextDef` |

## Size table and field types

The size/flags table is at `DAT_14210b2e0` (size) with flags at `+0x04`, indexed by
`fieldType * 8`. `flags != 0` is a fixed-size scalar; `flags == 0` is composite. The
`WriteFields` `switch` confirms the `fieldType` cases observed on 207.032: `1`/`3` two bytes,
`2` one byte, `4` base-128 varint, `5`/`7`/`0x1a` eight bytes, `6`/`0x17`/`0x19` four bytes,
`8` `0xc`, `9`/`0xb` `0x10`, `0xa` array header, `0xc` `0x1c`, `0xd` utf-16 cstring,
`0xe` utf-8 cstring, `0xf` optional, `0x10`/`0x11`/`0x12` struct/string arrays, `0x13` fixed bytes,
`0x14`/`0x15` length-prefixed bytes, `0x16` `MP_SRV_ALIGN`, `0x18` `MP_SRV_END`. Values remain
build-local.

## Validation asserts (207.032)

- Pair validator `FUN_140fed160` (lines 0x7a/0x7d/0x80/0x81):
  `curr->defArray[0].fieldType == MP_MSGID`, `defSize <= MAX_WORD`, `maxSize <= MAX_WORD`,
  `maxSize <= MSG_MAX_BUFFER_SIZE`.
- Recv installer `FUN_140fed770` (lines 0xe8/0xe9/0xed/0xf8): `ptr->dispatch` non-null,
  `ptr->defArray[0].fieldType == MP_MSGID`, `!target->defArray`,
  `target->defArray[0].maxSize <= MSG_MAX_BUFFER_SIZE`.
- Recv installer `FUN_140fed580` (lines 0x10d/0x10e/0x112/0x11d): same constraints,
  `dispatchType = 0`.
- Send installer `FUN_140fed960` (lines 0xca/0xce): `ptr->defArray[0].fieldType == MP_MSGID`,
  `!target->defArray`.
- The cap is unchanged: decoded size must not exceed `MSG_MAX_BUFFER_SIZE` (`0x2000`).
- Flood policy unchanged: `Msg::DispatchStream` raises `ERR_FLOODING` at `MsgConn.cpp:0xb32`
  against a 1000-tick window (`channel+0x50` count, `channel+0x54` window tick).

## Canonical routines (207.032 build-local)

| Canonical identity | Address |
| --- | --- |
| `Msg::DispatchStream` | `140fe9b80` |
| `MsgPack::ReadFields` | `140fec690` (caller in `Msg::DispatchStream`) |
| `MsgPack::WriteFields` | `140feaf40` |
| `MsgPack::ComputeDefSize` | `140fea290` |
| `MsgPack::ComputeMaxSize` | `140fea310` |
| `MsgChannel::Validate` (pairs) | `140fed160` |
| `MsgChannel::InstallRecv` (dispatchType 1) | `140fed770` |
| `MsgChannel::InstallRecv` (dispatchType 0) | `140fed580` |
| `MsgChannel::InstallSend` (flat) | `140fed960` |
| `MsgChannel::GetOrCreateRegistry` | `140fedae0` |

## MsgConn transport pipeline (207.032)

`MsgConnDispatch` = `FUN_140fea8b0` (its own error strings name the routine). Re-derived fields and
helpers:

| Fact | Value |
| --- | --- |
| connection mode word | `conn+0x108` (values `1` = CLIENT_START, `3` = established) |
| inbound cipher state | `conn+0x12C` (passed to the decrypt helper) |
| inbound frame container | `conn+0x58` |
| mode-1 handshake buffer | `conn+0x80` |
| flags byte | `conn+0x00` (bit0/bit1: open/closed state) |
| last / previous message id | `conn+0x40` / `conn+0x44` |
| recv flood count / window tick | `conn+0x50` / `conn+0x54` |
| resolved recv record | `conn+0x48` (`+0x08` defArray, `+0x10` dispatchType, `+0x18` handlerFn) |
| registry | `conn+0x18` |
| trace callback | `conn+0x390` |

Routines confirmed on 207.032:

| Canonical identity | Address |
| --- | --- |
| `MsgConn::Dispatch` (`MsgConnDispatch`) | `140fea8b0` |
| `Msg::DispatchStream` | `140fe9b80` |
| `MsgUtil::DecryptStream` (inbound RC4/expand) | `140fef220` |
| `MsgUtil::AppendToBuffer` | `140feef40` |
| post-dispatch helper | `140fe9950` |
| frame-kind dispatch table | `PTR_LAB_14210b818` |

Frame kinds remain a `switch` on `buffer[0]` with `buffer[1]` as the frame length; kinds `>= 3` are
rejected (`"Raw dispatch failed"`). Mode `1` appends to `conn+0x80`; mode `3` decrypts through
`conn+0x12C`, appends to the frame container at `conn+0x58`, then calls `Msg::DispatchStream`.

## ChCliContext size (207.032)

The `ChCliContext` allocation grew: its factory `FUN_1411b0940` allocates `0x548` bytes (the prior
contract recorded `0x530`), and the constructor `FUN_1411ae280` writes new tail fields at `+0x530`, `+0x538`
and `+0x540`. Field offsets below the tail are unchanged. The reconstructed `ChCliContext` struct
was updated to `Size = 0x548` with the three provisional tail fields.

## Object structure sizes (207.032)

Every explicit-layout struct in `Gw2.Contracts` was checked against the new image: each declared
`Size` was compared with the allocation sizes of the ~5,978 allocator call sites (`FUN_1409d03a0`)
recovered from the image. 90 of the large (`>= 0x40`) structs match an allocation exactly.

Declared sizes with no matching heap allocation were analysed further:

- Embedded sub-objects consistent with a confirmed parent: `TextureCoordinateTransformStorage`
  (`0xC4` at `FrameContentParams+0x6C`, parent `0x130`), and `ChCliPlayer`'s sub-objects
  `0x78+0x4EA0 -> 0x4F18`, `+0x4108 -> 0x9020`, `+0x60 -> 0x9080`, `+0x6D8 -> 0x9758`,
  `+0x400 -> 0xA178` (parent `0xA178`) — unchanged.
- Changed: `ChCliContext` `0x530 -> 0x548` (struct updated).
- Not yet confirmed (no parent and no matching allocation): `AsContext` `0x3E0`,
  `AsHealthFrameView` `0x284`, `GroundTargeting` `0x74`.

Small embedded structs have no allocation by construction; their sizes are bounded by the confirmed
size of their enclosing struct.

## Not yet re-derived on 207.032

- The `ChCliMsg.cpp` `0x264` handler and the per-id recv/send chains (corpora).
- The `ContextCollection` physical contract (`0x318`, slot `0x13` / `+0x98`) and its TLS accessor;
  the anchor was not isolated. `ChCliContext` is still assumed to resolve through `ContextCollection`
  as in the prior model, and the context-collection note marks this as not revalidated.
- `MsgUtil::Rc4Ksa` and the cipher key-schedule details.
- Live registry merge / flood callback deltas.

Until these are re-proved, the prior build's values for them must not be reused for 207.032, and
the runtime contract gate publishes nothing for `207032`.
