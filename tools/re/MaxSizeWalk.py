#!/usr/bin/env python3
"""Compute MsgPack defSize/maxSize per defArray chain across the 205.780 corpus.

Reads Gw2-64.exe directly (PE VA->file mapping), walks every descriptor chain
referenced by protocol/schema/205780/sweep2.csv, and computes per chain:

  maxSize  exactly as MsgPack_ComputeMaxSize (140fe98b0) does;
  defSize  exactly as MsgPack_ComputeDefSize (140fe9830) does;
  ok       maxSize <= MSG_MAX_BUFFER_SIZE (0x2000), the validator's bound.

Read-only: never writes to the image. Run offline from the repository root.

Output columns:
  ptr,ids,maxSize,defSize,fieldCount,fields,ok

where ids is a semicolon-joined list of the message ids that reference this
chain (in sweep2.csv), fields is a comma-joined list of fieldType hex values
(including the terminal marker), and ok is True iff maxSize <= 0x2000.

The defSize column is the size of the decoded struct the reader writes
(MsgPack_ReadFields output), i.e. the sum of the size-table entry per field.
It is not the wire length; maxSize is the worst-case wire length.
"""

import csv
import os
import struct
import sys

IMAGE = r"C:\Program Files (x86)\Steam\steamapps\common\Guild Wars 2\Gw2-64.exe"
CORPUS = os.path.join(os.path.dirname(__file__), "..", "..", "protocol",
                      "schema", "205780", "sweep2.csv")
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "protocol",
                   "schema", "205780", "maxsize.csv")

MSG_MAX_BUFFER_SIZE = 0x2000
SIZE_TABLE_VA = 0x142109500
DESC_STRIDE = 0x28


class PE:
    def __init__(self, path):
        with open(path, "rb") as f:
            self.blob = f.read()
        dos = struct.unpack_from("<I", self.blob, 0x3C)[0]
        assert self.blob[dos:dos + 4] == b"PE\x00\x00", "not a PE"
        nsec = struct.unpack_from("<H", self.blob, dos + 6)[0]
        opt_size = struct.unpack_from("<H", self.blob, dos + 20)[0]
        opt = dos + 24
        magic = struct.unpack_from("<H", self.blob, opt)[0]
        if magic == 0x10B:  # PE32
            self.image_base = struct.unpack_from("<I", self.blob, opt + 28)[0]
        else:  # PE32+
            self.image_base = struct.unpack_from("<Q", self.blob, opt + 24)[0]
        sec_base = opt + opt_size
        self.sections = []
        for i in range(nsec):
            off = sec_base + i * 40
            name = self.blob[off:off + 8].rstrip(b"\x00").decode("latin1")
            vaddr, vsize, rawptr, rawsize = struct.unpack_from("<IIII",
                                                               self.blob,
                                                               off + 8)
            # PE section header fields at off+8: VirtualSize, VirtualAddress,
            # SizeOfRawData, PointerToRawData -- swap to (vsize, vaddr, ...).
            vsize, vaddr, rawsize, rawptr = vaddr, vsize, rawptr, rawsize
            self.sections.append((name, vaddr, vsize, rawptr, rawsize))

    def read_va(self, va, size):
        rva = va - self.image_base
        for name, vaddr, vsize, rawptr, rawsize in self.sections:
            if vaddr <= rva < vaddr + max(vsize, rawsize):
                off = rawptr + (rva - vaddr)
                return self.blob[off:off + size]
        raise KeyError(f"VA 0x{va:x} not in any section")

    def read_u8(self, va):
        return self.read_va(va, 1)[0]

    def read_u16(self, va):
        return struct.unpack_from("<H", self.read_va(va, 2))[0]

    def read_u32(self, va):
        return struct.unpack_from("<I", self.read_va(va, 4))[0]

    def read_u64(self, va):
        return struct.unpack_from("<Q", self.read_va(va, 8))[0]


