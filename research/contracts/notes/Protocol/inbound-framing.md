# Inbound framing

**Build:** `205.780` (Gw2-64.exe, image SHA-256
`D2AE84876A0B93277FCCB368969046B848BB0403FD09DB420389C813D2459B23`).

**Status:** recovered and validated against captured wire. Evidence trail: the original note and
Addenda 1, 19, 20, 22, 25; see [msg-dispatch-addenda.md](msg-dispatch-addenda.md).

## Connection modes (`conn+0x108`)

| Value | Name | Meaning |
| --- | --- | --- |
| `1` | `MSGCONN_MODE_CLIENT_START` | handshake start; frames are buffered, not dispatched |
| `2` | (initial) | after the constructor sends the client DH public value; awaits the server key frame |
| `3` | `MSGCONN_MODE_ENCRYPTED` | keyed/established; the encrypted receive pipeline runs |

See [session-state.md](session-state.md) for the writers, transitions and corrections (the earlier
`2 = MSGCONN_MODE_ENCRYPTED` labelling was wrong; `MSGCONN_MODE_ENCRYPTED` is `3`).

## Receive pipeline

```text
Net_OnClientPacketReceived              network callback; DAT_1426632d0 = game conn
  -> MsgConn::Dispatch                  framing, mode word at conn+0x108
       mode 3: MsgUtil_CryptStream      decrypt in place, state conn+0x12C
               -> append to conn+0x58    decrypted transport (frame container)
               -> FUN_140fe8ef0          deframe each frame
                    raw when compLen==0, else FUN_141574a00 (LZ4)
                    -> append decoded payload to conn+0x80 (message stream)
                    -> Msg::DispatchStream
       modes 1/2: the three-kind table below (handshake/control only)
```

The three-entry kind table is indexed by the first frame byte and is **handshake/control only**;
mode-3 gameplay traffic never reaches it (kind 0 `Msg::Raw::RecvInvalid`, kind 1
`ClientRecvEncrypt`, kind 2 `ClientRecvError`).

## Frame container (`FUN_140fe8ef0`)

After the transport cipher is removed, the buffer is a concatenation of frames:

```text
0x00  u16 compLen     0 = raw; otherwise the LZ4 block length
0x02  u16 decodedLen  the decoded payload length
0x04  payload         compLen bytes of LZ4, or decodedLen raw bytes
```

Frame size = `(compLen != 0 ? compLen : decodedLen) + 4`. The decoded payloads are appended to
`conn+0x80`; the read cursor advances by `frameSize + 4`. Frames may span packets (a partial trailing
frame is retained), so a consumer must buffer.

Connection buffers (build-local layout):

| Offset | Field |
| --- | --- |
| `conn+0x58` | decrypted transport frame container: `{...; data ptr +0x08; capacity +0x10; length +0x14}` |
| `conn+0x80` | message stream (post-deframe); same struct shape |

## The message stream

Mode 3 is a concatenation of messages with **no per-message length**:

```text
[u16 msgId][schema fields] [u16 msgId][schema fields] ...
```

`Msg::DispatchStream`:

1. read a `u32`-sized id slot (`MsgUtil_ReadBytes`; the low `u16` is the id);
2. resolve the record via the live registry (see [schema-registry.md](schema-registry.md));
3. assert `defArray[0].defSize != 0`, allocate and decode with `MsgPack::ReadFields`;
4. call the handler `(ctx, decoded)` (`dispatchType` 0 or 1 selects the call shape).

## Handshake frame

`MsgRaw_ClientRecvEncrypt` handles dispatch kind `1` (frame `[u8 0x01][u8 0x16][20 bytes]`; the
precondition is the **length** byte `0x16`, not the kind). It derives the RC4 key
(`frame[2..0x15] XOR conn+0x118`), sets mode `3`, runs the KSA, and copies the state to the outbound
slot. See [handshake-key-derivation.md](handshake-key-derivation.md) and
[session-state.md](session-state.md).

## Validation

- `tools/re/fixtures/205780/0x264-wire.json`: a captured ciphertext frame deframes/decodes to two
  `0x264` messages.
- `tools/re/Gw2TransportDecode.py` implements deframe + LZ4, with truncation and bad-offset checks.

## Open

- Whether the frame container is identical across all channels/connections.
