# Inbound message dispatch and transport framing

**Confirmed build(s):** `205.780` (Gw2-64.exe, image SHA-256
`D2AE84876A0B93277FCCB368969046B848BB0403FD09DB420389C813D2459B23`)<br>
**Status:** statically confirmed structure; cipher schedule provisional; **no wire fixture**<br>
**Unresolved:** dispatch-table member [0] type; whether the three-entry table covers all
inbound frames or only handshake/control frames; the exact non-RC4 keying schedule

Evidence level for everything below is **static** (decoded instructions and decompiler output
on the identified image), supplemented by in-image string/assert anchors. There is no captured
replay, so nothing here establishes wire compatibility or a working encoder.

## Owners

These are native `MsgConn` / `MsgUtil` routines. They are the client's own inbound decoder
layer, not the wire codec and not a handler record.

| Identity (durable) | Ghidra address (build-local) | Source unit |
| --- | --- | --- |
| `MsgConn::Dispatch` | `MsgConn_Dispatch` @ `140fe9e50` | `Gw2\Services\Msg\MsgConn.cpp` |
| `MsgRaw::ClientRecvEncrypt` | `MsgRaw_ClientRecvEncrypt` @ `140fe87f0` | `Gw2\Services\Msg\MsgConn.cpp` |
| `MsgRaw::ClientRecvError` | `MsgRaw_ClientRecvError` @ `140fe8a90` | `Gw2\Services\Msg\MsgConn.cpp` |
| `MsgUtil` RC4/MD5 key schedule | `MsgUtil_Rc4Ksa` @ `140feea50` | `Gw2\Services\Msg\MsgUtil.cpp` |
| growable buffer append | `FUN_140fee4e0` @ `140fee4e0` | `Gw2\Services\Msg\MsgConn.cpp` |
| decrypt/expand helper | `FUN_140fee7c0` @ `140fee7c0` | `Gw2\Services\Msg\MsgConn.cpp` |

Addresses are coordinates inside build 205.780 only. The durable identity is the source-unit
routine plus the structural relationship described here.

## Connection state machine

`MsgConn` carries a mode word at **`conn+0x108`**. Values observed directly in the code:

| Value | Symbol (from in-image assert text) | Meaning in the recovered code |
| --- | --- | --- |
| `1` | `MSGCONN_MODE_CLIENT_START` | handshake start; frames are appended to a handshake buffer, not dispatched |
| `2` | `MSGCONN_MODE_ENCRYPTED` | encrypted phase; RC4 key material present at `conn+0x118` (20 bytes) |
| `3` | (no assert text recovered) | established; the raw frame dispatch loop runs |

Assert strings confirming these names exist in the image at `142109628`
(`mc->mode == MSGCONN_MODE_ENCRYPTED`) and `142109980` (`!MSGCONN_MODE_CLIENT_START`).

Transition `2 -> 3` happens in `MsgRaw_ClientRecvEncrypt` immediately after it derives the
20-byte key and calls the key schedule. The key bytes are produced by XOR-ing 20 bytes of
packet data against the 20 bytes at `conn+0x118`; the packet must be at least `0x16` bytes
long, and the frame kind byte must be `0x16` for this path to be taken.

## Frame format

Recovered from the dispatch loop at `140fe9f39`..`140fe9f94`, and consistent with the
bounds checks in the kind handlers:

```text
offset  size  meaning
0x00    1     frame kind, must be < 3 ; selects the dispatch-table entry
0x01    1     frame length in octets, must be >= 2 and <= remaining ; 0 is invalid
0x02    ..    kind-specific payload; the length byte covers the whole frame
```

The loop walks a buffer of length `param_4`:

```text
while (remaining != 0) {
    if (remaining < 2) fail;              // too short for a header
    if (len_field > remaining) fail;      // truncation check
    if (kind > 2) fail;                   // no such dispatch kind
    result = table[kind](out, conn, arg6, frame);
    if (result.status == 0) fail;
    if (len_field == 0) fail;             // would not advance
    frame += len_field; remaining -= len_field;
}
```

Both failure paths populate the same result record with a distinct assert line
(`0xf73` "msgConn already closed", `0xfaf` "Raw dispatch failed"), which is the observable
difference between a closed-connection failure and a malformed-frame failure.


### Dispatch result record

`Dispatch` writes a 0x28-byte record through its first argument. Layout is consistent across
all three failure paths and the success path:

| Offset | Type | Meaning |
| --- | --- | --- |
| `+0x00` | `const char *` | message text; `NULL` on success |
| `+0x08` | `const char *` | function name, always `"MsgConnDispatch"` |
| `+0x10` | `const char *` | source path, `...\MsgConn.cpp` |
| `+0x18` | `u32` | assert line number (`0xf73`, `0xfaf`, `0xc26`, `0xc52`, `0xc58`, `0xc5b`, `0xc2c`) |
| `+0x1c` | `u32` | copy of `conn+0x40` |
| `+0x20` | `u32` | copy of `conn+0x44` |
| `+0x24` | `u32` | status: `0` = failure, `1` = all frames dispatched |

This is a **native error record**, not a wire structure. It must not be serialized by copying
its memory.

## Dispatch table

The table lives at `142109a38` and holds three real code pointers:

| Index | Target | Routine |
| --- | --- | --- |
| `0` | `140fec590` | *not yet defined as a function in the program*; body starts with an assert record (`LEA [142109a10]`, line `0xc61`) |
| `1` | `140fe87f0` | `MsgRaw::ClientRecvEncrypt` |
| `2` | `140fe8a90` | `MsgRaw::ClientRecvError` |

The table is indexed by the frame kind byte, which is bounds-checked to `< 3` before the
indirect call. The Ghidra symbol name `PTR_LAB_142109a38` is misleading — the slots are
genuine code pointers, which the raw bytes `90c5fe4001000000` / `f087fe4001000000` /
`908afe4001000000` confirm as `0x140fec590`, `0x140fe87f0`, `0x140fe8a90`.

## Transport cipher

`MsgUtil_Rc4Ksa` at `140feea50` implements a 256-byte identity permutation, a standard RC4
swap loop over 256 iterations using a 20-byte key (`keylen` capped at `0x14`), and a
256-byte PRGA. Its state object layout:

| Offset | Meaning |
| --- | --- |
| `+0x00` | `u32` reset to 0; carries key/index state |
| `+0x08` | 256-byte S-box |

**This is not plain RC4.** Between the KSA and the PRGA the routine performs MD5-style
mixing on the working words using the constants `0x9fb498b3`, `0x66b0cd0d`, `0x7bf36ae2`,
`0xf33d5697`, `0xd675e47b`, `0xb453c259`, `0x59d148c0`, which belong to the MD5
round-constant family. The exact schedule is **not** reconstructed, and a plain-RC4
implementation will not reproduce the client's output. Capture byte-exact cipher output
before writing any codec.

## What this does and does not establish

Establishes:

- the inbound dispatch loop, its frame header shape, and its bounds checks;
- the three-entry dispatch table and that the kind byte is `0..2` at this site;
- a connection mode word at `conn+0x108` with three observed states and their in-image names;
- that a 20-byte key is derived by XOR at `conn+0x118` and fed to an MD5-derived RC4 schedule.

Does **not** establish:

- that these three kinds are the complete inbound frame space. They may be handshake, control
  and error frames only, with gameplay messages routed by a different mechanism;
- any mapping from frame content to the `0x264` / `0x27C` / `0x200..0x207` message ids;
- the cipher schedule, so no encoder or decoder can be written from this note alone;
- anything about the outbound path, which was not examined.

## Next steps

1. Define a function at `140fec590` and decompile it to identify dispatch kind 0.
2. Find the caller of `MsgConn_Dispatch` and determine whether other frame kinds exist
   elsewhere, which would bound the header's `kind` field.
3. Recover the MD5-derived schedule in `MsgUtil_Rc4Ksa` and produce an independent byte
   fixture for the S-box state after keying.
4. Only then attempt to associate decoded frames with catalog message ids.

---

# Addendum: the message layer above framing

**Status:** statically confirmed structure; schema-driven codec recovered; **still no wire fixture**

The original note stopped at framing. The layer above it — how a frame payload becomes a
typed message and reaches a handler — has now been recovered. This addendum supersedes the
"Next steps" list at the end of the original note.

Nothing here describes sending. No outbound caller, opcode space or send-time cipher state
was analysed.

## Corrected source-unit attributions

Two helpers were previously attributed to `MsgConn.cpp`. Their own embedded asserts put them in
`MsgUtil.cpp`:

| Routine | Ghidra | Assert source path | Corrected unit |
| --- | --- | --- | --- |
| `MsgUtil_CryptStream` | `140fee7c0` | `MsgUtil.cpp:0x38` | `Gw2\Services\Msg\MsgUtil.cpp` |
| `MsgUtil_ReadBytes` | `140fee920` | `MsgUtil.cpp` (`Array.h:0x2d2`) | `Gw2\Services\Msg\MsgUtil.cpp` |

The original note listed `140fee7c0` as an "expand helper" in `MsgConn.cpp`. It is a plain
stream cipher and it lives in `MsgUtil.cpp`. It is also **not** where the MD5 constants are
mixed in — that is still the key schedule at `140feea50`.

## The receive pipeline

`MsgConn::Dispatch` does not decode messages. In mode `3` it hands off:

```
Net_OnClientPacketReceived        14023eb80   network callback; DAT_1426632d0 = game conn
  -> MsgConn_Dispatch             140fe9e50   framing, mode word at conn+0x108
       mode 3 (established):
         MsgUtil_CryptStream      140fee7c0   decrypt in place (RC4-shaped PRGA)
         FUN_140fee4e0            append to the connection's receive buffer
         Msg_DispatchStream       140fe9120   <-- the message layer
```

The important structural result: **the three-entry dispatch table from the original note is
only reached in modes 1 and 2.** In mode 3 the loop never touches it — it goes straight to
`Msg::DispatchStream`. So the `kind` byte is a handshake/control concept, and the earlier
caveat ("may be handshake/control only") is now resolved in that direction. Gameplay messages
do not flow through the three-kind table.

## Msg::DispatchStream — the message layer

`Msg_DispatchStream` @ `140fe9120` (`MsgConn.cpp`), per message:

1. `MsgUtil_ReadBytes(ctx+0x80, 2, 4, &msgId, 0)` — read a **u32 message id**. This is the
   first field of the payload; ids are read as 4 bytes, little-endian into a qword-sized dest.
2. `Msg_RegistryRecordRecv(registry, msgId)` — flood accounting, window 1000 ticks
   (`FUN_1409cdda0` clock). Exceeding it raises `ERR_FLOODING` (line `0xb32`).
3. `Msg_RegistryLookupById(registry, msgId)` — resolve the record. Miss raises
   `ERR_NO_HANDLER` (line `0xb28`).
4. Assert `recvMsgPacked->defArray[0].defSize != 0` (line `0xa5f`).
5. Allocate `defArray[0].defSize` bytes from the arena (`ctx+0xc8`) and decode with
   `MsgPack_ReadFields(record->defArray, cursor, payloadEnd, decoded, decoded+defSize, ...)`.
6. Call `(*(record+0x18))(ctx, decoded)`. A return of 0 raises `ERR_DISPATCH_FAILED`
   (line `0xb3a`), unless the connection is in the ignore state.
7. Optional observer at `ctx+0x390`: virtual `(*(vtable+0x10))(this, &payload, &len)`.
8. `thunk_FUN_140fedbe0(...)` records per-id dispatch statistics.

### The registry is a direct-indexed array, not a hash map

`Msg_RegistryLookupById` @ `140fed3b0` is five instructions of real work:

```c
u32   count  = *(u32*)(registry + 0x7c);
void* base   = *(void**)(registry + 0x70);
if (msgId >= count) return 0;
record = base + msgId * 0x20;             // stride 0x20 = 32 bytes
if (*(void**)(record + 8) == 0) return 0; // null defArray == unregistered
return record;
```

