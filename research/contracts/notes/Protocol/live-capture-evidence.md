# Live capture evidence

**Build:** `207.032` (Gw2-64.exe, image identity in protocol/catalog.json).

**Status:** several layers are validated against private live captures. See
[msg-schema-207032.md](msg-schema-207032.md).

Live sessions used **hardware breakpoints only, no patching**. Raw captures and session keys are
private under `captures/local/` (git-ignored); only sanitized fixtures are committed.

## Committed fixtures (`tools/re/fixtures/207032/`)

| Fixture | Kind | What it pins |
| --- | --- | --- |
| `transport-cipher.json` | synthetic | mixed key, post-KSA S-box, keystream (tests an implementation) |
| `0x264-wire.json` | sanitized capture | inbound ciphertext -> transport frame -> two `0x264` messages |
| `handshake-kdf.json` | synthetic + captured vector | `R = Park-Miller(seed)`, `outA = G^R mod P`; the captured vector reproduces `conn+0x118` |
| `outbound-0x120.json` | sanitized capture | outbound plaintext `2001888f0101`, state, ciphertext `5a3160fb37db` |
| `keystream-reuse.json` | sanitized capture | inbound + outbound states that align after 2,795,974 bytes |

## What each live session established

| Finding | Result |
| --- | --- |
| 14 | the reader decodes a real 1807-byte stream (9 messages) and the live registry matches the static corpus |
| 15 | two `0x264` decoded handler records captured |
| 16 | the reconstructed cipher reproduces the client's PRGA byte-for-byte and the KSA state |
| 17-18 | the wire decrypts; ids collide across sites; the live recv registry is read |
| 19-20 | the inbound stream is framed (`[compLen][decodedLen]` + LZ4); a captured wire stream decodes to 52 messages |
| 22 | the first wire-bytes fixture: a captured `0x264` ciphertext -> frame -> messages; the varint order is confirmed from data |
| 26 | the handshake KDF is confirmed (seed -> `conn+0x118`); one outbound packet is captured end to end |
| 28 | cross-direction keystream reuse is confirmed |

## Operational cautions (cost rework if ignored)

- **Do not pause at the handshake KSA** (`MsgUtil_Rc4Ksa`) during a connect: resuming faulted a
  worker thread and crashed the client. The key is readable at `conn+0x118` once in-world.
- **Do not break on every `MsgUtil_CryptStream` call** interactively: it stalls the client per packet
  and, held long enough, causes a server-side network timeout.
- **The workable method is a handler-only breakpoint** (low frequency). At a handler the whole packet
  is still live, so `conn+0x58` (frame container) and `conn+0x80` (message stream) are readable.
- **Set hardware breakpoints once** and remove/disable them at a paused point, or relaunch; deleting
  a hardware breakpoint while the client runs is the prime suspect in a prior crash (the recovered evidence).

## Validation tiers

- **Captured replay / offline decode:** transport cipher, framing + LZ4, `0x264` inbound, `0x120`
  outbound, KDF, keystream reuse.
- **Static:** schema format, registry layouts, corpora, outbound internals.
- **Unity:** the `Gw2.Protocol` package compiles in the 6000.6 editor; the tests are the offline
  MSTest harness (`tools/Gw2.Protocol.Tests`).
- **Not claimed:** live interoperability, and no runtime wire message is registered as supported.

## Open

- One outbound packet is captured; broader outbound coverage is pending.
- The server side of the handshake and of the keystream reuse is unobserved.