def main():
    pe = PE(IMAGE)
    # Size table: 27 entries of {size:dword, flags:dword} indexed by fieldType*8.
    # Verified byte-for-byte against the binary at 0x142109500 (indices 0x00..0x1a).
    # flags != 0 means a fixed-size scalar (size is its output size); flags == 0
    # means a composite whose maxSize is computed by MsgPack_ComputeMaxSize.
    # The earlier note transcription of this table is inconsistent with these bytes.
    size = []
    flags = []
    for i in range(27):
        base = SIZE_TABLE_VA + i * 8
        size.append(pe.read_u32(base))
        flags.append(pe.read_u32(base + 4))
    print("size table:", size)
    print("flags table:", flags)

    rows = list(csv.DictReader(open(CORPUS, newline="")))
    seen = {}
    for r in rows:
        if r["tag"] == "OK" and r["ptr"]:
            seen.setdefault(r["ptr"], set()).add(r["id"])

    results = []
    violations = 0
    for ptr, ids in sorted(seen.items()):
        va = int(ptr, 16)
        fields = []
        total = 0
        defsize = 0
        guard = 0
        while True:
            guard += 1
            if guard > 4096:
                raise RuntimeError(f"unbounded chain at 0x{va:x}")
            ft = pe.read_u32(va)
            fields.append(ft)
            if ft == 0 or ft == 0x18:
                break
            # ComputeDefSize sums the size-table entry of every field; the
            # recursion it performs only fills the nested defSize cache.
            defsize += size[ft]
            if flags[ft] != 0:
                total += size[ft]
            else:
                param = pe.read_u32(va + 0x10)
                ref = pe.read_u64(va + 0x18)
                if ft == 4:
                    total += 5
                elif ft == 10:
                    total += 0x11
                elif ft in (0xD, 0xE, 0x13):
                    total += param + 8
                elif ft == 0xF:
                    total += _maxsize(pe, size, flags, ref) + 8
                elif ft == 0x10:
                    total += _maxsize(pe, size, flags, ref) * param + 8
                elif ft == 0x11:
                    total += _maxsize(pe, size, flags, ref) * param + 9
                elif ft == 0x12:
                    total += _maxsize(pe, size, flags, ref) * param + 10
                elif ft == 0x14:
                    total += pe.read_u16(va + 0x10) + 9
                elif ft == 0x15:
                    total += pe.read_u16(va + 0x10) + 10
                elif ft == 0x16:
                    pass  # MP_SRV_ALIGN: no data
            va += DESC_STRIDE
        ok = total <= MSG_MAX_BUFFER_SIZE
        if not ok:
            violations += 1
        results.append((ptr, ";".join(sorted(ids, key=lambda x: int(x, 16))),
                        total, defsize, len(fields), ",".join(
                            f"{x:x}" for x in fields), ok))

    with open(OUT, "w", newline="") as f:
        w = csv.writer(f)
        w.writerow(["ptr", "ids", "maxSize", "defSize", "fieldCount",
                    "fields", "ok"])
        w.writerows(results)

    ok_count = sum(1 for x in results if x[6])
    print(f"chains: {len(results)}  ok<=0x2000: {ok_count}  "
          f"violations: {violations}")
    for r in results:
        if not r[6]:
            print("  VIOLATION", r)
    return 0 if violations == 0 else 1


def _maxsize(pe, size, flags, va):
    total = 0
    guard = 0
    while True:
        guard += 1
        if guard > 4096:
            raise RuntimeError(f"unbounded ref chain at 0x{va:x}")
        ft = pe.read_u32(va)
        if ft == 0 or ft == 0x18:
            return total
        if flags[ft] != 0:
            total += size[ft]
        else:
            param = pe.read_u32(va + 0x10)
            ref = pe.read_u64(va + 0x18)
            if ft == 4:
                total += 5
            elif ft == 10:
                total += 0x11
            elif ft in (0xD, 0xE, 0x13):
                total += param + 8
            elif ft == 0xF:
                total += _maxsize(pe, size, flags, ref) + 8
            elif ft == 0x10:
                total += _maxsize(pe, size, flags, ref) * param + 8
            elif ft == 0x11:
                total += _maxsize(pe, size, flags, ref) * param + 9
            elif ft == 0x12:
                total += _maxsize(pe, size, flags, ref) * param + 10
            elif ft == 0x14:
                total += pe.read_u16(va + 0x10) + 9
            elif ft == 0x15:
                total += pe.read_u16(va + 0x10) + 10
            elif ft == 0x16:
                pass
        va += DESC_STRIDE


if __name__ == "__main__":
    sys.exit(main())