**`msgId` indexes the table directly — no hashing, no binary search.** Message ids are
therefore dense over `0 .. count-1`, and `count` is bounded by the table allocation. This is
the single most useful fact for enumerating the protocol: a walk of `0..count-1` enumerates
every registered inbound message.

### Handler record layout (`MsgRegistryRecord`, 32 bytes)

| Offset | Type | Field | Evidence |
| --- | --- | --- | --- |
| `0x00` | `u32` | `msgId` | `*(uint*)(lVar3 + base) = param_1` in registrar |
| `0x08` | `pointer` | `defArray` | null check in lookup; `defArray[0].defSize` assert |
| `0x10` | `u32` | `dispatchType` | switch in `Msg_DispatchStream`; assert at `142109840` |
| `0x18` | `pointer` | `handlerFn` | indirect call `(*(code**)(lVar7 + 0x18))` |

`dispatchType` selects between two handler call shapes:

- `0` — `handler(param_3, decoded)`
- `1` — `handler(*param_3, decoded)` (dereferences the connection context first)

Any other value hits `No valid case for switch variable 'mc->recvMsgPacked->dispatchType'`
(`142109840`, line `0xa98`) and is non-returning.

## The schema: MsgPackFieldDef

Messages are described by a linked descriptor chain, not by hand-written code. Struct
confirmed against `ADD RBX,0x28` (40-byte stride) in `MsgPack_ComputeDefSize`:

| Offset | Type | Field | Notes |
| --- | --- | --- | --- |
| `0x00` | `u32` | `fieldType` | indexes `DAT_142109500`; see enum |
| `0x04` | `u32` | `flags` | read as the second dword of the size-table entry |
| `0x08` | `u32` | `param` | element/array count; `maxSize` uses `+8` when non-zero |
| `0x0C` | `u32` | `elementCount` | used by nested types |
| `0x10` | `pointer` | `refTypeDef` | sub-struct for types `0xf..0x12`; assert `def->refTypeDef` |
| `0x18` | `u32` | `defSize` | cached fixed size |
| `0x1C` | `u32` | `maxSize` | cached worst-case size |
| `0x20` | `pointer` | `nextDef` | chain terminator when `fieldType == 0x18` or `== 0` |

`defSize`/`maxSize` are lazy: both compute functions fill the cache only when it is 0.

### Size table at `142109500`

26 entries of 8 bytes, `{size:dword, flags:dword}`, indexed by `fieldType * 8`:

| Idx | Size | Idx | Size | Idx | Size | Idx | Size |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 0 | 0 | 7 | 1 | 14 | 1 | 21 | 1 |
| 1 | 1 | 8 | 4 | 15 | 12 | 22 | 1 |
| 2 | 2 | 9 | 8 | 16 | 1 | 23 | 28 |
| 3 | 1 | 10 | 1 | 17 | 16 | 24 | 1 |
| 4 | 1 | 11 | 4 | 18 | 1 | 25 | 8 |
| 5 | 1 | 12 | 1 | 19 | 16 | | |
| 6 | 2 | 13 | 8 | 20 | 0 | | |

`MsgPack_ComputeMaxSize` then overrides for composite types: `4 -> +5`, `10 -> +0x11`,
`0xd/0xe/0x13 -> param+8`, `0xf -> inner+8`, `0x10 -> inner*param+8`,
`0x11 -> inner*param+9`, `0x12 -> inner*param+10`, `0x14 -> defSize+9`, `0x15 -> defSize+10`.

### Field type enum (`MsgPackFieldType`)

Exact names are confirmed only where an in-image assert or error string spells them out
(`MP_MSGID`, `MP_OPTIONAL`, `MP_SRV_ALIGN`, `MP_SRV_END`). The rest are inferred from the
decode behaviour and the size table:

| Value | Name | Wire size | Confidence |
| --- | --- | --- | --- |
| `0x00` | `MP_NONE` | terminal | high |
| `0x01` | `MP_MSGID` | 2 | **name confirmed** (`def[0].fieldType == MP_MSGID`) |
| `0x02` | `MP_CSTRING` | 1 | inferred (1-byte copy) |
| `0x03` | `MP_U16` | 2 | inferred |
| `0x04` | `MP_OPTIONAL` | shared tail w/ array | **name confirmed** |
| `0x05` | `MP_F64` | 8 | inferred |
| `0x06` | `MP_U32` | 4 | inferred |
| `0x07` | `MP_BOOL` | 8 | inferred (shares case with `0x5`) |
| `0x08` | `MP_VEC3` | 0xc | inferred |
| `0x09` | `MP_VEC4` | 0x10 | inferred |
| `0x0a` | `MP_ARRAY` | 0xc header + n | inferred |
| `0x0c` | `MP_PADDING` | 0x1c | inferred |
| `0x0d` | `MP_STRING8` | var, 1-byte len | inferred |
| `0x0e` | `MP_STRING16` | var, 2-byte len | inferred |
| `0x0f` | `MP_STRUCT` | inner+8 | asserted `def->refTypeDef` |
| `0x10` | `MP_STRUCT_ARRAY` | inner*param+8 | asserted |
| `0x11` | `MP_STRING_ARRAY` | inner*param+9 | asserted |
| `0x12` | `MP_STRING16_ARRAY` | inner*param+10 | asserted |
| `0x13` | `MP_U8` | 4 | inferred |
| `0x14` | `MP_BLOB8` | 1 + n | inferred (bounds-checked) |
| `0x15` | `MP_BLOB16` | 2 + n | inferred (bounds-checked) |
| `0x16` | `MP_SRV_ALIGN` | 0 | **name confirmed**; errors if data present |
| `0x17` | `MP_F32` | 4 | inferred |
| `0x18` | `MP_SRV_END` | 0 | **name confirmed**; chain terminator |
| `0x1a` | `MP_F64` (alt) | 8 | inferred |

Type-name values are build-local. Re-derive them for any other build — do not carry the
numbers across.


## The codec

| Routine | Ghidra | Role |
| --- | --- | --- |
| `MsgPack::ReadFields` | `140febc30` | decode: descriptor walk, bounds-checked |
| `MsgPack::WriteFields` | `140fea4e0` | encode: same walk, **no bounds check** |
| `MsgPack::ComputeDefSize` | `140fe9830` | fixed-size pass, memoised into `defSize` |
| `MsgPack::ComputeMaxSize` | `140fe98b0` | worst-case pass, memoised into `maxSize` |
| `MsgPack::DecodeMessage` | `140fec5d0` | entry: sizes the buffer, then reads |
| `MsgPack::ReadStringOrArray` | `140fec3c0` | shared tail for types `4` and `10` |
| `MsgUtil::CryptStream` | `140fee7c0` | RC4-shaped PRGA |
| `MsgUtil::ReadBytes` | `140fee920` | byte reader over a growable array |

The reader bounds-checks every field against the payload end and returns 0 on overrun. The
writer does **not** — it trusts the caller to have pre-sized via `ComputeMaxSize`. That
asymmetry is worth preserving in any reimplementation: a length the writer accepts may not be
one the client's reader accepts. Do not assume a codec validated against the writer is
accepted by the reader.

### CryptStream is RC4 PRGA but not stock RC4

`MsgUtil_CryptStream` expands the textbook schedule exactly:

```
i = (i + 1) & 0xff
j = (j + S[i]) & 0xff
swap(S[i], S[j])
out[k] = S[(S[i] + S[j]) & 0xff] ^ in[k]
```

State (`i`, `j`, `S[256]`) persists across calls, so this is a **stateful stream cipher** and
cannot be fingerprinted from, or replayed for, a single frame. The unresolved part remains the
KSA at `140feea50` where MD5-family constants are mixed into the permutation.

## What this establishes

- the complete inbound path from packet callback to handler invocation;
- that the three-kind frame table is handshake/control only, **not** the gameplay opcode space;
- that message ids are a dense `0..count-1` index space over a 32-byte-stride table;
- the `MsgRegistryRecord` layout, including the two `dispatchType` call shapes;
- the `MsgPackFieldDef` schema format, its lazy size cache, and the size table;
- a field-type enum with four names confirmed from asserts;
- that the wire format is **schema-driven**, not per-message hand code;
- the reader/writer bounds-check asymmetry.

## What this does not establish

- **No wire fixture.** Everything is static. No frame has been decoded against a capture, so
  no claim of wire compatibility is made and none should be inferred.
- **The registry instance is runtime-populated.** Only the lookup is recovered. The static
  base address, the value of `count`, and the concrete `defArray` for any message id are not
  yet located. The catalog's `0x264` handler record is **not** tied to a registry slot.
- Whether the u32 at step 1 is the same id space as the `0x264`-style catalog ids.
- The KSA, so no encoder or decoder can be written end to end yet.
- Outbound: callers of `MsgPack_DecodeMessage` (18+ sites) are candidate senders but were not
  analysed.

## Next steps (revised)

1. Locate the registry instance and read `count` from `registry+0x7c`. That fixes the id-space
   size and lets the table be enumerated statically.
2. Dump the `defArray` chain per slot across `0..count-1` and emit it as a machine-readable
   schema corpus — this is now mechanical, not investigative.
3. Recover the KSA at `140feea50` and produce an independent byte fixture for the S-box and the
   first N keystream bytes.
4. Take one captured frame and decode it end to end against a dumped schema.
5. Only then attempt to reconcile registry ids with catalog message ids.

The previous steps 1 and 2 (define `140fec590`; bound the frame `kind` field) are resolved: the
kind table is handshake/control only, and mode-3 traffic never reaches it.

---

# Addendum 2: the static message registry

**Status:** schema corpus located and partially enumerated; **still no wire fixture**

The previous addendum's next step 1 was "locate the registry instance". The static side of it is
now located. This does **not** close that step — the *runtime* record array is still not read —
but the static tables that populate it are, and they yield concrete message ids.

## New source unit: MsgChannel.cpp

Registration lives in `Gw2\Services\Msg\MsgChannel.cpp`, a third file alongside `MsgConn.cpp`
and `MsgUtil.cpp`. Its asserts use the same shape as the others.

## Registration path

```
static initialiser (one per channel)
  -> MsgChannel_RegisterRecvOnly      140fed7f0
       (u32 channelId, u32 protocol, u32 count, MsgChannelEntry* table, u32 flags)
  -> MsgChannel_RegisterBidirectional 140fed670  (adds a second, send-side table)

  each does:
    1. MsgChannel_ValidateTableFlat/Pairs   validate every entry (MsgChannel.cpp)
    2. EnterCriticalSection(DAT_142890034)  guarded by DAT_142890030
    3. FUN_140fed080(channelId, protocol)   resolve the per-channel registry
    4. FUN_140fecd10 / FUN_140fecb20 / FUN_140fecf00   install
```

**Validation runs before the lock is taken and only reads static tables.** It is a startup
self-check of the shipped schema corpus, not a runtime guard.

### Validated bounds (MsgChannel.cpp line numbers in-image)

| Assert | Line | Meaning |
| --- | --- | --- |
| `curr->defArray[0].fieldType == MP_MSGID` | `0x7a` | every entry's first field must be the id |
| `defSize <= MAX_WORD` | `0x7d` | 0xFFFF |
| `maxSize <= MAX_WORD` | `0x80` | 0xFFFF |
| `maxSize <= MSG_MAX_BUFFER_SIZE` | `0x81` | **0x2000 — the binding bound** |

So **no message in this build may exceed 8192 bytes decoded**. That is a hard, static fact about
the corpus and a useful sanity check for any reimplementation.

## The static table format

21 registrars exist. Their channel ids: `2, 4, 5, 0xd, 0x12, 0x13, 0x14, 0x19, 0x1c, 0x22,
0x24, 0x26, 0x2b, 0x2c, 0x31, 0x33, 0x35, 0x36, 0x37, 0x3a, 0x3b`.

