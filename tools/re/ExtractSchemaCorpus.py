#!/usr/bin/env python3
"""Emit a build 205.780 message-schema corpus (recv or send) as JSON.

Inbound and outbound have different chains per id (msg-dispatch Addendum 26), so
each direction gets its own corpus:

  recv  every id in the live per-connection recv registry map
        (protocol/schema/205780/live_ids.csv);
  send  the flat table-A entries across the TablePair / Bidirectional
        registration sites (registrars3.csv `countA` fixes how many leading
        col-0 rows of each site belong to table A; sweep2.csv lists them).

Each id's MsgPackFieldDef chain is read from Gw2-64.exe, including nested
refTypeDef chains:

  {
    "build": 205780,
    "direction": "recv",
    "messages": { "0x264": [ {"t":1,"p":612}, {"t":4}, ... ] }
  }

Field keys: t = fieldType, p = param, r = nested refTypeDef chain. The terminal
marker (fieldType 0 / 0x18) is not emitted.

Read-only. Addresses are build-local; the output is build-stamped so a consumer
cannot carry it to another build.

Usage
  python tools/re/ExtractSchemaCorpus.py --direction recv
  python tools/re/ExtractSchemaCorpus.py --direction send
"""

import argparse
import csv
import json
import os
import struct
import sys
from collections import defaultdict

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
IMAGE = r"C:\Program Files (x86)\Steam\steamapps\common\Guild Wars 2\Gw2-64.exe"
SCHEMA_DIR = os.path.join(ROOT, "protocol", "schema", "205780")
DEFAULT_IDS = os.path.join(SCHEMA_DIR, "live_ids.csv")
DEFAULT_SWEEP = os.path.join(SCHEMA_DIR, "sweep2.csv")
DEFAULT_REGISTRARS = os.path.join(SCHEMA_DIR, "registrars3.csv")

DESC_STRIDE = 0x28
MAX_DEPTH = 16
PAIR_REGISTRARS = ("RegisterTablePair", "RegisterBidirectional")


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
        if magic == 0x10B:
            self.image_base = struct.unpack_from("<I", self.blob, opt + 28)[0]
        else:
            self.image_base = struct.unpack_from("<Q", self.blob, opt + 24)[0]
        sec_base = opt + opt_size
        self.sections = []
        for i in range(nsec):
            off = sec_base + i * 40
            vsize, vaddr, rawsize, rawptr = struct.unpack_from("<IIII", self.blob, off + 8)
            self.sections.append((vaddr, vsize, rawptr, rawsize))

    def read_va(self, va, size):
        rva = va - self.image_base
        for vaddr, vsize, rawptr, rawsize in self.sections:
            if vaddr <= rva < vaddr + max(vsize, rawsize):
                off = rawptr + (rva - vaddr)
                return self.blob[off:off + size]
        raise KeyError(f"VA 0x{va:x} not mapped")

    def u32(self, va):
        return struct.unpack_from("<I", self.read_va(va, 4))[0]

    def u64(self, va):
        return struct.unpack_from("<Q", self.read_va(va, 8))[0]


def read_chain(pe, va, depth, active):
    """Read a field chain. Returns a list of {t,p,r} dicts (terminal excluded)."""
    fields = []
    for _ in range(4096):
        ft = pe.u32(va)
        if ft == 0 or ft == 0x18:
            break
        field = {"t": ft}
        param = pe.u32(va + 0x10)
        if param:
            field["p"] = param
        ref = pe.u64(va + 0x18)
        if ref and depth < MAX_DEPTH and ref not in active:
            try:
                active.add(ref)
                nested = read_chain(pe, ref, depth + 1, active)
                active.discard(ref)
                if nested:
                    field["r"] = nested
            except KeyError:
                active.discard(ref)
        fields.append(field)
        va += DESC_STRIDE
    return fields


def recv_id_map(ids_path):
    m = {}
    with open(ids_path, newline="") as f:
        for r in csv.DictReader(f):
            if r.get("tag") == "OK" and r.get("ptr"):
                m.setdefault(int(r["id"], 16), r["ptr"])
    return m


def send_id_map(registrars_path, sweep_path):
    counts = {}
    with open(registrars_path, newline="") as f:
        for r in csv.DictReader(f):
            if r["registrar"] in PAIR_REGISTRARS and (r.get("countA") or "").isdigit():
                counts[r["callAddr"]] = int(r["countA"])

    by_site = defaultdict(list)
    with open(sweep_path, newline="") as f:
        for r in csv.DictReader(f):
            by_site[r["callAddr"]].append(r)

    m = {}
    for site, rows in by_site.items():
        n = counts.get(site)
        if n is None:
            continue  # RecvOnly / unknown: no send table
        col0 = [r for r in rows if r["col"] == "0"]
        for r in col0[:n]:  # table A is emitted before table B
            if r["tag"] == "OK" and r["ptr"]:
                m.setdefault(int(r["id"], 16), r["ptr"])
    return m


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--direction", choices=["recv", "send"], default="recv")
    ap.add_argument("--image", default=IMAGE)
    ap.add_argument("--ids", default=DEFAULT_IDS)
    ap.add_argument("--sweep", default=DEFAULT_SWEEP)
    ap.add_argument("--registrars", default=DEFAULT_REGISTRARS)
    ap.add_argument("--out", default=None)
    args = ap.parse_args()

    if args.direction == "recv":
        id_map = recv_id_map(args.ids)
        source = "protocol/schema/205780/live_ids.csv (live game-connection recv registry)"
    else:
        id_map = send_id_map(args.registrars, args.sweep)
        source = ("protocol/schema/205780/sweep2.csv table A (flat send tables) split by "
                  "registrars3.csv countA across TablePair/Bidirectional sites")
    out = args.out or os.path.join(SCHEMA_DIR, f"chains-{args.direction}.json")

    pe = PE(args.image)
    messages = {}
    unreadable = 0
    for mid in sorted(id_map):
        try:
            messages[f"0x{mid:x}"] = read_chain(pe, int(id_map[mid], 16), 0, set())
        except KeyError:
            unreadable += 1
            continue

    body = {
        "build": 205780,
        "buildLabel": "205.780",
        "direction": args.direction,
        "imageSha256": "d2ae84876a0b93277fccb368969046b848bb0403fd09db420389c813d2459b23",
        "source": source,
        "fieldKeys": {"t": "fieldType", "p": "param", "r": "nested refTypeDef chain"},
        "provenance": "Generated by tools/re/ExtractSchemaCorpus.py (msg-dispatch Addendum 8 offsets).",
        "messages": messages,
    }
    with open(out, "w", newline="\n") as f:
        json.dump(body, f, indent=1, sort_keys=True)
        f.write("\n")
    print(f"wrote {out}: {len(messages)} messages, {os.path.getsize(out)} bytes, "
          f"{unreadable} unreadable chains")


if __name__ == "__main__":
    sys.exit(main())
