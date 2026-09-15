#!/usr/bin/env python3
"""Reference implementation of the build 205.780 handshake key derivation.

Recovered statically from Gw2-64.exe (see msg-dispatch Addendum 23):

  FUN_140fede80(size=0x88, desc, keyLen, seed, &outA, &outB):
      R    = FUN_141570640(seed, 512)     # 512-bit exponent from the 20-byte seed
      outA = blobB ^ R mod blobA          # shared secret; conn+0x118 = outA[0:20]
      outB = 4     ^ R mod blobA          # public value sent to the server

`desc` is DAT_142109e40 = {u32 1, u32 4, blobA[64], blobB[64]}. The two 64-byte
constants are read from that descriptor; the modulus P (blobA) is a 512-bit
probable prime, so the exponent R is not recoverable from outB.

This is an offline reference. It does NOT make a bare capture decryptable: the
seed is fresh client entropy (FUN_140fdf2d0) and is not on the wire.

Usage
  python tools/re/Gw2HandshakeKdf.py --selftest
  python tools/re/Gw2HandshakeKdf.py --seed <40-hex>
  python tools/re/Gw2HandshakeKdf.py --seed <40-hex> --outA <40-hex>   # compare
"""

import argparse
import hashlib
import json
import sys

# Little-endian bytes as stored in DAT_142109e40 (limb 0 is the least significant).
BLOB_A = bytes.fromhex(
    "f9e43af525f1b41fa263a00748cdf14898ef8f59783e8fcd70b41db94afd3d0a"
    "97ac56571c3b5ee7c4a8804f63961fd84158e553f1ed49cb4c470b72598573f9")
BLOB_B = bytes.fromhex(
    "270e7b5849658baf6bbadfe4a86ee1f00596f819b970eeb9fcb2080241c28b09"
    "8ff4737ccd996175482465084eb44ff89567c6fbd6a3e6a56292623dc358a6c4")

P = int.from_bytes(BLOB_A, "little")
G = int.from_bytes(BLOB_B, "little")

# Park-Miller minimal-standard LCG (FUN_141570640 constants).
PM_A = 0xbc8f      # 48271
PM_Q = 0xadc8      # 44488, the Schrage quotient
PM_MASK = 0x7fffffff
XOR_SEED = 0x75bd924

# Captured live (msg-dispatch Addendum 26): the game-connection seed and the
# outA[:20] the client stored at conn+0x118. outA alone is not the session key
# (the key is outA XOR a server value that is not recorded here).
CAPTURED_SEED = "0558904f44f11d2eb022eb43b0a25441b6c0d800"
CAPTURED_OUTA20 = "390aaf8f5e6104ebb9bc24736365c65a40124e59"


def _limbs(raw):
    """Little-endian 32-bit limbs, as FUN_14156ee00 stores them."""
    return [int.from_bytes(raw[i:i + 4].ljust(4, b"\x00"), "little")
            for i in range(0, len(raw), 4)]