`MsgChannelEntry` (16 bytes):

| Offset | Type | Field |
| --- | --- | --- |
| `0x00` | `void*` | `defArray` — the MsgPackFieldDef chain, or points into `.text` for descriptors embedded in code |
| `0x08` | `void*` | `handlerFn` — dispatch target, called `(ctx, decoded)` |

Note the static tables are grouped **per channel**, while the runtime record array is a flat
**dense id index**. Registration translates one into the other, which is why ids in the record
array are contiguous even though the tables are grouped.

## Enumerated ids

Read from descriptor `[0]` of each entry (`fieldType` verified `== 1` / `MP_MSGID`, id taken from
`+0x10`):

| Channel | Table | Entries | Message ids | Consecutive |
| --- | --- | --- | --- | --- |
| `2` | `1421350d8` | 2 | `0x2EB`, `0x2EC` | yes |
| `5` | `142138a30` | 5 | `0x11`, `0x12`, `0x13`, `0x14`, `0x16` | gaps |
| `0xd` | `14217cdf0` | 13 | `0x2DE`..`0x2E5` at entries 0..7 | yes |

Handler signatures confirmed against the `dispatchType == 0` shape `(ctx, decoded)`, e.g.
`FUN_1410f1830` (`1410f1830`, 0x31 bytes) and `FUN_1410f15d0` for channel 5.

**The dense-index hypothesis is confirmed.** Channel `0xd`'s eight schema entries carry ids
`0x2DE` through `0x2E5` with no gaps, and channel `2`'s carry `0x2EB`/`0x2EC`. Ids are allocated
contiguously across the corpus, not per channel.

### Two caveats on the enumeration

- Entries 8 and 12 of channel `0xd` point at `140348050` and `14038C050`, inside `.text`. Those
  are descriptors embedded in code rather than in `.rdata`; they were not decoded. Entry 9 points
  at `1412bfd80`, a thunk (`MOV RAX,[RCX+0x20]`), not a descriptor.
- The ids above are **read from the static tables**. They are not yet reconciled with the runtime
  record array, and nothing ties any of them to a catalog entry.

## What this establishes

- the registration path, its source unit, and that 21 channels exist with the ids listed;
- the `MsgChannelEntry` layout and the flat/pairs table variants;
- that validation proves `maxSize <= 0x2000` for every message at startup;
- concrete message ids for channels `2`, `5` and `0xd`, confirming ids are dense and allocated
  contiguously;
- handler entry points per channel with the `(ctx, decoded)` calling shape.

## What this does not establish

- **The runtime record array is still not read.** `FUN_140fed080`'s object is resolved by
  (channelId, protocol) at runtime; its base, its `count` at `+0x7c` and every installed record
  remain unread. The static tables show what *should* be installed, not what is.
- **The remaining 18 channels were not enumerated.** Only `2`, `5` and `0xd` were walked.
- The `dispatchType` value per entry (the static tables do not obviously carry it) and the
  mapping from `channelId`/`protocol` to the id space.
- Any id to catalog message id mapping. `0x264` is **not** among the ids enumerated here.
- **Still no wire fixture.** Nothing here is a decoded frame.

## Next steps

1. Walk the remaining 18 channels' tables and emit the full id corpus. The method is proven; this
   is mechanical.
2. Read `FUN_140fed080` to recover how (channelId, protocol) maps into the flat id space, then
   read `count` from a live instance if a debugger session is available.
3. Decode the two `.text`-embedded descriptors on channel `0xd` (entries 8 and 12).
4. Recover the KSA at `140feea50` (unchanged).
5. Reconcile ids with catalog entries, then take a captured frame end to end.

---

# Addendum 3: extraction tooling and corrected registration counts

**Status:** extractor implemented and validated; counts corrected; **still no wire fixture**

## Correction: there are 59 registration sites, not 23

Addendum 2 reported 21 registrars and "18 channels remaining". Both numbers were wrong. That
count came from `get_function_callers` on one registrar, which was an incomplete list. An
instruction-level search for the call itself gives the real figures:

| Registrar | Ghidra | Call sites | Table shape |
| --- | --- | --- | --- |
| `MsgChannel_RegisterRecvOnly` | `140fed7f0` | **21** | one flat table |
| `MsgChannel_RegisterTablePair` | `140fed730` | **36** | recv table + send table |
| `MsgChannel_RegisterBidirectional` | `140fed670` | **2** | recv table + send table, both validated as pairs |

**Total 59 registration sites.** `MsgChannel_RegisterTablePair` was previously unknown — it is
the most common form and it carries a **send-side table**, which means the outbound schema corpus
exists in the same static form as the inbound one and had simply not been looked at.

Lesson recorded because it cost a rework: `get_function_callers` under-reported. Any figure that
matters should come from an instruction search on the call target, not a caller query.

## Scale of the corpus

The largest site is channel `0x14` (`FUN_14020a000`):

```c
MsgChannel_RegisterTablePair(0, 0, 0xdc, recvTbl, 0x270, sendTbl, 8);
```

`0xdc` = **220 recv entries**, `0x270` = **624 send entries**. So the recv schema corpus is on
the order of several hundred messages, and the send corpus is roughly 3x larger. The outbound
side is the bigger target.

## Extractor

`tools/re/Extract-MsgRegistry.ps1` decodes a table dump into message records. Input is JSON:
one object per table, each entry carrying the `defArray` pointer, the handler address and the
first 40 bytes of the descriptor chain read from the image.

It refuses rather than guesses:

- a descriptor shorter than 40 bytes **throws**, naming the exact entry;
- a first descriptor whose `fieldType != 1` is **skipped and named**, never decoded as an id;
- an `MP_ARRAY` (`fieldType 0x0a`) envelope is skipped with an explicit reason, since the id
  sits in the descriptor the array wraps and that inner descriptor is not walked.

### Validated behaviour

| Case | Result |
| --- | --- |
| channel 14, 8 entries | 8 decoded, 0 skipped |
| first descriptor set to `fieldType 0x0a` | 7 decoded, 1 skipped, reason printed |
| descriptor truncated to 8 bytes | throws `descriptor is 8 bytes, need 40` naming the entry |
| malformed JSON input | fails at parse, no partial output |

The truncation case was not hypothetical: the first run of this tool failed on it because the
descriptor bytes had been read as 24 bytes instead of 40. The check caught a real error in the
data rather than emitting eight wrong ids.

## Channel 14 recv ids (first 8 of 220)

| Id | Handler |
| --- | --- |
| `0x1C` | `1410F1830` |
| `0x1E` | `1410F15D0` |
| `0x1F` | `1410F1690` |
| `0x20` | `1410F1650` |
| `0x21` | `1410F1870` |
| `0x22` | `1410F1830` |
| `0x23` | `1410F15D0` |
| `0x24` | `1410F1690` |

Handlers are reused across ids (`1410F1830` serves `0x1C` and `0x22`), so the handler address
is not a message identity — one handler can serve several ids.

## What this establishes

- the true registration-site count, 59, and the existence of the `TablePair` registrar;
- that a send-side (outbound) schema corpus exists statically and is far larger than the
  inbound one;
- channel `0x14`'s size: 220 recv, 624 send;
- an extractor whose failure mode is a named error rather than a plausible-looking wrong value;
- eight channel-14 recv ids with their handlers.

## What this does not establish

- **212 of channel 14's 220 recv entries, and all 624 of its send entries, are unwalked.**
  A single-entry walk per table was done; the bulk is not enumerated.
- **58 of 59 registration sites are unwalked.** Only channel 14 was extracted, and only its
  first 8 entries.
- The `MP_ARRAY` envelope form is not unwrapped, so any message using it has no id recorded.
- `maxSize <= 0x2000` was asserted from the validator's code but has **not** been checked
  across a real corpus, since the corpus is not yet extracted. The check is currently a claim
  about the validator, not about the data.
- Nothing is reconciled with the runtime record array or with any catalog entry. `0x264` is
  still not among the ids found.
- **Still no wire fixture.**

## Next steps

1. Bulk-read the channel-14 tables (220 + 624 pointers) and run the extractor over them. The
   tool and the format are proven; this is now data entry, not investigation.
2. Check `maxSize <= 0x2000` across the extracted corpus, and treat a violation as a decode bug
   rather than a game bug — it would mean a descriptor offset is wrong.
3. Unwrap the `MP_ARRAY` envelope so those ids are not lost.
4. Walk the remaining 58 sites, largest first.
5. Recover the KSA at `140feea50` (unchanged).
6. Reconcile ids with catalog entries, then take a captured frame end to end.


---

# Addendum 4: bulk extraction, and two corrections it forced

**Status:** channel `0x14` recv table fully extracted (220 elements); **still no wire fixture**

## Correction 1: a table element is TWO schema pointers, not schema+handler

Addendum 2 recorded `MsgChannelEntry` as `{void* defArray; void* handlerFn}`. **The second
column is not a handler.** It is a second `defArray` pointer.

The bulk walk exposed it. Column B of element 0 at `142167030` emitted `1425b6420`, which the
hand-verified cross-check flagged as not matching the expected handler `1410f1830`. Peeking the
pointer settled it:

| Address | Block | Function | fieldType | id |
| --- | --- | --- | --- | --- |
| `1425b6420` | `.data` | none | 1 (`MP_MSGID`) | `0x1e` |
| `1425b6300` | `.data` | none | 1 (`MP_MSGID`) | `0x1c` |
| `141246040` | `.text` | `FUN_141246040` | — | — |

`1425b6420` is a valid descriptor with an id, in `.data`, belonging to no function. It is a
**schema pointer**, not a code address. So `MsgChannelEntry` is `{defArrayA, defArrayB}`.

Raw bytes at `142167030`, confirmed byte-for-byte:

```
entry0 = 00635b4201000000 | 20645b4201000000   -> 1425B6300, 1425B6420
entry1 = 40655b4201000000 | b0665b4201000000   -> 1425B6540, 1425B66B0
entry2 = 40685b4201000000 | 80695b4201000000   -> 1425B6840, 1425B6980
```

Decoded: element 0 = ids `0x1c` + `0x1e`; element 1 = `0x1f` + `0x20`; element 2 = `0x21` + `0x22`.
That matches the hand-verified sequence `0x1c, 0x1e, 0x1f, 0x20, 0x21, 0x22, 0x23, 0x24` exactly.

## Correction 2: a `String.format` column shift, caught by the cross-check

The first scripted run produced 220 rows that *looked* plausible and were wrong:

| Symptom | Cause |
| --- | --- |
| "handler" column held `1425b6420` | a defArray pointer, per Correction 1 |
| ids `1c, 1f, 21, 23, 25` (every other) | every id was a real id, but read from the wrong column |
| 220 rows, 176 distinct ids | duplicates from the same shift |

The hand-verified 8 entries from `ch14-recv.json` — committed *before* the script existed — are
what caught it. Two rows of "plausible" output would have entered the corpus silently otherwise.
**This is the entire argument for keeping a small hand-verified sample as ground truth.** The
mismatch was visible on the first row.

Two mechanical defects followed from it and were fixed: `String.format("%d", long)` mixing that
shifted arguments, and `ConvertTo-Json` serializing a PowerShell `FileInfo` object instead of
the file's text.

## The table is heterogeneous

Column B is not uniform across the 220 elements. There is **exactly one transition, at index 110**:

| Index range | Column A | Column B |
| --- | --- | --- |
| 0–109 | `MP_MSGID` schema (220/220 in col A overall) | `MP_MSGID` schema |
| 110–219 | `MP_MSGID` schema | pointer into `.text`, inside a function |

So the table mixes two kinds of entry: schema-paired messages, and messages with a code handler.
Column A is `OK` for all 220; column B is `OK` for 110 and points into code for the other 110.
A consumer that assumes one uniform shape will mis-read half the table.

