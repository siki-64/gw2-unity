# Transport cipher

**Build:** `207.032` (Gw2-64.exe, image identity in protocol/catalog.json).

**Status:** reconstructed and validated against live traffic. Evidence trail: the recovered evidence
(reconstruction), the recovered evidence (byte-for-byte PRGA and KSA state match), the recovered evidence (outbound state),
the recovered evidence (cross-direction reuse). All in
[msg-schema-207032.md](msg-schema-207032.md).

## Algorithm

Source unit `Gw2\Services\Msg\MsgUtil.cpp`. It is **not** stock RC4: the key is pre-mixed before the
KSA. Three phases.

### 1. Key material

Zero a 20-byte buffer and copy in up to 20 key bytes (a longer key is asserted and clamped to 20).
Treat it as five little-endian `u32` words `w0..w4`.

### 2. Five-word mixing

Four dependent steps with `ROL(x, n)` a 32-bit rotate-left (constants do **not** match the MD5,
SHA-1 or SHA-2 round tables):

```text
A0 = w0 + 0x9fb498b3
B0 = ROL(A0, 30)
A1 = w1 + 0x66b0cd0d + ROL(A0, 5)
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

The fourth boolean term uses **`A2`, not `B2`** (the `AND` executes before the `ROL`). A
reconstruction that rotates first disagrees.

### 3. RC4 KSA and PRGA

`K'` (20 bytes) is the RC4 key: identity permutation, then `j = (j + S[i] + K'[i mod 20]) & 0xff`
for `i` in `0..255`. The PRGA is textbook:

```text
i = (i + 1) & 0xff
j = (j + S[i]) & 0xff
swap(S[i], S[j])
out = S[(S[i] + S[j]) & 0xff] ^ in
```

## State layout

`state = { u32 i, u32 j, byte S[256] }` — `0x108` bytes. `i` and `j` persist across calls, so the
cipher is a stateful keystream and cannot be fingerprinted or replayed from a single frame.

| Connection | State offset | Direction |
| --- | --- | --- |
| game | `conn+0x12C` | inbound (server -> client) |
| game | `conn+0x234` | outbound (client -> server) |

## Cross-direction reuse (two-time pad)

The handshake copies the whole `conn+0x12c` state to `conn+0x234`, so both directions start on the
same permutation from the same key (the recovered evidence). Confirmed live: advancing the outbound state by
2,795,974 bytes reproduced the inbound state exactly, `i`, `j` and the full S-box (the recovered evidence).
Therefore the two directions are **one keystream**:

```text
ciphertext_in XOR ciphertext_out = plaintext_in XOR plaintext_out   (equal absolute positions)
```

This is a transport-layer confidentiality weakness, not a decode blocker (a decoder uses one
direction's state at a time).

## Validation and references

| Item | Evidence |
| --- | --- |
| Synthetic vectors (mixed key, post-KSA S-box, keystream) | `tools/re/fixtures/207032/transport-cipher.json` |
| PRGA byte-for-byte and KSA state match on live bytes | the recovered evidence |
| Captured `0x264` ciphertext -> transport frame | `tools/re/fixtures/207032/0x264-wire.json`, `tools/Gw2.Protocol.Tests` |
| Cross-direction reuse | `tools/re/fixtures/207032/keystream-reuse.json` |

References: `tools/re/Gw2TransportCipher.py`; runtime `Gw2.Protocol.Transport.TransportCipher`.

## Open

- The server side of the reuse was not observed.
- The key is not derivable from the wire alone (see
  [handshake-key-derivation.md](handshake-key-derivation.md)).
