# Session state machine

**Build:** `205.780` (Gw2-64.exe, image SHA-256
`D2AE84876A0B93277FCCB368969046B848BB0403FD09DB420389C813D2459B23`). Addresses and offsets are
build-local coordinates; do not carry them to another build.

**Status:** statically recovered (instructions verified). This note also **corrects two earlier
claims** (see "Corrections"). Evidence trail: the original note and Addenda 1, 9, 25, 26; see
[msg-dispatch-addenda.md](msg-dispatch-addenda.md).

This note covers **how a game connection moves between phases and how it sends**, i.e. the layer
*around* the codecs. The cipher and key derivation are in
[transport-cipher.md](transport-cipher.md) and
[handshake-key-derivation.md](handshake-key-derivation.md); framing is in
[inbound-framing.md](inbound-framing.md); the send encoder is in
[outbound-messages.md](outbound-messages.md).

## Mode word (`conn+0x108`)

`MsgConn` carries a `u32` mode at `conn+0x108`. Only three values are reachable.

| Value | Name | Meaning | Written by |
| --- | --- | --- | --- |
| `1` | `MSGCONN_MODE_CLIENT_START` | handshake start; inbound frames are appended to the handshake buffer `conn+0x80` and are not dispatched as messages | `MsgConn_SetMode(conn, 1)` |
| `2` | *(no name recovered)* | **initial** state after the constructor sends the client's Diffie-Hellman public value; awaits the server's key frame | `FUN_140fe8b70` (constructor) |
| `3` | `MSGCONN_MODE_ENCRYPTED` | keyed/established; the mode-3 receive pipeline and the encrypted outbound flush run | `MsgConn_SetMode(conn, 0)`, and `MsgRaw_ClientRecvEncrypt` |

`MsgConn_SetMode(conn, x)` writes `1` when `x != 0` and `3` when `x == 0`; it can never produce `2`.
Mode `2` is set only by the constructor (`FUN_140fe8b70` is called with the third argument `2`).
`MsgConn_SetMode` asserts `conn->protocol == NET_PROTOCOL_CLI2GAME` (protocol `0`, `conn+0x10`), so
this machine governs the game connection only.

### Transitions

```text
                 constructor FUN_140fe8b70(.., 2)      MsgConn_SetMode(conn, 1)
   (new conn) -------------------------------------> 2 ------------------------> 1
                                                      |                          (buffering)
                       server key frame (kind 1)      |
                       state key = f[2..0x15] XOR conn+0x118
                       KSA conn+0x12C, copy -> conn+0x234
                                                      v
                                                      3  (established)
```

- `2 -> 3` happens in `MsgRaw_ClientRecvEncrypt` (dispatch-table kind `1`). It is the only place the
  transport cipher is keyed, and it copies the inbound state `conn+0x12C` to the outbound slot
  `conn+0x234`.
- The outbound flush asserts `mode == 3` (see below), so no encrypted gameplay may be sent before the
  server key frame arrives.
- `MsgConn_SetMode(conn, 1)` is reachable from the network layer (`FUN_14023ed80`, event 1). Whether
  a real game connection passes through mode `1` or stays in `2` until keyed is **not resolved** here;
  the constructor default is `2`.

## Handshake / control frame space

In modes `1`/`2` the inbound buffer is a sequence of frames dispatched through the three-entry table
at `142109a38`, indexed by the first byte `kind` (bounds-checked `< 3`):

| `kind` | Target | Routine | Preconditions |
| --- | --- | --- | --- |
| `0` | `140fec590` | `Msg::Raw::RecvInvalid` | always fails (assert `MsgConn.cpp:0xc61`) |
| `1` | `140fe87f0` | `Msg::Raw::ClientRecvEncrypt` | mode `2`; frame **length** byte `== 0x16` |
| `2` | `140fe8a90` | `Msg::Raw::ClientRecvError` | mode `2`; frame length `> 9` |

Frame layout is `[u8 kind][u8 length][payload]`, where `length` counts the whole frame (see
[inbound-framing.md](inbound-framing.md)).

