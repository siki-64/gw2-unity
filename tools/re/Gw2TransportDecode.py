#!/usr/bin/env python3
"""Offline decoder for the game-connection receive stream.

Two inputs are supported:

  raw capture + key
      a file of encrypted server->client bytes for the game connection and the
      20-byte session key (or the two inputs that produce it). The stream is run
      through the reconstructed transport cipher and then framed.
  decrypted stream
      the bytes already produced by Msg_DispatchStream / the dispatch buffer, in
      which case only framing and schema decoding run.

Framing and schema decoding mirror the recovered reader, MsgPack_ReadFields
(140febc30):

  * a mode-3 message stream is a concatenation of messages with no per-message
    length;
  * the *wire* stream is first a frame container (FUN_140fe8ef0):
    [u16 compLen][u16 decodedLen][payload], compLen 0 = raw, else an LZ4 block
    that expands to decodedLen. Use --deframe on wire captures.
  * each message starts with a u16 little-endian message id that the schema
    chain's first field (fieldType 1) consumes;
  * the remaining fields follow the MsgPackFieldDef chain for that id, read from
    the image (or supplied directly in a self-test).

This is schema-directed and has NOT been validated against a capture. It prints
what it decoded and preserves undecoded bytes rather than guessing.

Usage
  python tools/re/Gw2TransportDecode.py --selftest
  python tools/re/Gw2TransportDecode.py --decrypted stream.bin --ids protocol/schema/207032/sweep2.csv
  python tools/re/Gw2TransportDecode.py --capture enc.bin --key <40-hex> --ids ...
  python tools/re/Gw2TransportDecode.py --capture enc.bin --state conn12C.bin --out plain.bin

--state is the 0x108-byte cipher state captured at conn+0x12C (i u32, j u32,
S[256]). It starts the PRGA directly, so a capture taken mid-session decrypts
without replaying the whole keystream; use it when the capture does not begin at
the first encrypted byte.
"""

import argparse
import csv
import json
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import Gw2TransportCipher as cipher  # noqa: E402

IMAGE = r"C:\Program Files (x86)\Steam\steamapps\common\Guild Wars 2\Gw2-64.exe"
DESC_STRIDE = 0x28
MSG_MAX_BUFFER_SIZE = 0x2000
FIXTURE = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                       "fixtures", "207032", "0x264-wire.json")


class PE:
    """Minimal PE VA->file reader (see MaxSizeWalk.py)."""

    def __init__(self, path):
        with open(path, "rb") as f:
            self.blob = f.read()
        dos = struct.unpack_from("<I", self.blob, 0x3C)[0]
        assert self.blob[dos:dos + 4] == b"PE\x00\x00", "not a PE"
        nsec = struct.unpack_from("<H", self.blob, dos + 6)[0]
        opt_size = struct.unpack_from("<H", self.blob, dos + 20)[0]
        opt = dos + 24
        magic = struct.unpack_from("<H", self.blob, opt)[0]
        if magic == 0x10B:
            self.image_base = struct.unpack_from("<I", self.blob, opt + 28)[0]
        else:
            self.image_base = struct.unpack_from("<Q", self.blob, opt + 24)[0]
        sec_base = opt + opt_size
        self.sections = []
        for i in range(nsec):
            off = sec_base + i * 40
            vsize, vaddr, rawsize, rawptr = struct.unpack_from("<IIII",
                                                               self.blob,
                                                               off + 8)
            self.sections.append((vaddr, vsize, rawptr, rawsize))

    def read_va(self, va, size):
        rva = va - self.image_base
        for vaddr, vsize, rawptr, rawsize in self.sections:
            if vaddr <= rva < vaddr + max(vsize, rawsize):
                off = rawptr + (rva - vaddr)
                return self.blob[off:off + size]
        raise KeyError(f"VA 0x{va:x} not in any section")

    def u32(self, va):
        return struct.unpack_from("<I", self.read_va(va, 4))[0]

    def u16(self, va):
        return struct.unpack_from("<H", self.read_va(va, 2))[0]

    def u64(self, va):
        return struct.unpack_from("<Q", self.read_va(va, 8))[0]