## Channel 0x14 recv ids

- 220 elements, column A all valid `MP_MSGID`
- ids range `0x18` to `0x1D5`
- **176 distinct ids across 220 elements** — so ids repeat; the element index is not the id

`0x264` **is absent** from channel `0x14`'s recv table.

## What this establishes

- `MsgChannelEntry` is `{defArrayA, defArrayB}`, both schema pointers, corrected from `{defArray, handlerFn}`;
- the table form is heterogeneous with one clean boundary at index 110;
- channel `0x14` recv is fully enumerated: 220 elements, 176 distinct ids, `0x18`–`0x1D5`;
- `0x264` is not in channel `0x14` recv;
- a scripted walk can be validated against a hand-verified sample, and was.

## What this does not establish

- **`0x264` was only searched in channel `0x14` recv.** Its absence there says nothing about the
  other 58 registration sites. This does not close the `identity-mapping` question.
- **The two CSVs from the first, incorrect run were deleted** rather than committed:
  `ch14-recv-full.csv` and `ch14-send-full.csv`. What remains is `ch14-recv-pairs.csv`, which is
  the corrected output for the recv table only.
- **The send table (`142167710`, 624 entries) has not been extracted with the corrected script.**
  Its earlier "624 ok" result used the flawed column reading and must be re-run, not trusted.
- Ids are still not reconciled with the runtime record array or any catalog entry.
- `maxSize <= 0x2000` is still unchecked against the corpus — the extractor does not yet compute it.
- **Still no wire fixture.**

## Next steps

1. Re-run the corrected extractor over the send table `142167710` (624 elements).
2. Characterize the `.text` pointers in column B, indices 110–219: resolve them to functions.
3. Walk the remaining 58 sites with the corrected script.
4. Assert `maxSize <= 0x2000` across the extracted corpus.
5. Search all sites for `0x264` to settle the `identity-mapping` question.
6. Recover the KSA at `140feea50`.


---

# Addendum 5: full 59-site sweep, and a partial-failure boundary

**Status:** 51 of 59 sites extracted cleanly; 8 sites suspect and quarantined;
**still no wire fixture**

## Discovery, done programmatically

All 59 registration sites were found by scanning for `CALL rel32` targeting the three
registrar entry points, rather than transcribed by hand. The per-registrar split came out
21 / 36 / 2 — **independently reproducing** the instruction-search count from Addendum 3 by a
different method. Two independent routes agreeing is the first time a count in this note has
had that.

## Three harness defects found and fixed, all mine

Each produced plausible-looking wrong output before being caught.

**1. Table operand not an `Address`.** A `LEA R9,[0x1421350d8]` displacement comes back from
Ghidra as a **`Scalar`**, not an `Address`. The first version tested `instanceof Address` and
emitted `?` for every table while still decoding the `R8D` counts correctly — a signature that
looked like "counts work, tables don't".

**2. Two calling patterns, not one.** Two sites (`14021fc0a`, `140222bfa`) pass arguments on
the **stack**, not in registers:

```asm
LEA RAX,[0x1422e4568]
MOV qword ptr [RSP+0x28],RAX     ; table
MOV dword ptr [RSP+0x20],0x2     ; count
CALL 0x140fed730
```

A register-only window reader reports nothing for these. They are the 2 `NOARGS` rows.

**3. Unmapped-pointer crash.** `Memory.getBlock()` returns null for a pointer into no mapped
block, and the first full run **crashed** on it. Now recorded as `PTR_UNMAPPED` rather than
thrown.

## The result

| Tag | Rows | Meaning |
| --- | --- | --- |
| `OK` | 854 | valid `MP_MSGID` descriptor |
| `CODE` | 352 | pointer into `.text` (handler, per Addendum 4) |
| `PTR_UNMAPPED` | 14 | pointer into no mapped block |
| `NULL` | 13 | zero column |
| `NOARGS` | 2 | argument setup not recovered |
| `BADFT` | 1 | first descriptor not `MP_MSGID` |

**The 14 `PTR_UNMAPPED` and 1 `BADFT` are my bugs, not binary features.** The giveaway is that
the "pointers" are UTF-16 text fragments:

| Reported pointer | As ASCII | Actually |
| --- | --- | --- |
| `7466656c` | `lfet` | fragment of a UTF-16 string |
| `6f4378614d74756f` | `outMaxCo` | fragment of a UTF-16 string |

A descriptor pointer cannot be two ASCII characters. Those rows are mis-decoded arguments
where my 14-instruction window picked up a `LEA` from neighbouring unrelated code.

## The boundary: 51 clean, 8 suspect

| | Sites |
| --- | --- |
| Clean (only `OK`/`CODE`/`NULL` rows) | **51** |
| Suspect (any `UNMAPPED`/`BADFT`/`NOARGS`) | **8** |

The 8 suspect sites, named so they can be re-derived and are not silently trusted:
`14020a8e1`, `14021b991`, `14021cf01`, `14021df71`, `14021f1d1`, `14021fc0a`, `1402201f1`,
`140222bfa`.

**Site `14021cf01` is the worst** — 16 rows, all suspect. Its reported "pointers" include
`1000000000572613`, which is not a plausible address.

## Verification against ground truth

Site `14020a031` (channel `0x14`) reproduces the hand-verified sequence exactly:

```
col0 OK 1c  1425b6300  .data
col1 OK 1e  1425b6420  .data
col0 OK 1f  1425b6540  .data
col1 OK 20  1425b66b0  .data
col0 OK 21  1425b6840  .data
col1 OK 22  1425b6980  .data
```

That matches the committed `ch14-recv.json` values field for field. **This is the check that
justifies trusting the 51 clean sites.**

## Corpus totals (clean subset only)

- 854 `OK` descriptor rows, **677 distinct ids**
- id range `0x01` to `0x516`
- 352 `CODE` pointers, consistent with Addendum 4's finding that column B can be a handler

## `0x264` is still not found

Searched across all 854 cleanly-decoded descriptors: **absent**. Combined with Addendum 4's
negative result on channel `0x14`, `0x264` is now negative in every site decoded successfully.

**This does not yet close the `identity-mapping` question**, because 8 sites remain suspect, and
`0x264` could sit in one of them. Closing it requires the 8 to be re-derived.

## What this does not establish

- **8 of 59 sites are not trustworthy.** Their rows are in `all-sites.csv` tagged as suspect, but
  the tags come from the same argument decoder that produced them, so a site could be
  mis-decoded *and* mis-tagged as clean. Only `14020a031` was independently verified.
- The `OK` rows are descriptor decodes; the `CODE` rows are **not** decoded at all, so 352
  column-B values are pointers of unknown meaning.
- **Argument recovery is a heuristic**, not a proof. Sites with more than one table argument or
  unusual register allocation may decode wrongly without being flagged.
- `maxSize <= 0x2000` is still unchecked — the extractor does not compute it.
- Nothing is reconciled with the runtime record array or any catalog entry.
- **Still no wire fixture.** `wireVerified` remains false.

## Next steps

1. Re-derive the 8 suspect sites by reading their arguments from the registrar call rather than a
   fixed instruction window. `14021cf01` and `1402201f1` first.
2. Compute `maxSize` per descriptor and assert `<= 0x2000` across the clean corpus.
3. Decode the 352 `.text` pointers to functions, to name handlers.
4. Re-search `0x264` after the 8 suspect sites are resolved. Only then is the absence meaningful.
5. Recover the KSA at `140feea50`.


---

# Addendum 6: the two table shapes, read from the validators

**Status:** stride error found in Addendum 4's walk; layout now resolved from source asserts
**This addendum supersedes the layout claims in Addendum 4 and part of Addendum 2.**

## Resolved from the registrar decompilation

`FUN_140fed730` (`MsgChannel_RegisterTablePair`), decompiled:

```c
void FUN_140fed730(int param_1, int param_2, uint param_3, longlong *param_4,
                   uint param_5, longlong *param_6, undefined4 param_7)
{
  MsgChannel_ValidateTableFlat(param_3, param_4);    // table A
  MsgChannel_ValidateTablePairs(param_5, param_6);   // table B
  ...
}
```

Signature: **7 parameters**, two tables with two counts. `get_function_signature` reports
`param_count: 7`, consistent.

## The two validators define the two shapes

Both call `FUN_1409dda80` with source-quoted assert strings, which are far more reliable than
anything inferred from disassembly:

| Validator | Address | Assert | File:line |
| --- | --- | --- | --- |
| `MsgChannel_ValidateTableFlat` | `140fec7d0` | `curr->defArray[0].fieldType == MP_MSGID` | `MsgChannel.cpp:0x7a` |
| `MsgChannel_ValidateTablePairs` | `140fec700` | *(identical assert)* | `MsgChannel.cpp:0x7a` |
| both | — | `defSize <= MAX_WORD` | `:0x7d` |
| both | — | `maxSize <= MAX_WORD` | `:0x80` |
| both | — | `maxSize <= MSG_MAX_BUFFER_SIZE` | `:0x81` |

**Every pointer in both tables is a defArray.** Both validators dereference `*param_2` and
assert it is a `defArray` whose first field is `MP_MSGID`. This confirms Addendum 4's correction
and **disproves** the `{defArray, handlerFn}` reading that Addendum 2 (and a plate comment in
`FUN_140fed080`) carried.

`maxSize <= 0x2000` is now **verified from source**, not inferred. `MSG_MAX_BUFFER_SIZE` is
asserted at `MsgChannel.cpp:0x81`. This closes the "asserted but unchecked" caveat.

## The difference between the shapes is stride

```c
/* ValidateTableFlat */   puVar1 = param_2 + (param_1 & 0xffffffff);  // +1 pointer  = 8 bytes
                          param_2 = param_2 + 1;

/* ValidateTablePairs */  puVar2 = param_2 + (ulonglong)param_1 * 2;  // +2 pointers = 16 bytes
                          param_2 = param_2 + 2;
```

| Table | Shape | Stride |
| --- | --- | --- |
| A | flat pointer array | **8 bytes** |
| B | pair array | **16 bytes** |

`ValidateTablePairs` walks **2n** pointers for `n` entries, i.e. each element is `{ptrA, ptrB}`.
`ValidateTableFlat` walks **n** pointers with no pairing.

## Why this matters: Addendum 4 used the wrong stride

Addendum 4 walked channel `0x14` with a **16-byte stride** and read `{pA, pB}` at `+0` and `+8`.
Table A is flat, so a 16-byte stride **skips every other entry**. That is the real explanation for
the "every other id" symptom of `1c, 1f, 21, 23, 25` — I attributed it to a `String.format`
column shift and fixed the formatting, but **the stride was the actual error**, and it is still
live in the committed extraction.

**Arithmetic confirmation, independent of the decompiler.** Table A is at `142167030` with 220
entries; table B is at `142167710`.

| Stride | Byte extent | Result |
| --- | --- | --- |
| 8 (flat) | 220 x 8 = `0x6E0` | `142167030 + 0x6E0` = **`142167710`** — exactly table B's start |
| 16 | 220 x 16 = `0xDC0` | runs 1760 bytes **past** into table B |

The tables are exactly adjacent under the 8-byte stride. That is a second, independent
confirmation that table A is flat, and it does not depend on my reading of the decompiler at all.

## The Addendum 4 "index 110 boundary" is an artefact

Addendum 4 reported a single clean transition at index 110 where column B stopped being a schema
and became a `.text` pointer. Under a 16-byte stride on a flat 220-pointer table, index 110 lands
at byte offset `110 x 16 = 1760` = `0x6E0` — **exactly the end of table A**. So "index 110" was
the boundary of the table, not a structural feature inside it. The transition is real; the
interpretation as a heterogeneous table was wrong.

## Consequences for the committed corpus

- **`ch14-recv-pairs.csv` is invalid.** It was produced with the 16-byte stride on a flat table and
  its "pairs" are an artefact. It must be regenerated, not interpreted.
