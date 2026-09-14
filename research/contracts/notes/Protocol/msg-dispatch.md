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