def load_id_map(path):
    """msgId -> defArray pointer, from a sweep CSV (OK rows only)."""
    m = {}
    with open(path, newline="") as f:
        for r in csv.DictReader(f):
            if r.get("tag") == "OK" and r.get("ptr"):
                m.setdefault(int(r["id"], 16), r["ptr"])
    return m


def read_chain(pe, va):
    """Read a MsgPackFieldDef chain into a list of dicts."""
    fields = []
    for _ in range(4096):
        ft = pe.u32(va)
        if ft == 0 or ft == 0x18:
            break
        fields.append({
            "fieldType": ft,
            "param": pe.u32(va + 0x10),
            "refTypeDef": pe.u64(va + 0x18),
        })
        va += DESC_STRIDE
    return fields


class DecodeError(Exception):
    pass


def _varint(data, off, end):
    """Little-endian base-128 varint, up to 5 bytes (140fec3c0)."""
    value = 0
    shift = 0
    for _ in range(5):
        if off >= end:
            raise DecodeError("varint end of payload")
        b = data[off]
        off += 1
        value |= (b & 0x7F) << shift
        if (b & 0x80) == 0:
            return value & 0xFFFFFFFF, off
        shift += 7
    raise DecodeError("varint exceeds 5 bytes")


def _cstring16(data, off, end):
    start = off
    while off + 1 < end:
        if data[off] == 0 and data[off + 1] == 0:
            raw = data[start:off]
            return raw.decode("utf-16-le", "replace"), off + 2
        off += 2
    raise DecodeError("unterminated utf16 string")


def _cstring8(data, off, end):
    start = off
    while off < end:
        if data[off] == 0:
            return data[start:off].decode("utf-8", "replace"), off + 1
        off += 1
    raise DecodeError("unterminated string")


def decode_fields(pe, chain, data, off, end, depth=0):
    """Mirror MsgPack_ReadFields. Returns (list of (ft, value), new off)."""
    if depth > 16:
        raise DecodeError("nesting too deep")
    out = []
    i = 0
    while i < len(chain):
        f = chain[i]
        ft = f["fieldType"]
        p = f["param"]
        if ft in (0, 0x18):
            break
        try:
            if ft in (1, 3):
                v = struct.unpack_from("<H", data, off)[0]
                off += 2
            elif ft == 2:
                v = data[off]
                off += 1
            elif ft in (6, 0x19, 0x17):
                v = struct.unpack_from("<I", data, off)[0]
                off += 4
            elif ft in (5, 7, 0x1A):
                v = data[off:off + 8].hex()
                off += 8
            elif ft == 8:
                v = data[off:off + 0xC].hex()
                off += 0xC
            elif ft in (9, 0xB):
                v = data[off:off + 0x10].hex()
                off += 0x10
            elif ft == 0xC:
                v = data[off:off + 0x1C].hex()
                off += 0x1C
            elif ft == 4:
                v, off = _varint(data, off, end)
            elif ft == 0xA:
                v = {"header": data[off:off + 0xC].hex()}
                off += 0xC
                v["count"], off = _varint(data, off, end)
            elif ft == 0xD:
                v, off = _cstring16(data, off, end)
            elif ft == 0xE:
                v, off = _cstring8(data, off, end)
            elif ft == 0x13:
                v = data[off:off + p].hex()
                off += p
            elif ft == 0x14:
                n = data[off]
                off += 1
                v = data[off:off + n].hex()
                off += n
            elif ft == 0x15:
                n = struct.unpack_from("<H", data, off)[0]
                off += 2
                v = data[off:off + n].hex()
                off += n
            elif ft in (0xF, 0x10, 0x11, 0x12):
                sub = read_chain(pe, f["refTypeDef"]) if pe else f["refTypeDef"]
                if ft == 0xF:
                    present = data[off]
                    off += 1
                    if present:
                        v, off = decode_fields(pe, sub, data, off, end,
                                               depth + 1)
                        v = {"present": 1, "value": v}
                    else:
                        v = {"present": 0}
                else:
                    if ft == 0x11:
                        n = data[off]
                        off += 1
                    elif ft == 0x12:
                        n = struct.unpack_from("<H", data, off)[0]
                        off += 2
                    else:
                        n = p
                    items = []
                    for _ in range(n):
                        item, off = decode_fields(pe, sub, data, off, end,
                                                  depth + 1)
                        items.append(item)
                    v = items
            elif ft == 0x16:
                raise DecodeError("MP_SRV_ALIGN data present")
            else:
                raise DecodeError(f"unknown fieldType 0x{ft:x}")
        except (struct.error, IndexError):
            raise DecodeError(f"overrun at fieldType 0x{ft:x} off {off}")
        if off > end:
            raise DecodeError(f"field 0x{ft:x} overran payload")
        out.append({"fieldType": ft, "value": v})
        i += 1
    return out, off


