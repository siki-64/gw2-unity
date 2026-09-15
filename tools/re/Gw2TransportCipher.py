#!/usr/bin/env python3
"""Reference transport stream cipher (Gw2-64.exe).

Reconstructs, from the disassembly of MsgUtil_Rc4Ksa (140feea50) and
MsgUtil_CryptStream (140fee7c0), the key schedule and keystream generator the
client uses on the game connection. This is reference material for offline
decoding; it is not runtime code and is not registered as a supported wire
codec.

Algorithm (statically recovered, image SHA-256
612364759DFA24800F4F9802F842323FC21EB127A08DB8132BC12EA74D5FC3D1):

  key schedule (140feea50)
    1. zero a 20-byte buffer and XOR in up to 20 key bytes (longer is
       asserted and clamped to 20).
    2. treat the buffer as five little-endian u32 words and apply a bespoke
       five-word mixing (four dependent steps; see mix_five_words).
    3. seed S[256] with the identity permutation and run a textbook RC4 KSA
       using the 20 mixed bytes as the key (key index wraps at 20).

  keystream (140fee7c0)
    textbook RC4 PRGA; state is (i, j, S[256]) and persists across calls.

The mixing uses constants 0x9fb498b3, 0x66b0cd0d, 0x7bf36ae2, 0xf33d5697,
0xd675e47b, 0xb453c259, 0x59d148c0 and the mask 0x22222222. It is read
directly from the instruction stream; it is not standard MD5 (MD5 uses four
64-step rounds over a 16-word block, not four dependent steps over five
words). The name "Rc4Ksa" is the previous analyst's label and is kept for
continuity.

Confidence and limits
  * The instruction-to-code mapping is recorded step by step in the Protocol notes
    (research/contracts/notes/Protocol/msg-schema-207032.md).
  * Validated against live traffic (the schema re-derivation): the PRGA
    reproduces the client's CryptStream output byte-for-byte for a captured
    game-connection packet, and advancing key_schedule from the captured key
    matches the client's live state (j, S[256]) exactly. The key and capture
    that proved this are private and uncommitted.
  * The reference vector this tool emits is SYNTHETIC and remains a regression
    check only; it is not the evidence of wire correctness.

Usage
  python tools/re/Gw2TransportCipher.py --selftest
  python tools/re/Gw2TransportCipher.py --emit-fixture tools/re/fixtures/207032/transport-cipher.json
"""

import argparse
import hashlib
import json
import os
import sys

MASK32 = 0xFFFFFFFF
KEY_LEN = 0x14

C_A0 = 0x9FB498B3
C_A1 = 0x66B0CD0D
C_A2_F = 0x7BF36AE2
C_A2 = 0xF33D5697
C_A3 = 0xD675E47B
C_A3_B = 0x59D148C0
C_W0 = 0xB453C259
C_W0_MASK = 0x22222222


def rol32(x, n):
    x &= MASK32
    return ((x << n) | (x >> (32 - n))) & MASK32


def mix_five_words(w):
    """Four dependent mixing steps over five u32 words (140feeb20..140feebd6).

    Returns the five mixed words. Each `A` below corresponds to the register
    value carrying the same name in the disassembly; the comments give the LEA
    or ALU immediate that fixes the constant.
    """
    w0, w1, w2, w3, w4 = [x & MASK32 for x in w]

    a0 = (w0 + C_A0) & MASK32                       # LEA ESI,[R11-0x604b674d]
    b0 = rol32(a0, 30)                              # ROL ESI,0x1e

    a1 = (w1 + C_A1 + rol32(a0, 5)) & MASK32        # LEA EDI,[RBX+0x66b0cd0d]; ADD EDI,EAX
    b1 = rol32(a1, 30)                              # ROL EDI,0x1e

    f2 = (~(a0 & C_W0_MASK)) & C_A2_F               # AND EAX,0x22222222; NOT; AND 0x7bf36ae2
    a2 = (rol32(a1, 5) + w2 + f2 + C_A2) & MASK32   # ADD R9D,R10D; ADD R9D,EAX; ADD EAX,0xf33d5697
    b2 = rol32(a2, 30)                              # ROL R9D,0x1e

    f3 = (((b0 ^ C_A3_B) & a1) ^ C_A3_B)            # XOR/AND/XOR 0x59d148c0
    a3 = (rol32(a2, 5) + w3 + f3 + C_A3) & MASK32   # ADD EDX,R8D; ADD EDX,EAX; ADD EAX,0xd675e47b

    f4 = (((b0 ^ b1) & a2) ^ b0)                    # AND happens before ROL R9D,0x1e
    n0 = (f4 + w0 + w4 + rol32(a3, 5) + C_W0) & MASK32   # ADD EAX,R15D/R11D/ECX
    n1 = (w1 + a3) & MASK32                         # ADD EBX,EDX
    n2 = (w2 + b2) & MASK32                         # ADD R10D,R9D
    n3 = (w3 + b1) & MASK32                         # ADD R8D,EDI
    n4 = (w4 + b0) & MASK32                         # ADD R15D,ESI
    return [n0, n1, n2, n3, n4]