def park_miller_expand(seed, bits=512):
    """FUN_141570640: expand `seed` (bytes) to `bits` bits of Park-Miller output.

    FUN_141570640 also mutates and rotates the seed bignum for repeated calls;
    FUN_140fede80 calls it once, so the return value is all that matters here.
    """
    words = bits >> 5
    S = _limbs(seed)
    size = len(S)
    while size > 0 and S[size - 1] == 0:
        size -= 1
    if size == 0:                      # FUN_141570640 forces size >= 1
        S = [0]
        size = 1

    out = []
    idx = 0
    for k in range(words):
        x = (XOR_SEED if k == idx else 0) ^ S[idx]
        word = 0
        for step in range(2):
            x = ((x // PM_Q) + x * PM_A) & 0xFFFFFFFF
            state = x & PM_MASK
            word |= (x & 0xFFFF) << (16 * step)
            x = state
        out.append(word & 0xFFFFFFFF)
        S[idx] = state
        idx = (idx + 1) % size

    return int.from_bytes(b"".join(w.to_bytes(4, "little") for w in out), "little")


def derive(seed):
    """Return (R, outA, outB) for a 20-byte-or-shorter seed."""
    R = park_miller_expand(seed)
    return R, pow(G, R, P), pow(4, R, P)


def derived_key(seed, key_len=20):
    """The 20 bytes the client stores at conn+0x118 (outA truncated, LE)."""
    return derive(seed)[1].to_bytes(64, "little")[:key_len]


def selftest():
    errors = []
    seed = bytes.fromhex("000102030405060708090a0b0c0d0e0f10111213")
    R, outA, outB = derive(seed)
    if R.bit_length() > 512:
        errors.append("R exceeds 512 bits")
    if outA != pow(G, R, P) or outB != pow(4, R, P):
        errors.append("modpow mismatch")
    if park_miller_expand(seed) != R:
        errors.append("expand not deterministic")
    # Independent recomputation of the 16 Park-Miller output words.
    words = []
    S = _limbs(seed)
    size = len(S)
    while size > 1 and S[size - 1] == 0:
        size -= 1
    idx = 0
    for k in range(16):
        x = ((XOR_SEED if k == idx else 0) ^ S[idx])
        acc = 0
        for step in range(2):
            x = ((x // PM_Q) + x * PM_A) & 0xFFFFFFFF
            acc |= (x & 0xFFFF) << (16 * step)
            x &= PM_MASK
        words.append(acc)
        S[idx] = x
        idx = (idx + 1) % size
    R2 = int.from_bytes(b"".join(w.to_bytes(4, "little") for w in words), "little")
    if R2 != R:
        errors.append("independent recomputation disagrees")
    # A non-zero top limb changes the cycling period, so exercise both shapes.
    for s in (bytes(20), bytes.fromhex("ff" * 20), bytes(range(19)) + b"\x00"):
        derive(s)
    # outA must satisfy the relation blobB^R = outA.
    if pow(G, R, P) != outA:
        errors.append("outA relation broken")
    # Captured live vector (Addendum 26): seed -> outA[:20] must reproduce conn+0x118.
    if derived_key(bytes.fromhex(CAPTURED_SEED)).hex() != CAPTURED_OUTA20:
        errors.append("captured KDF vector mismatch")
    return errors


def emit_fixture(path):
    """Write a synthetic KDF vector set. It tests a reimplementation; it does
    not confirm the client's output (that needs a captured (seed, outA) pair)."""
    seeds = [
        "000102030405060708090a0b0c0d0e0f10111213",
        "0000000000000000000000000000000000000000",
        "ffffffffffffffffffffffffffffffffffffffff",
    ]
    vectors = []
    for s in seeds:
        R, outA, outB = derive(bytes.fromhex(s))
        vectors.append({
            "seed": s,
            "R": f"{R:0128x}",
            "outA": outA.to_bytes(64, "little").hex(),
            "outB": outB.to_bytes(64, "little").hex(),
        })
    body = {
        "algorithm": "FUN_140fede80: R=FUN_141570640(seed,512); outA=G^R mod P; outB=4^R mod P",
        "build": 205780,
        "buildLabel": "205.780",
        "imageSha256": "d2ae84876a0b93277fccb368969046b848bb0403fd09db420389c813d2459b23",
        "kind": "synthetic",
        "representation": "wire-bytes",
        "modulusP": BLOB_A.hex(),
        "generatorG": BLOB_B.hex(),
        "noClaim": ("Synthetic reference vectors. They test an implementation of the "
                    "reconstruction and do not confirm the client's output; that needs a "
                    "captured (seed, outA) pair. The seed is fresh client entropy and is "
                    "not on the wire, so a bare capture cannot be decrypted from this."),
        "provenance": "Generated by tools/re/Gw2HandshakeKdf.py from the static "
                      "reconstruction in msg-dispatch Addendum 23.",
        "capturedVector": {
            "seed": CAPTURED_SEED,
            "outA20": CAPTURED_OUTA20,
            "provenance": "Live game connection, build 205.780 (msg-dispatch Addendum 26): "
                          "the seed was read at the FUN_140fede80 call and outA[:20] at "
                          "conn+0x118 after it. outA alone is not the session key.",
        },
        "vectors": vectors,
    }
    blob = (json.dumps(body, indent=2) + "\n").encode()
    body["sha256"] = hashlib.sha256(blob).hexdigest()
    with open(path, "w", newline="\n") as f:
        f.write(json.dumps(body, indent=2) + "\n")
    return path


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--seed", metavar="HEX", help="20-byte seed (<=40 hex chars)")
    ap.add_argument("--outA", metavar="HEX",
                    help="expected outA[0:20] from a capture, for comparison")
    ap.add_argument("--emit-fixture", metavar="PATH",
                    help="write synthetic vectors as JSON")
    args = ap.parse_args()

    if args.emit_fixture:
        print("wrote", emit_fixture(args.emit_fixture))
        return 0

    if args.selftest:
        errors = selftest()
        for e in errors:
            print("FAIL", e)
        if errors:
            return 1
        print("OK Gw2HandshakeKdf selftest")
        return 0

    if not args.seed:
        ap.print_help()
        return 0

    seed = bytes.fromhex(args.seed)
    R, outA, outB = derive(seed)
    print("R       ", f"{R:0128x}")
    print("outA    ", outA.to_bytes(64, "little").hex())
    print("outB    ", outB.to_bytes(64, "little").hex())
    print("conn+118", derived_key(seed).hex())
    if args.outA:
        expected = bytes.fromhex(args.outA)
        got = derived_key(seed)
        print("match   ", got == expected)
        return 0 if got == expected else 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