def lz4_decompress(src, dst_len):
    """LZ4 block decompression (mirrors FUN_141574a30)."""
    dst = bytearray()
    i = 0
    n = len(src)
    while i < n:
        token = src[i]
        i += 1
        lit = token >> 4
        if lit == 15:
            while True:
                if i >= n:
                    raise DecodeError("lz4: literal length overrun")
                b = src[i]
                i += 1
                lit += b
                if b != 255:
                    break
        if i + lit > n:
            raise DecodeError("lz4: literal overrun")
        dst += src[i:i + lit]
        i += lit
        if i >= n:
            break
        if i + 2 > n:
            raise DecodeError("lz4: offset overrun")
        offset = src[i] | (src[i + 1] << 8)
        i += 2
        if offset == 0 or offset > len(dst):
            raise DecodeError("lz4: bad offset")
        matchlen = token & 0xF
        if matchlen == 15:
            while True:
                if i >= n:
                    raise DecodeError("lz4: match length overrun")
                b = src[i]
                i += 1
                matchlen += b
                if b != 255:
                    break
        matchlen += 4
        start = len(dst) - offset
        for k in range(matchlen):
            dst.append(dst[start + k])
    if len(dst) != dst_len:
        raise DecodeError(f"lz4: got {len(dst)} want {dst_len}")
    return bytes(dst)


def deframe(data):
    """Decode the inbound frame container (FUN_140fe8ef0).

    Frame = [u16 compLen][u16 decodedLen][payload]. compLen == 0 means the
    payload is `decodedLen` raw bytes; otherwise the payload is an LZ4 block
    (`compLen` bytes) that expands to `decodedLen` bytes. Concatenated decoded
    payloads are the message stream Msg_DispatchStream parses.
    """
    out = bytearray()
    off = 0
    while off + 4 <= len(data):
        comp = struct.unpack_from("<H", data, off)[0]
        dec = struct.unpack_from("<H", data, off + 2)[0]
        size = comp if comp else dec
        if off + 4 + size > len(data):
            break
        payload = data[off + 4:off + 4 + size]
        out += payload if comp == 0 else lz4_decompress(payload, dec)
        off += 4 + size
    return bytes(out), off


def decode_stream(pe, id_map, data, limit=0x2000):
    """Decode a mode-3 message stream into messages."""
    chains = {}
    msgs = []
    off = 0
    end = len(data)
    while off < end:
        if off + 2 > end:
            msgs.append({"error": "truncated msgid", "off": off})
            break
        msgid = struct.unpack_from("<H", data, off)[0]
        ptr = id_map.get(msgid)
        if ptr is None:
            msgs.append({"error": "unknown msgid", "msgid": msgid, "off": off,
                         "raw": data[off:off + 16].hex()})
            break
        if msgid not in chains:
            chains[msgid] = read_chain(pe, int(ptr, 16))
        start = off
        try:
            fields, off = decode_fields(pe, chains[msgid], data, off, end)
        except DecodeError as e:
            msgs.append({"msgid": msgid, "error": str(e), "off": start,
                         "raw": data[start:start + 32].hex()})
            break
        if off - start > MSG_MAX_BUFFER_SIZE:
            msgs.append({"msgid": msgid, "error": "message exceeds 0x2000",
                         "off": start})
            break
        msgs.append({"msgid": msgid, "wireLength": off - start,
                     "fields": fields})
        if len(msgs) > limit:
            break
    return msgs