- **`all-sites.csv` is affected.** The sweep used a 16-byte stride for both tables. Clean sites are
  those where the wrong stride still happened to land on valid descriptors; the 8 "suspect" sites
  may be stride damage rather than argument-recovery damage. **Both diagnoses need re-testing.**
- The Addendum 5 partition (51 clean / 8 suspect) is **not trustworthy as a partition**, because
  the stride error was confounded with the argument error.
- Addendum 4's verified match of `1c, 1e, 1f, 20` against `ch14-recv.json` remains valid: the
  hand-walked values were read by hand one pointer at a time and did not use a stride.

## What this establishes

- `MsgChannel_RegisterTablePair` takes 7 params: two (count, table) pairs;
- both tables are arrays of defArray pointers, asserted `fieldType == MP_MSGID`;
- table A is flat (8-byte stride), table B is paired (16-byte stride);
- `maxSize <= 0x2000` verified from `MsgChannel.cpp:0x81`;
- the Addendum 4 "index 110 transition" is the end of table A, not a heterogeneous table;
- source path recovered: `D:\Perforce\Live\NAEU\v2\Code\Gw2\Services\Msg\MsgChannel.cpp`.

## What this does not establish

- **The validators' comparison boundary is not fully read.** Control flow branches on
  `maxSize < 0x10000` then `0x2000 < maxSize`, consistent with `maxSize <= 0x2000`, but the exact
  comparison was not single-stepped.
- Which table is *recv* and which is *send* is still inferred from call-site argument order in
  `FUN_14020a000`, not proven. `RegisterRecvOnly` calling `ValidateTablePairs` inverts the naive
  naming assumption, so this attribution deserves its own check.
- None of the ids are reconciled with the runtime record array or a catalog entry.
- `0x264` remains unfound, and the search that produced that result used the wrong stride for table
  A, so **the negative result is not trustworthy** and must be re-run.
- **Still no wire fixture.**

## Next steps

1. Regenerate channel `0x14` with 8-byte stride on table A, 16-byte on table B.
2. Re-run the 59-site sweep with per-table stride. Re-derive the 8 suspect sites, now suspecting
   stride rather than argument recovery.
3. Re-search `0x264` after (1) and (2). The current absence is void.
4. Determine authoritatively which table is recv and which is send.
5. Compute `maxSize` per descriptor and assert `<= 0x2000` across the corpus.


---

# Addendum 7: stride-corrected sweep, `0x264` located, recv/send proven

**Status:** the corrected 59-site sweep is complete; `0x264` is found in a recv table;
recv/send attribution is proven from the installers; **still no wire fixture**

This addendum closes the "Next steps" list of Addendum 6. All addresses below are build-local
coordinates inside build 205.780.

## 1. The stride-corrected sweep is complete

`tools/re/Sweep2.java` re-walks all 59 registration sites with the stride the validator for each
table actually uses (flat = 8 bytes, pairs = 16 bytes), keyed by registrar:

| Registrar | Table A | Table B |
| --- | --- | --- |
| `RegisterRecvOnly` (`140fed7f0`) | pairs (16) | — |
| `RegisterTablePair` (`140fed730`) | flat (8) | pairs (16) |
| `RegisterBidirectional` (`140fed670`) | flat (8) | pairs (16) |

Output: `protocol/schema/205780/sweep2.csv` (3118 rows). Corpus totals, clean subset:

| Tag | Rows | Meaning |
| --- | --- | --- |
| `OK` | 1813 | valid `MP_MSGID` descriptor, id read at `+0x10` |
| `CODE` | 1305 | pointer into `.text` — the recv table's dispatch handler |
| `NULL` | 3 | zero column in table B of two `RecvOnly` sites |
| `NOARGS` | 2 | argument setup on the stack (resolved below, not left as `NOARGS`) |

**1256 distinct ids, range `0x01`..`0x516`.** The previous negative result for `0x264` is void:
the Addendum-4/5 search used a 16-byte stride on a flat table.

## 2. `0x264` is found — and it is a recv message

Searching the corrected corpus for `0x264`:

```
TablePair 14020a031  col0 OK  264  defArray 1425cd320  (.data)
                           col1 CODE         141257a20  (.text)   <-- dispatch handler
```

Site `14020a031` is channel `0x14` (`FUN_14020a000`). In the site's output, table A occupies rows
0..219 (220 flat entries) and table B rows 220..1467 (624 pairs). Row 1246 is table B entry
`(1246 - 220) / 2 = 513`, column 0. So:

- **`0x264` lives in table B of channel `0x14`**, and table B is the recv table (section 3).
- Its defArray chain begins at `1425cd320`; its dispatch handler is `FUN_141257a20`.

Handler `FUN_141257a20` (decompiled) is a `ChCliMsg.cpp` message handler:

```
assert "player"  ChCliMsg.cpp:0x2460
player = LookupPlayerById(*(u32*)(decoded + 8))
skill  = (*(decoded + 2) != 0) ? LookupSkillById(*(u32*)(decoded + 2)) : 0
SetPlayerSkillbarEntry(player + 0x9bd8, skill, *(byte*)(decoded+6), *(byte*)(decoded+7))
```

This is the `ChCliMsg.cpp` handler family the catalog's `0x264` / `0x27C` research leads point to
(configured skill record lead). The id `0x264` in the catalog is therefore **the same id space as
the recv registry** — the "identity-mapping" open question is answered for this message: the
registry id space is the catalog message-id space, and `0x264` maps to a recv handler.

Also located in the same recv table B of channel `0x14` (all `col0 OK`, handler in the following
`col1`):

| Id | Recv entry | Handler |
| --- | --- | --- |
| `0x100` | 181 | `14124bc70` |
| `0x1A5` | 327 | `141250e70` |
| `0x200` | 413 | `141253c10` |
| `0x264` | 513 | `141257a20` |
| `0x27C` | 549 | `1412588a0` |

`0x100` additionally appears in table A (send), entry 219, which is consistent with it being a
message the client also sends (world-entry request) rather than a pure inbound one.

## 3. Recv/send attribution is proven from the installers

Addendum 6 left "which table is recv and which is send" as an open check. The two table-B
installers settle it:

| Installer | Records | Record layout | Asserted |
| --- | --- | --- | --- |
| `FUN_140fecf00` (table A) | 16-byte, base `+0x50`, count `+0x5c` | `{+0x00 flags, +0x08 defArray}` | `!target->defArray` only |
| `FUN_140fecd10` (table B) | 32-byte, base `+0x70`, count `+0x7c` | `{+0x00 flags, +0x08 defArray, +0x10 dispatchType, +0x18 handlerFn}` | `ptr->dispatch` non-null |

`FUN_140fecd10` writes `dispatchType = 1` at `+0x10` and copies the second column of each table-B
pair into the record's `+0x18` handler slot. That is exactly the `MsgRegistryRecord` layout that
`Msg_RegistryLookupById` (Addendum 2) indexes at `registry + 0x70`, count `+0x7c`, stride `0x20`,
and which `Msg_DispatchStream` dispatches on. **Table B is therefore the recv (dispatch) table,
and table A is the send (schema-only) table.**

`FUN_140fecb20` (Bidirectional's table-B installer) is the same shape but sets `dispatchType = 0`.
`RegisterRecvOnly` installs its single pairs table through `FUN_140fecd10`, so a `RecvOnly` site is
entirely recv-side — consistent with its name and with the sweep's `col1` being `.text` handlers.

## 4. The four remaining problem sites are resolved

Two sites walk past their real table into UTF-16 string data; two pass arguments on the stack.
Each is now read from its call-site instructions, not guessed:

| Site | Registrar | Resolved args | Verified ids |
| --- | --- | --- | --- |
| `14021cf4d` | `RecvOnly` | table `14224d368`, count 1, flags `0x19` | `0x10F` |
| `14021fc5d` | `RecvOnly` | table `1422e46e0`, count 5, flags `0x37` | `0x4CD..0x4D1` |
| `14021fc0a` | `TablePair` | stack: `[RSP+0x20]=2`, `[RSP+0x28]=table 1422e4568` | `0x4C3`, `0x4C4` |
| `140222bfa` | `TablePair` | stack: `[RSP+0x20]=1`, `[RSP+0x28]=table 1422f6530` | `0x505` |

Examples of the recovered argument setup:

```
14021cf45  MOV R8D,0x1          ; RecvOnly count = 1        (14021cf4d)
14021cf44  LEA R9,[0x1422e46e0] ; RecvOnly table             (14021fc5d)
14021fbf3  MOV [RSP+0x28],RAX   ; TablePair table B on stack (14021fc0a)
14021fbfe  MOV [RSP+0x20],0x2   ; TablePair count B          (14021fc0a)
```

The two `RecvOnly` sites' earlier over-walk was a **count mis-read**, not a table problem: the
sweep's register-window heuristic picked the wrong `MOV R8D` immediate. Their tables contain
exactly 1 and 5 pairs respectively, confirmed by the byte extents of the raw tables and by the
descriptor pointers inside them.

`sweep2.csv` was regenerated with these four sites' rows corrected. There are no remaining
`PTR_UNMAPPED`, `BADFT` or `NOARGS` rows.

## 5. Descriptor layout note (corrected understanding)

The sweep reads the msgId at `defArray + 0x10` and `fieldType` at `+0x00`. Confirmed against the
raw chain at `1425cd320`:

```
fieldType=1 (MP_MSGID)  id=0x264   ... chain continues at +0x28 stride
```

Chain walk for `0x264` (`1425cd320`): `1 (MSGID, id 0x264) -> 4 (MP_OPTIONAL) -> 2 -> 2 -> 4 ->
0 (terminal)`. This matches the handler's field usage (`decoded+2` skill id, `decoded+6/+7` byte
flags, `decoded+8` player id) as a type-directed decode; it does **not** by itself pin the wire
byte layout, which still requires a capture.

## 6. What this establishes

- `0x264` is present in channel `0x14` table B, entry 513, with a `.data` defArray and a `.text`
  dispatch handler, closing the Addendum-4/5 negative search.
- Table B is the recv table and table A the send table, proven from `FUN_140fecd10` /
  `FUN_140fecf00` record layouts matching `MsgRegistryRecord` / a schema-only 16-byte record.
- `0x27C`, `0x200..0x207`, `0x100`, `0x1A5` are all present in channel `0x14` recv table B, and
  `0x100` also in its send table A.
- The four previously unresolvable sites are resolved from their call-site instructions.
- `maxSize <= 0x2000` is verified as a validator assertion; the corpus-wide per-descriptor check
  is still not run (next step 5 of Addendum 6 remains open).

## 7. What this does not establish

- **Still no wire fixture.** No frame has been decoded against a capture; `wireVerified` stays
  false. The `MsgPack` field types above are build-local inferred names, not wire encoding proof.
- The runtime record array is still not read from a live instance; the static tables show what is
  registered, and the installers show how it is translated, but `count` at `registry+0x7c` is not
  read from memory.
- The `maxSize <= 0x2000` corpus-wide check (Addendum 6 next step 5) is not yet computed across
  `sweep2.csv`.
- Outbound serialization and the KSA at `140feea50` remain unrecovered.

## Next steps (revised)

1. Compute `maxSize` per descriptor chain across the `sweep2.csv` corpus and assert `<= 0x2000`;
   treat a violation as a decode bug, not a game bug.
2. Recover the KSA at `140feea50` (unchanged).
3. Take one captured frame and decode it end to end against a dumped schema for `0x264` /
   `0x27C` / `0x200`.
4. Read the runtime registry's `count` at `+0x7c` in a debugger session to confirm the static
   corpus matches the live install.


---

# Addendum 8: the maxSize bound checked across the corpus, and two schema corrections

