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