def synthetic_chain_0x264():
    return [
        {"fieldType": 1, "param": 0x264, "refTypeDef": 0},
        {"fieldType": 4, "param": 0, "refTypeDef": 0},
        {"fieldType": 2, "param": 0, "refTypeDef": 0},
        {"fieldType": 2, "param": 0, "refTypeDef": 0},
        {"fieldType": 4, "param": 0, "refTypeDef": 0},
    ]


def verify_fixture(path):
    """Verify a captured inbound fixture: RC4 wire bytes -> transport frame.

    Returns (errors, message stream). Needs no image/ids.
    """
    errors = []
    with open(path) as f:
        fx = json.load(f)
    ct = bytes.fromhex(fx["wireHex"])
    st = [int(fx["cipherState"]["i"], 16),
          int(fx["cipherState"]["j"], 16),
          list(bytes.fromhex(fx["cipherState"]["sboxHex"]))]
    frame = bytes(cipher.crypt_stream(st, ct))
    if frame != bytes.fromhex(fx["transportFrameHex"]):
        errors.append("fixture: RC4(wire) != transportFrame")
    stream, used = deframe(frame)
    if used != len(frame):
        errors.append("fixture: deframe did not consume the whole frame")
    if not stream:
        errors.append("fixture: empty message stream")
    return errors, stream


