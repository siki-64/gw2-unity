# Outbound messages

**Build:** `205.780` (Gw2-64.exe, image SHA-256
`D2AE84876A0B93277FCCB368969046B848BB0403FD09DB420389C813D2459B23`).

**Status:** the encoder, send registry, buffer and flush are located; the cipher is confirmed on one
captured packet. Evidence trail: Addenda 24, 26, 27, 29; see
[msg-dispatch-addenda.md](msg-dispatch-addenda.md).

## The send encoder `FUN_140fea110(conn, rawDataBytes, rawData)`

```c
assert(conn && rawData && rawDataBytes >= 2);
if (conn+0x108 == 3 && (*(byte*)conn & 4)) {
    msgid   = *(u16*)rawData;
    sendMsg = FUN_140fed3d0(conn+0x18, msgid);          // send registry lookup
    assert(sendMsg && sendMsg->defArray[0].defSize != 0);
    assert(rawDataBytes == sendMsg->defArray[0].defSize);
    MsgPack_WriteFields(out, &ctx, sendMsg->defArray, rawData, rawData+rawDataBytes, ...);
    // send stats + observer
}
```

`rawData` is a **decoded native record** whose size must equal the send schema's
`defArray[0].defSize`. It is called by hundreds of generated per-message senders, so it is the single
outbound encode entry.

## The send registry `FUN_140fed3d0(registry, id)`

```c
if (*(u32*)(registry + 0x5c) <= id) return 0;
record = *(qword*)(registry + 0x50) + id * 0x10;        // stride 0x10
return *(qword*)(record + 8) ? record : 0;              // defArray at +0x08
```

This is **table A** (see [schema-registry.md](schema-registry.md)). So outbound is schema-driven on
the **send** corpus (`chains-send.json`), which has different chains from recv for the same id.

## Buffer, cursor, flush

| Offset | Field |
| --- | --- |
| `conn+0x398` | outbound plaintext buffer (message stream) |
| `conn+0xd0` | write cursor (reset to `conn+0x398` after each flush) |
| `conn+0x234` | outbound RC4 state (`0x108` bytes) |
| `*(conn+8)` | transport object; `FUN_140fe3e60(obj, data, len)` calls `(*(obj+0x30)+0x38)(...)` |

`FUN_140fe96f0(conn)` (flush):

```c
FUN_140fee540(conn+0xd8, now);                 // ping/time accounting, not framing
if (conn+0x108 == 1) { conn+0xd0 = conn+0x398; return; }
len = conn+0xd0 - (conn+0x398);
MsgUtil_CryptStream(conn+0x234, len, conn+0x398, temp);
FUN_140fe3e60(*(conn+8), temp, len);           // transport send
conn+0xd0 = conn+0x398;
```

## Asymmetry with inbound

- **No compression container.** The flush encrypts the raw `[u16 msgid][fields]` stream and hands
  `(len, temp)` to the transport; there is no `[compLen][decodedLen]` and no LZ4 on the outbound side
  (contrast [inbound-framing.md](inbound-framing.md)).
- **Separate cipher state** (`conn+0x234`), which is the handshake copy of the inbound state
  (two-time pad; see [transport-cipher.md](transport-cipher.md)).
- `MsgPack_WriteFields` does not bounds-check; sizes must come from `ComputeMaxSize`.

## Live capture (Addendum 26)

Flush `FUN_140fe96f0` sent a 6-byte packet:

```text
plaintext (conn+0x398, len 6)   20 01 88 8F 01 01       msgid 0x120, varint 0x4788, u8 1
pre-flush state (conn+0x234)    i = 0x71, j = 0x2f
ciphertext (flush stack temp)   5A 31 60 FB 37 DB       == Crypt(state, plaintext)
```

`0x120` decodes with the **send** chain `[MP_MSGID,varint,u8]`, not the recv chain `[1,4,4,2,4]`.
Fixture `tools/re/fixtures/205780/outbound-0x120.json`; runtime `OutboundProtocolCodec`.

## Open

- Send sequence/acknowledgement rules.
- Whether the full send corpus exceeds the 481 statically-recovered ids.
- Whether the transport (`vtable +0x38`) adds an outer length prefix.
