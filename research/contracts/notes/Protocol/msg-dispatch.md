# Message dispatch and transport framing

**Build:** `205.780` (Gw2-64.exe, image SHA-256
`D2AE84876A0B93277FCCB368969046B848BB0403FD09DB420389C813D2459B23`). Addresses, field-type
numbers and sizes are build-local coordinates; do not carry them to another build.

**Status:** the inbound path and the connection handshake are recovered and validated against
private captures; outbound encoding is located and validated on one captured packet. This is not
live-server compatibility.

This note is an **index**. The detail lives in the topic notes below; the chronological evidence
(the original note and Addendum 1-29, verbatim, including superseded claims and corrections) is in
[msg-dispatch-addenda.md](msg-dispatch-addenda.md). Other notes and code cite that file as
`msg-dispatch-addenda Addendum N`.

## The pipeline

Inbound (game connection, mode 3):

```text
Net_OnClientPacketReceived
  -> MsgConn::Dispatch                     framing, mode at conn+0x108
       -> MsgUtil_CryptStream              RC4 PRGA, state conn+0x12C
       -> frame container                  conn+0x58: [u16 compLen][u16 decodedLen][payload]
            -> deframe / LZ4               raw when compLen == 0
            -> message stream              conn+0x80: [u16 msgId][fields]...
                 -> Msg::DispatchStream
                      -> MsgPack::ReadFields   schema from the live registry (conn+0x18)
```

Outbound:

```text
schema encode (Msg::WriteMsg -> FUN_140fea110)   send registry (conn+0x18, +0x50/+0x5c)
  -> plaintext stream                            conn+0x398, cursor conn+0xd0
  -> flush FUN_140fe96f0
       -> MsgUtil_CryptStream                    RC4 PRGA, state conn+0x234
       -> transport                              *(conn+8), no compLen/LZ4
```

## Topic notes

| Note | Contents |
| --- | --- |
| [transport-cipher.md](transport-cipher.md) | RC4-variant key schedule + PRGA, state layout, validation, cross-direction reuse |
| [handshake-key-derivation.md](handshake-key-derivation.md) | Diffie-Hellman KDF, the client seed, why the wire alone is not enough |
| [inbound-framing.md](inbound-framing.md) | connection modes, receive pipeline, frame container + LZ4, handshake frame |
| [schema-registry.md](schema-registry.md) | channels, registry records, `MsgPack` schema format, direction-specific corpora |
| [outbound-messages.md](outbound-messages.md) | send encoder, send registry, buffer, flush |
| [live-capture-evidence.md](live-capture-evidence.md) | captures, fixtures, validation status, operational cautions |
| [handler-to-subsystem.md](handler-to-subsystem.md) | control flow from a decoded message through its handler into the native subsystems |

## Evidence levels

- **Capture-validated, offline:** transport cipher; inbound frame container + LZ4; the `0x264`
  inbound wire fixture; the `0x120` outbound wire fixture; the handshake KDF; cross-direction
  keystream reuse.
- **Static:** schema-format offsets, registry layouts, the schema corpora, the outbound flush
  internals.
- **Open:** outbound sequence/acknowledgement rules; the server side of the handshake; Unity
  EditMode tests (the package is compiled-validated only).

## Runtime

The engine-independent `Gw2.Protocol` package implements this pipeline for build 205.780
(`TransportCipher`, `TransportFrame`/`Lz4Block`, `MsgPackReader`/`MsgPackWriter`,
`MessageStreamDecoder`, `ProtocolCodec`, `OutboundProtocolCodec`) with a direction-aware
`ProtocolSchemaCorpus`. See [msg-dispatch-addenda Addendum 29](msg-dispatch-addenda.md).