**Status:** the `maxSize <= 0x2000` check is complete for all 1770 chains; the size table and the
`MsgPackFieldDef` offsets are corrected; **still no wire fixture**

This addendum closes Addendum 7 next step 1 (and Addendum 6 next step 5). Two Addendum-2/6
transcriptions turned out to disagree with the image and are corrected here from the bytes and
from `MsgPack_ReadFields` itself.

## 1. The bound holds across the whole corpus

`tools/re/MaxSizeWalk.py` maps `Gw2-64.exe` VA→file read-only and reimplements
`MsgPack_ComputeDefSize` (`140fe9830`) and `MsgPack_ComputeMaxSize` (`140fe98b0`) over every
`OK` `defArray` pointer in `protocol/schema/205780/sweep2.csv`. Output:
`protocol/schema/205780/maxsize.csv`.

| Quantity | Result |
| --- | --- |
| Unique chains walked | 1770 |
| Chains referencing at least one id | 1770 |
| Distinct ids covered | 1256 (matches the sweep's distinct-id count) |
| `maxSize <= 0x2000` | **1770 / 1770** |
| Largest `maxSize` | 8189 (`0x1FFD`), 3 bytes under the bound |
| Largest `defSize` | 198 |
| `defSize > maxSize` | 0 |

No violation. This was the expected result — the client's own validator asserts the bound at
startup (`MsgChannel.cpp:0x81`) — so the value of the check is that a **wrong descriptor offset
would have produced a violation**, and none did. It is a whole-corpus consistency test of the
extraction, not new protocol behaviour.

The earlier claim in Addendum 6 that the per-descriptor check "is not yet computed" is now
closed, and Addendum 7's next step 1 with it.

## 2. Correction: the size table at `142109500`

Addendum 2's 26-row size table and Addendum 6's "`maxSize` overrides" list are **not** the bytes
in the image. The actual table is **27** dwords-pairs, `{size, flags}`, indexed by
`fieldType * 8`, read byte-for-byte from `142109500`:

| `ft` | size | flags | `ft` | size | flags | `ft` | size | flags |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `0x00` | 0 | 1 | `0x09` | 16 | 1 | `0x12` | 10 | 0 |
| `0x01` | 2 | 1 | `0x0a` | 16 | 0 | `0x13` | 8 | 0 |
| `0x02` | 1 | 1 | `0x0b` | 16 | 1 | `0x14` | 9 | 0 |
| `0x03` | 2 | 1 | `0x0c` | 28 | 1 | `0x15` | 10 | 0 |
| `0x04` | 4 | 0 | `0x0d` | 8 | 0 | `0x16` | 0 | 0 |
| `0x05` | 8 | 1 | `0x0e` | 8 | 0 | `0x17` | 4 | 1 |
| `0x06` | 4 | 1 | `0x0f` | 8 | 0 | `0x19` | 4 | 1 |
| `0x07` | 8 | 1 | `0x10` | 8 | 0 | `0x1a` | 8 | 1 |
| `0x08` | 12 | 1 | `0x11` | 9 | 0 | | | |

`flags != 0` is a fixed-size scalar; `flags == 0` is composite and its `maxSize` is computed by
the switch in `MsgPack_ComputeMaxSize` (`4 -> +5`, `0xa -> +0x11`, `0xf -> inner+8`,
`0x10 -> inner*param+8`, `0x11 -> inner*param+9`, `0x12 -> inner*param+10`, `0x14 -> u16+9`,
`0x15 -> u16+10`).

**The table is self-consistent with the reader.** `MsgPack_ReadFields` copies exactly `size[ft]`
output bytes for every fixed-size type (`1/3 -> 2`, `2 -> 1`, `5/7/1a -> 8`, `6/19 -> 4`,
`8 -> 0xc`, `9/b -> 0x10`, `c -> 0x1c`, `17 -> 4`), and the pointer-shaped composites each
occupy `8` (`0xf`, `0x10`, `0x13`), `9` (`0x11`, `0x14`, `1 + 8`) or `10` (`0x12`, `0x15`,
`2 + 8`). That is a second, independent confirmation of the bytes, and it is what made the
old transcription's error visible.

Note in particular that `size[1] = 2` and `size[2] = 1`; the earlier table had `size[1] = 1`
and `size[2] = 2`, which is how a `String.format`-style argument shift would present itself.
The earlier values appear to be a flattened `{size, flags}` stream, not a size table.

## 3. Correction: `MsgPackFieldDef` offsets

The struct stride is `0x28` (confirmed in Addendum 2 and unchanged). The field offsets in
Addendum 2's table are shifted by one qword. Read from `MsgPack_ComputeMaxSize`,
`MsgPack_ComputeDefSize` and `MsgPack_ReadFields` together, the offsets are:

| Offset | Type | Field | Evidence |
| --- | --- | --- | --- |
| `0x00` | `u32` | `fieldType` | `*(uint*)(def+0)` in all three |
| `0x10` | `u32` | `param` | `(int)plVar4[-1]` in MaxSize; `param_1[4]` in DefSize/ReadFields |
| `0x18` | `pointer` | `refTypeDef` | `*plVar4` / `*(int**)(param_1+6)`; asserted non-null |
| `0x20` | `u32` | `defSize` (cache) | `*piVar3` in DefSize; `param_1[8]` is the nested read budget |
| `0x24` | `u32` | `maxSize` (cache) | `*(int*)(plVar4 + 0xc)` in MaxSize |
| `0x28` | `pointer` | `nextDef` | `piVar3[2]` / `plVar4[2]` = next `fieldType` |

The earlier table placed `param` at `+0x08`, `refTypeDef` at `+0x10`, `defSize` at `+0x18`,
`maxSize` at `+0x1C` and `nextDef` at `+0x20` — every composite field one qword low. A consumer
that used those offsets would read `refTypeDef` out of the `param` slot and compute nested sizes
from a value that is not a pointer.

## 4. Fields `4` and `0x0a` share a variable-length integer reader

`MsgPack_ReadStringOrArray` (`140fec3c0`) is not a string copier. It reads a **base-128
variable-length integer** from the wire cursor:

```text
scan 1..5 bytes for the first with the high bit clear;
decode from the terminator back to the first byte:
    value = (value << 7) | (byte & 0x7f)
write the 32-bit value to the output slot (+4 bytes);
advance the cursor past the terminator.
```

So the first wire byte is the **most significant** 7-bit group (a big-endian base-128 varint),
not the little-endian order of the protobuf varint. It fails closed: >5 bytes without a
terminator, or end-of-payload, returns 0 and sets the error flag.

`MsgPack_ReadFields` calls it for `fieldType 4` directly, and for `fieldType 0x0a` after copying
a `0xc`-byte array header. In both cases the decoded value lands in a `u32` output slot, which
is why `size[4] = 4` while `maxSize` adds `+5` (worst case is a 5-byte integer).

This is the length/count prefix the Addendum-2 enum called `MP_OPTIONAL` / `MP_ARRAY`, and it is
why those two types share a "tail" in the codec. A `fieldType 4` scalar is therefore not a fixed
4-byte field on the wire; it is a varint of 1..5 bytes that decodes into 4.

## 5. `0x264`'s chain, resolved against the handler record

The chain at `1425cd320` is `1 (MP_MSGID, id 0x264) -> 4 -> 2 -> 2 -> 4 -> 0`. Applying the
corrected reader:

| Desc | `fieldType` | Wire read | Output slot |
| --- | --- | --- | --- |
| 0 | `1` `MP_MSGID` | `u16` id `0x264` | `+0x00` |
| 1 | `4` varint | 1..5 bytes | `+0x02` (`u32`) |
| 2 | `2` | 1 byte | `+0x06` |
| 3 | `2` | 1 byte | `+0x07` |
| 4 | `4` varint | 1..5 bytes | `+0x08` (`u32`) |
| — | — | **defSize `0x0C`, maxSize `0x0E`** | |

`maxsize.csv` reports exactly `defSize 12, maxSize 14` for `1425cd320`.

This is the **first independent corroboration** of the hand-recovered packed record in
[`../UI/Widgets/remote-equipped-skills.md`](../UI/Widgets/remote-equipped-skills.md), which
records the same record as `u16` msgid, `u32` at `+0x02`, `u8` at `+0x06`, `u8` at `+0x07`,
`u32` at `+0x08`, size `0x0C`. The static schema and the handler's field usage agree on every
offset and on the total size, by two different routes.

It also **refines** that record: the two `u32` members (`+0x02` skill content id, `+0x08`
`PlayerListIndex`) are varint-encoded on the wire, not fixed 4-byte little-endian. The decoded
struct is `0x0C`; the wire frame is at most `0x0E`.

## What this establishes

- `maxSize <= 0x2000` holds for all 1770 extracted chains, with the closest at 8189;
- the binary size table at `142109500` (27 entries), cross-checked against `MsgPack_ReadFields`
  output widths, and a correction of the Addendum-2/6 transcription;
- the corrected `MsgPackFieldDef` offsets (`param +0x10`, `refTypeDef +0x18`, `defSize +0x20`,
  `maxSize +0x24`, `nextDef +0x28`);
- that `fieldType 4`/`0x0a` carry a 1..5-byte base-128 varint (MSB group first);
- the decoded layout of `0x264` (`defSize 0x0C`, `maxSize 0x0E`) and its agreement with the
  hand-recovered handler record.

## What this does not establish

- **Still no wire fixture.** The `0x264` layout above is a static, schema-directed parse; no
  captured frame has been decoded. `wireVerified` stays false and the varint is still a
  build-local encoding.
- The varint is inferred from `MsgPack_ReadStringOrArray`'s instructions, not from a sample that
  exercises it at >1 byte. A capture with a value that needs two or more groups would confirm
  the bit order.
- The runtime registry instance is still not read; the static corpus is not reconciled with the
  live install.
- The KSA at `140feea50` and the outbound path remain unrecovered.
- The `sweep2.csv` extraction itself still rests on the argument-recovery heuristic for the two
  stack-argument sites; the `maxSize` check exercises the descriptors, not the registrar
  argument decoding.

## Next steps

1. Take a captured frame and decode it end to end against the `0x264` schema; a skill id or
   player index above 0x7F would confirm the varint order from data.
2. Recover the KSA at `140feea50` (unchanged).
3. Read the runtime registry's `count` at `+0x7c` and spot-check a `defArray` pointer against
   `sweep2.csv` in a debugger session.
4. Extend `MaxSizeWalk.py` to also emit the nested `refTypeDef` tree so the per-field wire widths
   are machine-readable for codec work.


---

# Addendum 9: the transport key schedule reconstructed, and dispatch kind 0 closed

**Status:** the key schedule is reconstructed from the instruction stream and a synthetic
reference vector is emitted; the PRGA is confirmed; dispatch kind 0 is identified;
**still no wire fixture**

This addendum closes Addendum 8 next steps 1 and the original note's next step 3. All addresses
are build-local coordinates inside build 205.780.

## 1. Correction: `MsgUtil_Rc4Ksa` is the key schedule only

The original note described `MsgUtil_Rc4Ksa` (`140feea50`) as doing an identity permutation, a
"standard RC4 swap loop ... using a 20-byte key", and "a 256-byte PRGA". The third phase is not
a PRGA. The function:

1. builds 20 bytes of key material from the 20-byte connection key;
2. mixes those 20 bytes; and
3. runs **one** standard RC4 KSA over the identity permutation.

The keystream is produced by `MsgUtil_CryptStream` (`140fee7c0`), which is a separate function
and is unchanged from the Addendum description. The 256-iteration loop at `140feebf0` that the
note called a PRGA is the KSA.

## 2. Step 1: key material

`FUN_1409b9dc0(RSP+0x20, 0x14)` zeroes a 20-byte buffer. Up to 20 key bytes are then XORed in
(a qword loop at `140feead0` and a byte tail at `140feeb10`). A key longer than `0x14` trips the
assert at `MsgUtil.cpp:0x1ce` and is clamped. The buffer is five little-endian `u32` words
`w0..w4`.

## 3. Step 2: the five-word mixing (`140feeb20`..`140feebd6`)

The mixing is **not** standard MD5. It is four dependent steps over five words, with `ROL` of 5
and 30 bits and bespoke constants. Read directly from the instructions, with `ROL(x,n)` a
32-bit rotate-left:

```text
A0 = w0 + 0x9fb498b3
A1 = w1 + 0x66b0cd0d + ROL(A0, 5)
B0 = ROL(A0, 30)
B1 = ROL(A1, 30)
A2 = ROL(A1, 5) + w2 + (~(A0 & 0x22222222) & 0x7bf36ae2) + 0xf33d5697
B2 = ROL(A2, 30)
A3 = ROL(A2, 5) + w3 + (((B0 ^ 0x59d148c0) & A1) ^ 0x59d148c0) + 0xd675e47b

K'0 = w0 + w4 + ROL(A3, 5) + 0xb453c259 + (((B0 ^ B1) & A2) ^ B0)
K'1 = w1 + A3
K'2 = w2 + B2
K'3 = w3 + B1
K'4 = w4 + B0
```

Note the fourth boolean term uses **`A2`, not `B2`**: the `AND EAX,R9D` at `140feebb0` executes
before `ROL R9D,0x1e` at `140feebb3`. A reconstruction that rotates first will disagree. The
first constant is the `LEA ESI,[R11-0x604b674d]` immediate (`-0x604b674d == 0x9fb498b3 mod 2^32`).

The constants do not match the MD5, SHA-1 or SHA-2 round tables, so the earlier "MD5-family"
label is dropped. The structure is four steps over five words, not MD5's four 16-step rounds
over a 16-word block.

## 4. Step 3: RC4 KSA

`K'` (20 bytes) is the RC4 key. The routine writes the identity permutation (`S[i] = i`) at
`state+8` and then runs

```text
j = 0
for i in 0..255:
    j = (j + S[i] + K'[i mod 20]) & 0xff
    swap(S[i], S[j])
```

The key index wraps at 20 (the magic multiply at `140feec45`..`140feec5c` is an unsigned modulo
by 20). `state+0` (i) and `state+4` (j) are zeroed at `140feeb3f`.

## 5. `MsgUtil_CryptStream` state layout

Confirmed from the instructions: `state[0] = i`, `state[1] = j`, `S[256]` at `state+8`. The
routine is a textbook RC4 PRGA applied in place to the caller's buffer, and `i`/`j`/`S` persist
across calls, so a connection's keystream cannot be fingerprinted from one frame.

## 6. The reference implementation and its vector

`tools/re/Gw2TransportCipher.py` implements the schedule and PRGA and emits
`tools/re/fixtures/205780/transport-cipher.json`. The self-test checks four things:

| Check | What it catches |
| --- | --- |
| expression form vs mutation form of the mixing | an algebra slip such as using `B2` where the asm uses `A2` |
| KSA/PRGA vs an independent textbook RC4 over the derived key | a mistake in the RC4 half |
| key-length edge cases (0, 1, 7, 8, 19, 20, 25) | the clamp and short-key path |
| two half-streams vs one whole stream | losing carried cipher state across calls |

The fixture is **synthetic**. It is produced by the reconstruction, so it can catch a
transcription error in a *reimplementation*; it cannot confirm the client's output. There is
still no captured frame, so `wireVerified` remains false for every message.

## 7. Dispatch kind 0 is `Msg::Raw::RecvInvalid`

Addendum 2 left dispatch-table entry 0 as "not yet defined as a function". The body at
`140fec590` fills the 0x28 result record with:

| Offset | Value |
| --- | --- |
| `+0x00` | `142109a10` -> `"RecvInvalid"` |
| `+0x08` | `142109a20` -> `"Msg::Raw::RecvInvalid"` |
| `+0x10` | `1421095d8` -> `...\Services\Msg\MsgConn.cpp` |
| `+0x18` | `0xc61` (assert line) |
| `+0x1c`, `+0x20` | copies of `conn+0x40`, `conn+0x44` |
| `+0x24` | `0` (failure status) |

So frame kind 0 is an explicit invalid-frame error path, not a data frame. With kinds 1 and 2
already defined (`ClientRecvEncrypt`, `ClientRecvError`), the three-kind table is entirely
handshake/control, which is consistent with Addendum 1's finding that mode-3 traffic bypasses it.

## What this establishes

- the exact transport key schedule: 20-byte XOR buffer, four-step five-word mixing, RC4 KSA;
- that the earlier "MD5" and "PRGA phase" descriptions of `140feea50` were wrong;
- the PRGA state layout and carried-state behaviour;
- a runnable reference with four self-tests and a synthetic vector;
- that dispatch kind 0 is `Msg::Raw::RecvInvalid`.

## What this does not establish

- **Still no wire fixture.** The reference vector is synthetic. The key schedule and PRGA are
  static reconstructions, not observed against captured bytes.
- How the 20-byte connection key at `conn+0x118` is obtained from the handshake. That is the
  session-state prerequisite and remains unrecovered.
- Whether inbound and outbound use the same schedule and key, or independent RC4 states.
- Any association between a frame and a catalog message id end to end.

## Next steps

1. Recover how `conn+0x118` is populated in `MsgRaw_ClientRecvEncrypt` (the handshake key
   derivation), which is the remaining blocker before a captured frame can be decrypted.
2. When a capture is available, decode a frame with this reference and compare the `0x264`
   payload against the schema in Addendum 8.
3. Recover the KSA call site for the send side and confirm whether it reuses the same key state.


---

# Addendum 10: the game-connection key exchange, and where it stops

**Status:** the constructor that writes `conn+0x118` and the XOR key agreement are located and
read; the derivation between them is a big-integer/PRNG construction whose exact form is **not**
reconstructed; **still no wire fixture**

Addendum 9 next step 1 is partially closed: the field `conn+0x118` is **not** the handshake
seed. It is overwritten with a derived value before any encrypted frame is read.

## New owners

| Identity (durable) | Ghidra address (build-local) | Source unit / note |
| --- | --- | --- |
| `MsgConn::SetMode` | `140fea330` | `MsgConn.cpp`; writes `conn+0x108` |
| `MsgConn::SetDispatchCallback` | `140fea3a0` | `MsgConn.cpp` |
| game-net event handler | `14023ed80` | `GcGameCmd.cpp`; creates/tears down the game conn |
| `MsgConn` constructor | `140fe9b00` | `MsgConn.cpp`; seeds and derives `conn+0x118` |
| handshake KDF | `140fede80` | key-derivation helper, `MsgConn.cpp` |
| `MsgRaw::ClientRecvEncrypt` | `140fe87f0` | XOR agreement + KSA install |
| `MsgRaw::ClientRecvError` | `140fe8a90` | mode-2/kind-2 path |

`MsgConn::SetMode(conn, x)` sets `conn+0x108 = 3` when `x == 0` and `= 1` otherwise. It asserts
`msgConn->protocol == NET_PROTOCOL_CLI2GAME` and `msgConn != NULL`.

## Connection creation

`FUN_14023ed80` (the game-net event handler) creates the game connection on event `1`:

```c
piVar3   = FUN_140fedfb0(local_1b8);      // local_1b8[0] = 0x88, piVar3 = &DAT_142109e40
DAT_1426632d0 = FUN_140fe9b00(param_2, 0, FUN_14023ec30,
                              local_1b8[0], piVar3, 0x14, param_4 + 0x34);
```

So the constructor is called with **key length `0x14`** and a **seed source at `param_4+0x34`**.
`FUN_140fedfb0` is two instructions: it writes `0x88` through its argument and returns
`&DAT_142109e40` (the KDF descriptor). Event `4` forwards packets to
`Net_OnClientPacketReceived`; event `3` tears the connection down.

## The constructor: seed, derive, overwrite, send

`FUN_140fe9b00` calls `FUN_140fe8b70(param_1, param_2, 2)` to allocate the connection.
`FUN_140fe8b70` stores that third argument at `conn+0x108`, so the game connection is
**created in mode `2`** (`MSGCONN_MODE_ENCRYPTED`), consistent with `ClientRecvEncrypt`
requiring mode `2`. `FUN_140fe9b00` then, read from the code:

1. Stores its callback argument (`FUN_14023ec30`, whose error label is
   `Gc::GameSrvEncryptCallback`) at `conn+0x110`.
2. If the seed length is `0`, `FUN_140fdf2d0(0x14, conn+0x118)` fills the 20 key bytes from a
   time/entropy-mixed generator. Otherwise the seed dwords are copied from `param_7`
   (`param_4+0x34`), clamped to 20 and asserted dword-aligned.
3. `FUN_140fede80(0x88, &DAT_142109e40, 0x14, conn+0x118, outA, outB)` derives two values from
   the 20 seed bytes.
4. `conn+0x118` is **zeroed** (`FUN_1409b9dc0(...,0x14)`) and then overwritten with the first
   `min(len, 0x14)` bytes of `outA`.
5. `outB` is sent to the server through `FUN_140fe3e60`, prefixed with a two-byte header
   `{0x00, len}` where `len` is `outB`'s byte length (`<= 0x40`, asserted). `FUN_140fe3e60` is a
   thin virtual send: `(*(*(conn+0x30) + 0x38))(*(conn+0x30), len, bytes)`.

So the value the server eventually returns is XORed against a **locally derived** 20-byte key,
not against a seed the client sent in the clear.

## The KDF descriptor and helper

`DAT_142109e40` is `0x88` bytes: `{u32 1, u32 4, blobA[0x40], blobB[0x40]}`.

| Field | Value |
| --- | --- |
| `+0x00` | `1` (checked by `FUN_140fede80`, else it does nothing) |
| `+0x04` | `4` (fed to a helper as a small integer) |
| `+0x08` | `blobA` = `f9e43af5..598573f9` (64 bytes) |
| `+0x48` | `blobB` = `270e7b58..c358a6c4` (64 bytes) |

`FUN_140fede80` loads `blobA`, `blobB`, the integer `4` and the 20 seed bytes into four
big-integer contexts, generates `0x40` bytes from a PRNG seeded by the seed context, and combines
them into two outputs (`outA`, `outB`). The PRNG helper `FUN_141570640` is a **Park-Miller
minimal-standard LCG**: multiplier `48271` (`0xbc8f`), modulus `2^31-1`, with the Schrage
reduction `x/44488 + x*48271` (the `0xadc8` constant). The contexts are manipulated by an
anonymous big-integer library (`FUN_14156ee00` loads bytes into limbs and trims trailing zero
limbs; `FUN_14156fdc0` combines contexts). `CptSha.cpp` SHA-1 (`FUN_1415766e0`) is present in
the image but is **not** the routine this path calls.

The exact `FUN_140fede80`/`FUN_14156fdc0` construction (which combination, and whether it is a
modular/DH-style exchange or a keyed derivation) is not reconstructed.

## The XOR agreement and the receive cipher state

`MsgRaw_ClientRecvEncrypt` in mode `2`, for a frame whose kind byte is `1` and whose length byte
is `0x16`:

```text
key[0..19] = frame[2..0x15] XOR conn[0x118..0x12b]     // 20 bytes
conn+0x108 = 3
MsgUtil_Rc4Ksa(conn+0x12c, 0x14, key)                  // inbound cipher state
copy 0x108 bytes conn+0x12c -> conn+0x234              // a second state snapshot
(*(conn+0x110))(record, param_3, {0x300000, 0xc451b58})
```

`MsgConn::Dispatch` then decrypts mode-3 traffic with
`MsgUtil_CryptStream(conn+0x12c, len, src, dst)`, so **the inbound cipher state is at
`conn+0x12c`** (i at `+0x12c`, j at `+0x130`, `S[256]` at `+0x134`). The 20 bytes at
`conn+0x118` are the derived key material; they are not themselves the cipher state.

`MsgRaw::ClientRecvError` (kind `2`, mode `2`, length byte `> 9`) also calls the `conn+0x110`
callback, with `frame+2` as the payload.

## What this establishes

- `conn+0x118` is written by the `MsgConn` constructor `FUN_140fe9b00`, then overwritten by the
  first 20 bytes of a derived value; it is not the handshake seed.
- the seed is either `param_4+0x34` (a 20-byte field of the game-net event) or a time/entropy
  generator when the seed length is zero;
- the KDF descriptor `{1, 4, blobA[64], blobB[64]}` at `142109e40` and its Park-Miller
  (`48271`/`2^31-1`) PRNG helper;
- the client sends a second derived value (`outB`, `<= 0x40` bytes) with a `{0x00, len}` header;
- the game connection is created in mode `2`, and `conn+0x110` is the constructor's callback
  (`FUN_14023ec30`, error label `Gc::GameSrvEncryptCallback`);
- the mode-2/kind-1 handshake frame carries the server's 20-byte value, which is XORed with the
  stored 20 bytes to form the RC4 key;
- the inbound cipher state lives at `conn+0x12c`, and a second `0x108`-byte snapshot is copied to
  `conn+0x234`.

## What this does not establish

- **The derivation itself.** `FUN_140fede80`'s exact construction and the big-integer/PRNG
  library semantics are not recovered, so the 20 bytes at `conn+0x118` cannot yet be reproduced
  from a seed.
- **The seed's origin.** `param_4+0x34` comes from the game-net event struct; what fills it
  (a login/platform token, a previous handshake, or entropy) is not traced.
- the meaning of the `{0x300000, 0xc451b58}` argument passed to the `conn+0x110` callback;
- whether `conn+0x234` is the outbound cipher state or a retransmit snapshot, and whether the
  send path installs a separate KSA;
- **Still no wire fixture.** Nothing here is validated against captured bytes.

## Next steps

1. Recover `FUN_140fede80` and the `FUN_14156fdc0` combination into a reference implementation,
   then add a keyed test vector alongside `Gw2TransportCipher.py`.
2. Trace where `param_4+0x34` is filled, back through the event-1 producer, to identify the seed.
3. Decode the `{0x300000, 0xc451b58}` argument and `FUN_14023ec30`'s full behaviour.
4. Determine whether `conn+0x234` is used by the send path, which would fix the outbound cipher
   entry point.


---

# Addendum 11: the seed comes from the game-server connect event

**Status:** the chain from the connect request to the `MsgConn` seed is traced; the seed's exact
writer is **not** located, but the code shows it is provisioned, not generated; **still no wire
fixture**

Addendum 10 next step 2 is partly closed. This addendum follows `param_4+0x34` back to the event
that carries it and answers the "client-generated or server-issued" question as far as static
evidence supports.

## The chain

| Step | Address | What it does |
| --- | --- | --- |
| register event `0x23` | `14023f930` | `FUN_141064b00(0x23, FUN_14023f9b0, 1)` (EvtApi) |
| event `0x23` handler | `14023f9b0` | switches on the payload's first int; case `2` builds the game conn |
| build connection | `14023fca0` | allocates the `MsgConn` and calls the event-1 handler |
| event-1 handler | `14023ed80` | `FUN_140fe9b00(..., 0x14, request+0x34)` |
| fire event `0x23` | `14023dc00` | `*(obj+0x38) = *(obj+0x24); FUN_141064a30(ctx, 0, 0x23, obj+0x38)` |

The event payload is the GcSrv state object at `+0x38`. The handler's case `2` passes
`payload+4` (int pointer arithmetic, `= obj+0x48`) as the connect request, and the seed is that
request's `+0x34`, i.e. **`obj+0x7c`**.

## The request struct

From `FUN_14023fca0` and the constructor call:

| Request offset | Use |
| --- | --- |
| `+0x00`, `+0x08` | two pointers passed through to the `MsgConn` (a container or buffer pair; not inspected) |
| `+0x2c` | mode selector (`0` -> `FUN_14023ed80`, `3` -> `FUN_140240cf0`) |
| `+0x30` | a `u32` copied into the `MsgConn` |
| `+0x34` | the 20-byte seed passed to `FUN_140fe9b00` |

This is the shape of a game-server redirect (a container pair, a mode, a `u32`, and a 20-byte
token), though the container contents were not inspected.

## Is the seed client-generated?

The `MsgConn` constructor `FUN_140fe9b00` uses its entropy generator **only when the seed length
is zero**. The game-connect path always passes `0x14` and `request+0x34`, so the seed is never
generated there. It is a value that already exists in the GcSrv state object before the game
connection is opened.

The producer path (`FUN_14023dc00`) is one of a family of notification methods on the same GcSrv
object, each copying `obj+0x24` to `obj+0x38` and firing a different event id (`0x20`, `0x23`,
`0x25`, `0x68`, `0x69`, ...). They notify event consumers of state the object already holds; they
do not originate cryptographic material.

**Conclusion (inference, not proof of the writer):** the seed is provisioned state, not fresh
client entropy. It is established before the game connection exists, which places its origin in
the preceding login/platform exchange — i.e. it is issued to the client, not invented by the
game connection. The exact instruction that writes `obj+0x7c` was not located, so "server-issued"
is the supported reading rather than a proven fact.

## Consequence for offline decoding

This is the decision-relevant result. Reconstructing `FUN_140fede80` alone would **not** make a
captured frame decryptable, because the seed is an external input. To decode a real frame
offline, a capture must also supply the session state — either

- the 20-byte derived key material read from `conn+0x118`, or
- the 20-byte seed (`obj+0x7c`), and only then a reconstructed KDF.

A capture of transport bytes alone is insufficient regardless of the KDF work. This should be
recorded when the private capture is imported.

## What this establishes

- event `0x23` is the game-server connect event, registered in `GcSrv.cpp` (`14023f930`) to
  handler `14023f9b0`, and fired by the GcSrv notification method `14023dc00`;
- the seed is `request+0x34` where `request = payload+0x10`, `payload = obj+0x38`, so it lives at
  `obj+0x7c` in the GcSrv state object;
- the request's shape (address list, mode, `u32`, 20-byte token) is consistent with a
  game-server redirect;
- the seed is supplied state, not generated by the connection, so it is not reproducible from
  the client alone.

## What this does not establish

- **The writer of `obj+0x7c`.** The login/platform message or function that fills the token was
  not located, so the origin is an inference.
- Whether the seed is a long-term account token or a per-session game-link value.
- Anything about the KDF itself (Addendum 10), the `conn+0x110` argument, or the send-side
  cipher state.
- **Still no wire fixture.**

## Next steps

1. Locate the writer of `obj+0x7c` by finding the GcSrv state constructor or the login message
   that carries the redirect, which would make the origin a proven fact.
2. Reconstruct `FUN_140fede80` (as before), but treat it as incomplete until a seed source is
   identified or captured.
3. When importing the private capture, include the runtime `conn+0x118` (or `obj+0x7c`) so the
   transport layer can be exercised at all.


---

# Addendum 12: the writer search stops at the GcSrv/login message layer

**Status:** the writer of `obj+0x7c` was **not** isolated; the search did locate the login/GcSrv
message registry and rule out the login handlers; **still no wire fixture**

This addendum is the Addendum 11 next step 1 result. It is a bounded negative, recorded so the
work is not repeated.

## What was searched

The connect request that carries the seed is `payload+0x10` where `payload` is the event-0x23
payload `obj+0x38` (Addendum 11). Reading the offsets: the seed at `request+0x34` is `obj+0x7c`.
The writers that could fill it are the handlers that populate `obj` before the event fires.

`obj` is an entry in a request list rooted at `DAT_1426630c0` / `DAT_1426630b0`. The login
handlers find it by `*(int*)(entry+0x30) == id` and act only when `*(int*)(entry+0x20) == 3`.
Fields written by the handlers that were read:

| Handler | Sink | Writes |
| --- | --- | --- |
| `140240250` | `FUN_14023a8d0` | `entry+0x24` |
| `1402402a0` | `FUN_14023a4e0` | `entry+0x1f0`, `entry+0x1f4`, `entry+0x24` |
| `1402402f0` | `FUN_14023b0c0` | `entry+0x20`, `entry+0x24` |
| `140240350` | `FUN_14023a270` (type `7`) | `entry+0x1a4`..`entry+0x244` (two `0x20`-byte blobs at `+0x200`, `+0x220`) |
| `140240350` | `FUN_14023a270` (type `0x15`) | `entry+0x68`, `entry+0x70` |
| `1402404c0` | `FUN_14023a3f0` | `DAT_142663100`/`108` (a 16-byte-record list), fires `0x1d` |
| `140240490`, `140240550`, `140240600` | `FUN_14023a140`, `FUN_14023a670`, `FUN_14023a9f0` | not `+0x7c` |

**None writes `entry+0x7c`.** So the seed is not set by the login message handlers examined. The
writers were not exhausted (the registry has more entries than were read), but the shape is
consistent with the seed being placed by the connect-initiating (game/UI) code rather than by a
login response.

## What the search did find: a login/GcSrv `{blob, handler}` registry

A `{blob, handler}` pair table sits at `141921fc0` (and continues past it). The blobs are
formatted **comparably** to `MsgPackFieldDef` chains: at `1425a7a50` the first dwords are `1`
then, at `+0x28`, `4`, then `4`, then `0xc` — the same `0x28`-stride, `fieldType`-first format as
the game corpus, and the shared value `0x141922a88` appears at `+0x08` of both the `0x264` chain
(`1425cd320`) and these chains.

Example pairs:

| Blob | Handler |
| --- | --- |
| `1425a7a50` | `140240250` |
| `1425a82e0` | `1402404c0` (a count + 16-byte records, fires `0x1d`) |
| `1425a8240` | `140240490` |
| `1425a8850` | `140240550` |
| `1425a85c0` | `140240600` |

**Caveat:** the handlers read *packed* byte offsets (`param_2 + 2`, `+ 6`, `+ 10`, pointer at
`+ 3`), which a schema-decoded struct would not produce. So whether these blobs are decoded
schemas, or the handlers parse a raw packed frame against some other table, is a **lead to
verify**, not established. If they are schemas, the login/platform protocol is also schema-driven
and its id space can be walked like `sweep2.csv`; it is not the game connection's registry
(`DAT_1426632d0` / `140fed3b0`).

## What this establishes

- `obj` is an entry in a request list at `DAT_1426630c0`/`DAT_1426630b0` with state at `+0x20`,
  subtype at `+0x24`, id at `+0x30`, response data at `+0x1a4`..`+0x244`;
- the login/GcSrv handlers read do not write `+0x7c`, so the seed is not one of their fields;
- the login/GcSrv connection has its own `{blob, handler}` registry at `141921fc0`, distinct from
  the game message registry (the blob format resembles `MsgPackFieldDef`, to be verified).

## What this does not establish

- **The writer of `obj+0x7c` remains unlocated.** The negative above is bounded by the handlers
  that were read, not by exhausting the registry.
- Whether the `141921fc0` blobs are decoded schemas, and the login registry's ids, size and
  handler set (not enumerated).
- Whether the seed is a login-session key, an account token, or a game/UI-provided value.
- **Still no wire fixture.**

## Next steps

1. Establish whether the `141921fc0` blobs are schemas (walk one as a `MsgPackFieldDef` chain and
   check `fieldType 1` / the id read against the handler's fields). If so, enumerate the login
   registry as a separate corpus.
2. Search specifically for a writer of `+0x7c` across the *game/UI* modules rather than the login
   handlers, or for a copy of the login `MsgConn`'s `conn+0x118` into the connect request.
3. Keep the capture requirement from Addendum 11: include `conn+0x118` (or `obj+0x7c`).