`kind 0` is the **client's own** channel: the constructor builds it and the client never receives it,
so its inbound handler is a failure stub. The client sends it directly through the transport
(`FUN_140fe3e60`) — this send bypasses the cipher and the outbound buffer entirely:

```text
frame = [u8 kind=0x00][u8 length = len+2][outB bytes]        // outB = 4^R mod P, up to 0x40 bytes
```

The server replies with `kind 1`:

```text
frame = [u8 kind=0x01][u8 length=0x16][20-byte server value]
key   = server_value XOR conn+0x118        // conn+0x118 holds outA[:20]
```

`kind 2` (`ClientRecvError`) carries a server error and requires a frame longer than 9 bytes.

## Outbound send model — no sequence, acknowledgement, or frame header

The outbound stream is **plain concatenation**, not framed:

```text
schema-encode one message (starts with the message-id field)
  -> MsgConn_WriteBytes FUN_140fe8690   append into buffer conn+0x398, cursor conn+0xd0
       (flushes when the next write would exceed the buffer)
  -> flush FUN_140fe96f0 / FUN_140fea0f0
       mode 1: discard the buffer (cursor reset); handshake frames are sent directly
       mode 3: MsgUtil_CryptStream(conn+0x234, len, conn+0x398, tmp); send tmp raw
  -> transport *(conn+8), length = cursor - conn+0x398; cursor reset
```

There is **no per-frame length, sequence number, acknowledgement, or compression container** on the
outbound side. Message boundaries are recovered by the peer from the schema (message-id field first),
exactly as the client does for inbound traffic. The only auxiliary structure is a timestamp ring at
`conn+0xd8` updated on each flush (`FUN_140fee540`) whose purpose is unresolved.

This resolves the previously open "outbound sequence/acknowledgement rules": for build `205.780`
there are none at the transport layer. Message-level acknowledgements (for example the `0x1A5`
world-entry completion) are gameplay semantics, not transport sequencing, and remain governed by the
send corpus.

## Connection lifecycle (network layer)

`FUN_14023ed80` (`Gw2\\Game\\Net\\Cli\\GcGameCmd.cpp`) dispatches connection events; the game
connection object lives in the global `DAT_1426632d0`:

| Event | Action |
| --- | --- |
| `0` | reset the module state |
| `1` | create the `MsgConn` (`FUN_140fe9b00`): generate/accept the 20-byte seed, run the DH KDF, store `outA[:20]` at `conn+0x118`, send the `kind 0` hello; install the dispatch callback; may `MsgConn_SetMode(conn, 1)` |
| `2` | build a 0x26-byte request and hand it to the socket layer |
| `3` | destroy the connection (`fun_140fe9de0`) and clear the globals |
| `4` | route a received packet to `Net_OnClientPacketReceived` -> `MsgConn_Dispatch` |

## Corrections to earlier claims

1. **Mode numbering.** `inbound-framing.md` (and the original note) list `2` as
   `MSGCONN_MODE_ENCRYPTED`. The assert at `MsgConn.cpp:0x291` (`mc->mode == MSGCONN_MODE_ENCRYPTED`)
   guards the flush's `mode != 3` branch, so **`MSGCONN_MODE_ENCRYPTED` is `3`**, not `2`. Mode `2` is
   the unnamed initial state. `MSGCONN_MODE_CLIENT_START` is `1` (guarded by
   `MsgConn.cpp:0xc26`, `!MSGCONN_MODE_CLIENT_START`, for the expected mode `2`).
2. **`ClientRecvEncrypt` precondition.** The check is on the frame **length** byte
   (`*(frame+1) == 0x16`, i.e. a 20-byte payload), not the frame kind. The kind is `1` (its
   dispatch-table index); the kind byte is never `0x16` in this space.

`inbound-framing.md` has been updated to match (1) and (2).

## Open

- Whether a real game connection enters mode `1` or remains in mode `2` until keyed.
- The purpose of the `conn+0xd8` timestamp ring.
- The server side of the handshake and of the key delivery frame.
- The runtime package does not yet model this machine or emit/parse the handshake frames; the DH
  modexp (512-bit) is not implemented in the runtime.