def mix_five_words_asm_style(w):
    """Same mixing written as the destructive register updates of the listing.

    Used only by the self-test: it mirrors 140feeb20..140feebd6 operation for
    operation, so comparing it with mix_five_words catches an algebraic slip
    such as reading an original word where the assembly reads an updated one.
    """
    w0, w1, w2, w3, w4 = [x & MASK32 for x in w]
    r11, ebx, r10, r8, r15 = w0, w1, w2, w3, w4
    esi = (r11 + C_A0) & MASK32
    eax = rol32(esi, 5)
    edi = (ebx + C_A1) & MASK32
    edi = (edi + eax) & MASK32
    eax = esi & C_W0_MASK
    esi = rol32(esi, 30)
    eax = (~eax) & MASK32
    r9 = edi
    eax &= C_A2_F
    r9 = rol32(r9, 5)
    eax = (eax + C_A2) & MASK32
    r9 = (r9 + r10) & MASK32
    r9 = (r9 + eax) & MASK32
    eax = esi ^ C_A3_B
    edx = r9
    eax &= edi
    edx = rol32(edx, 5)
    eax ^= C_A3_B
    edi = rol32(edi, 30)
    eax = (eax + C_A3) & MASK32
    edx = (edx + r8) & MASK32
    edx = (edx + eax) & MASK32
    r8 = (r8 + edi) & MASK32
    ecx = rol32(edx, 5)
    ebx = (ebx + edx) & MASK32
    ecx = (ecx + C_W0) & MASK32
    eax = esi ^ edi
    eax &= r9
    r9 = rol32(r9, 30)
    eax ^= esi
    r10 = (r10 + r9) & MASK32
    eax = (eax + r15) & MASK32
    eax = (eax + r11) & MASK32
    eax = (eax + ecx) & MASK32
    r15 = (r15 + esi) & MASK32
    return [eax, ebx, r10, r8, r15]


def derive_key(key_bytes):
    """XOR-and-mix step 1+2 of the key schedule. Returns 20 bytes."""
    buf = bytearray(KEY_LEN)
    for i, b in enumerate(key_bytes[:KEY_LEN]):
        buf[i] ^= b
    words = [int.from_bytes(buf[i * 4:i * 4 + 4], "little") for i in range(5)]
    mixed = mix_five_words(words)
    return b"".join(w.to_bytes(4, "little") for w in mixed)


def rc4_ksa(key_bytes):
    """Textbook RC4 KSA; key index wraps at len(key_bytes)."""
    s = list(range(256))
    j = 0
    n = len(key_bytes)
    for i in range(256):
        j = (j + s[i] + key_bytes[i % n]) & 0xFF
        s[i], s[j] = s[j], s[i]
    return s


def key_schedule(key_bytes):
    """Full schedule. Returns (i, j, S) with i = j = 0 and the KSA S-box."""
    if len(key_bytes) > KEY_LEN:
        key_bytes = key_bytes[:KEY_LEN]
    mixed = derive_key(key_bytes)
    return [0, 0, rc4_ksa(mixed)], mixed


def crypt_stream(state, data):
    """RC4 PRGA, one state object (i, j, S) shared across calls."""
    i, j, s = state[0], state[1], state[2]
    out = bytearray(len(data))
    for k, b in enumerate(data):
        i = (i + 1) & 0xFF
        j = (j + s[i]) & 0xFF
        s[i], s[j] = s[j], s[i]
        out[k] = s[(s[i] + s[j]) & 0xFF] ^ b
    state[0], state[1] = i, j
    return bytes(out)


def _textbook_rc4(key_bytes, data):
    s = rc4_ksa(key_bytes)
    i = j = 0
    out = bytearray(len(data))
    for k, b in enumerate(data):
        i = (i + 1) & 0xFF
        j = (j + s[i]) & 0xFF
        s[i], s[j] = s[j], s[i]
        out[k] = s[(s[i] + s[j]) & 0xFF] ^ b
    return bytes(out)


