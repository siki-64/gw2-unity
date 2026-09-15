# Handshake key derivation

**Build:** `205.780` (Gw2-64.exe, image SHA-256
`D2AE84876A0B93277FCCB368969046B848BB0403FD09DB420389C813D2459B23`).

**Status:** recovered statically and **confirmed live**. Evidence trail: Addenda 10-13 (location and
seed) and Addendum 23 (the derivation) with Addendum 26 (live confirmation), all in
[msg-dispatch-addenda.md](msg-dispatch-addenda.md).

This is the key material behind the [transport cipher](transport-cipher.md): it is a
**Diffie-Hellman exchange**, not a hash — so the session key is **not derivable from the wire
alone**.

## The function (`FUN_140fede80`, `MsgConn.cpp`)

`FUN_140fede80(size, desc, keyLen, seed, &outA, &outB)`, when `size == 0x88` and `desc[0] == 1`:

```text
blobA = desc[0x08 .. 0x48]      // 64 bytes, 512-bit modulus P
blobB = desc[0x48 .. 0x88]      // 64 bytes, generator G
small = desc[1] = 4
R     = FUN_141570640(seed, 512)     // 512-bit exponent from the 20-byte seed
outA  = blobB ^ R mod blobA          // the shared secret
outB  = 4     ^ R mod blobA          // the client's public value, sent to the server
```

`desc` is `DAT_142109e40`. The bignum library: `FUN_14156ee00` from-bytes (little-endian limbs),
`FUN_14156fa40` multiply, `FUN_14156f420` reduce, `FUN_141570de0` square, `FUN_141570bf0/cd0`
shift, `FUN_14156fdc0` modular exponentiation.

## The PRNG (`FUN_141570640`)

Fill 16 32-bit words (512 bits). For each output word, XOR the seed limb with `0x75bd924` when the
output index equals the seed index, then apply the Park-Miller minimal-standard LCG
(`x/0xadc8 + x*0xbc8f`, mask `0x7fffffff`) twice, packing the low 16 bits of each step; write the
mutated value back to the seed limb and advance the seed index cyclically. So `R` is a deterministic
function of the 20-byte seed.

## The seed

The client generates it (`FUN_140fdf2d0`): it XORs a persistent 20-byte global with the caller's
buffer, folds in `timeGetTime()`, `FILETIME` and two perf-counter reads, SHA-1s the result, and XORs
that back. It is fresh client entropy carrying persistent global history — not on the wire.

## The rest of the handshake

`MsgConn` constructor `FUN_140fe9b00`: copies the seed to `conn+0x118`, calls `FUN_140fede80`, zeroes
`conn+0x118`, then stores `outA[0:20]` there. It sends `outB` as `{0x00, len+2}` + `outB` through the
transport at `*(conn+8)`.

`MsgRaw_ClientRecvEncrypt` (mode 2, frame kind `0x16`):

```text
key[0..19] = frame[2..0x15] XOR conn[0x118..0x12b]
conn+0x108 = 3
MsgUtil_Rc4Ksa(conn+0x12c, 0x14, key)      // inbound cipher state
copy 0x108 bytes conn+0x12c -> conn+0x234  // outbound state (same key: Addendum 25/28)
```

So the RC4 key = `outA XOR server_value`, where `server_value` is on the wire and `outA` is the local
shared secret.

## The constants, and why the wire is not enough

Little-endian as stored; `P = blobA`, `G = blobB`:

```text
blobA f9e43af5 25f1b41f a263a007 48cdf148 98ef8f59 783e8fcd 70b41db9 4afd3d0a
      97ac5657 1c3b5ee7 c4a8804f 63961fd8 4158e553 f1ed49cb 4c470b72 598573f9
blobB 270e7b58 49658baf 6bbadfe4 a86ee1f0 0596f819 b970eeb9 fcb20802 41c28b09
      8ff4737c cd996175 48246508 4eb44ff8 9567c6fb d6a3e6a5 6292623d c358a6c4
```

`P` is a 512-bit probable prime; `P - 1 = 2^3 · 5^2 · 7 · C` where `C` is a 502-bit composite with no
factor `<= 2·10^6`. Recovering `R` from the wire value `outB = 4^R mod P` is therefore an infeasible
discrete log.

**Consequence:** a capture must carry per-session material — the seed, or `conn+0x118` (`outA`) /
`conn+0x12C` (PRGA state). A pcap alone is not enough.

## Live confirmation (Addendum 26)

At the game-connection `FUN_140fede80` call, seed `0558904f44f11d2eb022eb43b0a25441b6c0d800`
produced `conn+0x118 = 390aaf8f5e6104ebb9bc24736365c65a40124e59`, exactly the reconstruction's
`outA[:20]`. Fixture `tools/re/fixtures/205780/handshake-kdf.json`; reference
`tools/re/Gw2HandshakeKdf.py`; runtime `Gw2.Protocol.Session.HandshakeKeyExchange` (`ExpandExponent`,
`Derive`), which reproduces the fixture's synthetic vectors and this captured vector.

## Open

- The server side of the exchange, and whether `blobB = 4^s` for a stable `s`.
- The origin of the seed beyond "client entropy" (no writer beyond `FUN_140fdf2d0`).