def selftest():
    errors = []
    chain = synthetic_chain_0x264()

    # A hand-built 0x264 frame: msgid 0x264, varint skill 0x1234, slot 2,
    # context 0, varint player 0x7F. Varint 0x1234 = B4 24 (LSB group first).
    frame = bytes.fromhex("6402" + "b424" + "02" + "00" + "7f")
    fields, off = decode_fields(None, chain, frame, 0, len(frame))
    got = [f["value"] for f in fields]
    if got != [0x264, 0x1234, 2, 0, 0x7F]:
        errors.append(f"0x264 decode wrong: {got}")
    if off != len(frame):
        errors.append(f"0x264 consumed {off} of {len(frame)}")

    # Truncation must fail, not silently succeed.
    for n in range(len(frame)):
        try:
            decode_fields(None, chain, frame[:n], 0, n)
            errors.append(f"truncated frame at {n} decoded without error")
        except DecodeError:
            pass

    # A 6-byte varint must fail (reader caps at 5).
    try:
        decode_fields(None, [chain[1]], b"\x80\x80\x80\x80\x80\x00", 0, 6)
        errors.append("over-long varint accepted")
    except DecodeError:
        pass

    # A little-endian varint must round-trip against the reader's own rule.
    for value in (0, 1, 0x7F, 0x80, 0x1234, 0x1FFFFF, 0xFFFFFFFF):
        enc = bytearray()
        v = value
        while True:
            b = v & 0x7F
            v >>= 7
            if v:
                enc.append(b | 0x80)
            else:
                enc.append(b)
                break
        dec, _ = _varint(bytes(enc), 0, len(enc))
        if dec != value:
            errors.append(f"varint round-trip {value:#x} -> {dec:#x}")

    # A serialized state (i u32, j u32, S[256]) must reload and continue
    # identically, which is what --state depends on.
    st1, _ = cipher.key_schedule(bytes(range(20)))
    cipher.crypt_stream(st1, bytes(range(37)))      # advance out of phase
    blob = struct.pack("<II", st1[0], st1[1]) + bytes(st1[2])
    st2 = [struct.unpack_from("<I", blob, 0)[0],
           struct.unpack_from("<I", blob, 4)[0], list(blob[8:8 + 256])]
    if cipher.crypt_stream(st1, bytes(48)) != cipher.crypt_stream(st2, bytes(48)):
        errors.append("serialized state did not reload identically")

    # 6. The frame container: raw frame, and a hand-built LZ4 block.
    raw_frame = bytes([0, 0, 3, 0, 0xAA, 0xBB, 0xCC])
    stream, used = deframe(raw_frame)
    if stream != bytes([0xAA, 0xBB, 0xCC]) or used != 7:
        errors.append(f"raw frame deframe wrong: {stream.hex()} used {used}")
    # LZ4 block for "AAAAA": token 0x10 (1 literal, match ext 0), literal 'A',
    # offset 1, match length 0+4.
    lz4_frame = bytes([4, 0, 5, 0, 0x10, 0x41, 0x01, 0x00])
    stream, _ = deframe(lz4_frame)
    if stream != b"AAAAA":
        errors.append(f"lz4 frame deframe wrong: {stream!r}")
    try:
        lz4_decompress(bytes([0x10, 0x41, 0x02, 0x00]), 5)
        errors.append("lz4 accepted an out-of-range offset")
    except DecodeError:
        pass

    # 7. The captured inbound fixture, if present: ciphertext -> frame -> 0x264.
    if os.path.exists(FIXTURE):
        ferr, stream = verify_fixture(FIXTURE)
        errors += ferr
        if not stream.startswith(bytes.fromhex("6402")):
            errors.append("fixture: message stream does not start with 0x264")

    return errors


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--decrypted", metavar="PATH", help="decrypted stream bytes")
    ap.add_argument("--capture", metavar="PATH", help="encrypted stream bytes")
    ap.add_argument("--key", metavar="HEX", help="20-byte RC4 key")
    ap.add_argument("--key-file", metavar="PATH",
                    help="file holding the 20-byte key (x64dbg conn118.bin)")
    ap.add_argument("--state", metavar="PATH",
                    help="0x108-byte PRGA state (i u32, j u32, S[256]) captured at "
                         "conn+0x12C; decrypts a mid-session capture with no KSA")
    ap.add_argument("--out", metavar="PATH",
                    help="write the decrypted stream bytes here")
    ap.add_argument("--deframe", action="store_true",
                    help="decode the inbound frame container (LZ4) before parsing")
    ap.add_argument("--ids", metavar="CSV",
                    default=os.path.join("protocol", "schema", "207032",
                                         "sweep2.csv"))
    ap.add_argument("--image", metavar="PATH", default=IMAGE)
    args = ap.parse_args()

    if args.selftest:
        errors = selftest()
        for e in errors:
            print("FAIL", e)
        if errors:
            return 1
        print("OK Gw2TransportDecode selftest (7 groups)")
        return 0

    if not (args.decrypted or args.capture):
        ap.print_help()
        return 0

    if args.capture:
        with open(args.capture, "rb") as f:
            enc = f.read()
        if args.state:
            raw = open(args.state, "rb").read()
            if len(raw) < 0x108:
                print("error: --state must be 0x108 bytes (i,j,S[256])")
                return 2
            i = struct.unpack_from("<I", raw, 0)[0]
            j = struct.unpack_from("<I", raw, 4)[0]
            state = [i, j, list(raw[8:8 + 256])]
        else:
            key = None
            if args.key:
                key = bytes.fromhex(args.key)
            elif args.key_file:
                with open(args.key_file, "rb") as f:
                    key = f.read()[:20]
            if not key or len(key) != 20:
                print("error: --capture needs --state, a 20-byte --key, or --key-file")
                return 2
            state, _ = cipher.key_schedule(key)
        data = cipher.crypt_stream(state, enc)
    else:
        with open(args.decrypted, "rb") as f:
            data = f.read()

    if args.deframe:
        data, _ = deframe(data)

    if args.out:
        with open(args.out, "wb") as f:
            f.write(data)
        print(f"wrote {len(data)} bytes to {args.out}")
        return 0

    pe = PE(args.image)
    id_map = load_id_map(args.ids)
    msgs = decode_stream(pe, id_map, data)
    print(json.dumps({"messages": msgs}, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