def selftest():
    errors = []

    # 1. The expression form and the mutation form must agree. This catches an
    #    algebra slip (e.g. reading an original word where the asm reads an
    #    updated one); it is a formulation check, not protocol evidence.
    test_words = ([0, 0, 0, 0, 0], [0xFFFFFFFF] * 5,
                  [0x01020304, 0x05060708, 0x090A0B0C, 0x0D0E0F10, 0x11121314],
                  [0x9E3779B9, 0x00000001, 0x80000000, 0x7FFFFFFF, 0xDEADBEEF])
    for w in test_words:
        a = mix_five_words(w)
        b = mix_five_words_asm_style(w)
        if a != b:
            errors.append(f"mix expression != mutation form for {w}: {a} vs {b}")

    # 2. RC4 KSA+PRGA must equal an independent textbook implementation for the
    #    derived key. This checks the KSA/PRGA half of the reconstruction.
    key = bytes(range(20))
    mixed = derive_key(key)
    s = rc4_ksa(mixed)
    keystream_state = [0, 0, list(s)]
    ours = crypt_stream(keystream_state, b"\x00" * 64)
    theirs = _textbook_rc4(mixed, b"\x00" * 64)
    if ours != theirs:
        errors.append("KSA/PRGA disagrees with textbook RC4 for derived key")

    # 3. Key length edge cases: empty key, short key, over-long key clamps.
    for klen in (0, 1, 7, 8, 19, 20, 25):
        k = bytes((i * 7 + 3) & 0xFF for i in range(klen))
        st, _ = key_schedule(k)
        if st[2][0] < 0 or st[2][0] > 255:
            errors.append(f"S[0] out of range for keylen {klen}")
        if klen > 20 and derive_key(k) != derive_key(k[:20]):
            errors.append("over-long key did not clamp to 20")

    # 4. Stream state must carry: two half-calls equal one whole call.
    st1, _ = key_schedule(bytes(range(20)))
    whole = crypt_stream(st1, bytes(range(128)))
    st2, _ = key_schedule(bytes(range(20)))
    part = crypt_stream(st2, bytes(range(64))) + \
        crypt_stream(st2, bytes(range(64, 128)))
    if whole != part:
        errors.append("stream state did not carry across calls")

    return errors


def fixture_vector(key_bytes):
    state, mixed = key_schedule(key_bytes)
    sbox = bytes(state[2])
    keystream = crypt_stream(state, b"\x00" * 32)
    return {
        "inputKey": key_bytes.hex(),
        "mixedKey": mixed.hex(),
        "sboxAfterKsa": sbox.hex(),
        "keystream32": keystream.hex(),
    }


def build_fixture():
    vectors = [
        fixture_vector(bytes(range(20))),
        fixture_vector(b"\x00" * 20),
        fixture_vector(b"\xff" * 20),
    ]
    body = {
        "kind": "synthetic",
        "build": 207032,
        "buildLabel": "207.032",
        "imageSha256": "612364759DFA24800F4F9802F842323FC21EB127A08DB8132BC12EA74D5FC3D1",
        "algorithm": "MsgUtil_Rc4Ksa(140feea50) 5-word mixing + RC4 KSA, "
                     "MsgUtil_CryptStream(140fee7c0) RC4 PRGA",
        "provenance": "Generated by tools/re/Gw2TransportCipher.py from the "
                      "static reconstruction in the schema re-derivation.",
        "noClaim": "Synthetic reference vector. It tests an implementation of "
                   "the reconstruction and does not confirm the client's wire "
                   "output; that needs a captured frame.",
        "vectors": vectors,
    }
    canonical = json.dumps(body, sort_keys=True, separators=(",", ":")).encode()
    body["sha256"] = hashlib.sha256(canonical).hexdigest()
    return body


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--emit-fixture", metavar="PATH")
    args = ap.parse_args()

    if args.selftest:
        errors = selftest()
        for e in errors:
            print("FAIL", e)
        if errors:
            return 1
        print("OK Gw2TransportCipher selftest (4 groups)")
        return 0

    if args.emit_fixture:
        fixture = build_fixture()
        os.makedirs(os.path.dirname(os.path.abspath(args.emit_fixture)),
                    exist_ok=True)
        with open(args.emit_fixture, "w", newline="\n") as f:
            json.dump(fixture, f, indent=2, sort_keys=True)
            f.write("\n")
        print(f"wrote {args.emit_fixture} sha256 {fixture['sha256']}")
        return 0

    ap.print_help()
    return 0


if __name__ == "__main__":
    sys.exit(main())
