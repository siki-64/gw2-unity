# Exports reviewed analysis anchors from the active Ghidra program as JSON, so the catalog and the
# findings notes can cite an anchor export by SHA-256 instead of pasting addresses.
#
# Ownership (research/contracts/methodology/ghidra.md):
#   Ghidra owns analysis state. This script does not rename, retype, comment or analyse anything.
#   It reads in-process and writes one file outside the Ghidra project. Absolute VAs stay
#   build-local coordinates; the export records the image identity alongside them and the catalog
#   never treats a VA as a durable identity.
#
# Run inside Ghidra:  Window > Script Manager > Run (Jython), or
#   analyzeHeadless <proj> <name> -process <program> -postScript export_anchors.py <outPath>
#
# The result is recorded as an `image-anchor-export` source reference.

# @category GW2
# @runtime Jython

import json
import os
import re

from java.io import File

# Anchors worth exporting: assert/source-path strings and the constructor-anchor vtable pattern.
# Everything else stays in the Ghidra project, because duplicating Ghidra's navigation surface in a
# repository file is exactly what methodology/tooling.md forbids.
SOURCE_PATH_RE = re.compile(r"(?:0)?[A-Za-z]:\\.*?\\Code\\.*?\.(?:c|cpp|h|hpp)$", re.IGNORECASE)
MAX_STRING_HITS = 4000
MAX_VTABLE_CANDIDATES = 4000


def _default_output_path():
    base = os.path.dirname(os.path.abspath(__file__))
    return os.path.join(base, currentProgram.getName() + ".anchor-export.json")


def _image_identity():
    memory = currentProgram.getMemory()
    exe_path = currentProgram.getExecutablePath()
    return {
        "programName": currentProgram.getName(),
        "executablePath": exe_path if exe_path else None,
        "executableMd5": currentProgram.getExecutableMD5() or None,
        "executableSha256": currentProgram.getExecutableSHA256() or None,
        "languageId": currentProgram.getLanguageID().toString(),
        "compilerSpec": currentProgram.getCompilerSpec().getCompilerSpecID().toString(),
        "imageBase": str(currentProgram.getImageBase()),
        "minAddress": str(memory.getMinAddress()),
        "maxAddress": str(memory.getMaxAddress()),
        "ghidraVersion": str(getGhidraVersion()),
    }


def _string_anchors():
    """Assert/source-file anchors. These are identity leads, never semantics."""
    anchors = []
    listing = currentProgram.getListing()
    iterator = listing.getDefinedData(True)
    while iterator.hasNext() and len(anchors) < MAX_STRING_HITS:
        data = iterator.next()
        try:
            value = str(data.getValue())
        except Exception:
            continue
        if "\\Code\\" not in value and "\\Code/" not in value:
            continue
        if not SOURCE_PATH_RE.search(value):
            continue
        anchors.append({
            "address": str(data.getAddress()),
            "value": value,
            "byteLength": data.getLength(),
        })
    return anchors


def _vtable_candidates():
    """
    Runs of three or more consecutive pointer slots whose targets are functions.

    This is only a seed list. It deliberately does not decide what class a candidate belongs to:
    candidate identification stays a manual Ghidra step per methodology/tooling.md.
    """
    candidates = []
    memory = currentProgram.getMemory()
    function_manager = currentProgram.getFunctionManager()
    pointer_size = currentProgram.getDefaultPointerSize()

    for block in memory.getBlocks():
        if not block.isInitialized() or block.isExecute():
            continue
        start = block.getStart()
        end = block.getEnd()
        address = start
        run_start = None
        run_length = 0
        while address.compareTo(end) <= 0:
            target = None
            try:
                target = memory.getLong(address)
            except Exception:
                target = None
            is_function_slot = False
            if target:
                try:
                    is_function_slot = function_manager.getFunctionAt(toAddr(target)) is not None
                except Exception:
                    is_function_slot = False
            if is_function_slot:
                if run_start is None:
                    run_start = address
                run_length += 1
            else:
                if run_length >= 3 and len(candidates) < MAX_VTABLE_CANDIDATES:
                    candidates.append({
                        "address": str(run_start),
                        "slotCount": run_length,
                        "section": block.getName(),
                    })
                run_start = None
                run_length = 0
            try:
                address = address.add(pointer_size)
            except Exception:
                break
        if run_length >= 3 and len(candidates) < MAX_VTABLE_CANDIDATES:
            candidates.append({
                "address": str(run_start),
                "slotCount": run_length,
                "section": block.getName(),
            })
    return candidates


def main():
    args = getScriptArgs()
    output_path = args[0] if len(args) > 0 else _default_output_path()
    output_file = File(os.path.abspath(output_path))

    payload = {
        "schemaVersion": 1,
        "kind": "image-anchor-export",
        "producedBy": "tools/ghidra/export_anchors.py",
        "classification": "build-local analysis coordinates; not durable identities",
        "image": _image_identity(),
        "sourcePathAnchors": _string_anchors(),
        "vtableCandidates": _vtable_candidates(),
        "note": (
            "Absolute addresses are coordinates inside one analysed build. Do not copy them into "
            "runtime code and do not reuse them for another build."
        ),
    }

    handle = open(output_file.getAbsolutePath(), "w")
    try:
        json.dump(payload, handle, indent=2)
        handle.write("\n")
    finally:
        handle.close()

    print("[gw2re] wrote anchor export to " + output_file.getAbsolutePath())
    print("[gw2re] source path anchors: %d, vtable candidates: %d" % (
        len(payload["sourcePathAnchors"]), len(payload["vtableCandidates"])))


main()
