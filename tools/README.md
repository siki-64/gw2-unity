# Reverse-engineering tools

Offline, repository-local tooling for the contract-to-client workflow in
[`../docs/workflow.md`](../docs/workflow.md). Nothing here touches a live process, injects into the game,
or produces a wire codec.

## What was checked, reused and added

Per `research/contracts/methodology/tooling.md`, before adding tooling:

| Capability | Where it lives | Decision |
| --- | --- | --- |
| Decompilation, xrefs, call graphs, strings, types, structure synthesis, Version Tracking | **Ghidra** (plus Ghidra MCP for agent navigation) | Reused. Not wrapped, not duplicated. |
| vtable recovery from constructor anchors, assert-string anchors, master dispatch-table inspection | **Ghidra** workflow in `ghidra.md` / `tooling.md` | Reused. The one repeated step that Ghidra does not expose to an agent as data is exporting the assert `D:\Perforce\...` anchors and vtable candidate seeds as JSON; that is the only script here. |
| Offline PE/disassembly inspection | Ghidra/MCP | Reused. No disassembler is shipped here. |
| Archive/texture/schema decoding | `research/data/Gw2.Dat` (its own tests) | Reused unchanged. |
| Byte-level opcode/schema probing against a live process | external debugger (e.g. kx-packet-inspector's Cheat Engine Lua) | Not reimplemented. See *Live inspection* below. |
| Message catalog, catalog validation, catalog build-stamp, evidence bundle integrity | this directory | **Added** as one operator wrapper `gw2re.ps1`. It is the thin generator/consumer the methodology permits: it produces and checks maintained repo artifacts only. |

## Layout

| Path | Purpose |
| --- | --- |
| `gw2re.ps1` | Single operator wrapper. Every command takes explicit input/output paths and stamps the game build into its output. |
| `ghidra/export_anchors.py` | Ghidra script (Ghidra 10/11, Jython). Reads the analyzed image in-process and writes the anchor/vtable seed JSON that the catalog stamp consumes. |
| `schema/catalog.schema.json` | JSON Schema for `protocol/catalog.json` and each `protocol/messages/<build>/<id>.json`. |
| `pipelines/taxonomy.json` | Decoder-pipeline identifiers. The catalog records only pipeline ids; defaults step order is documentation, not a claim until a build-specific pipeline documents it. |
| `README.md` | This file. |

Generated or reviewed outputs are checked in; private inputs (captures, dumps, exported JSON) are not.
`artifacts/re-tooling/` and `captures/local/` are ignored by Git.

## Commands

Run `gw2re.ps1 <area> <action>` from the repository root. Every command prints `info`/`warning`/`error`
lines and exits non-zero on any error. Add `-Json` for one machine-readable result object instead, but not
together with warning escalation. `-Strict` turns warnings into a non-zero exit.

```powershell
# Catalog ---------------------------------------------------------------------------------

# Validate the catalog, the template and every referenced message artifact
.\tools\gw2re.ps1 catalog validate

# Stamp a build whose in-memory image you observed. Manual: nothing is registered automatically.
.\tools\gw2re.ps1 catalog stamp -Build 205780 -BuildLabel 205.780 -ImageSha256 <64-hex> -StampNote "MumbleLink build"

# Evidence --------------------------------------------------------------------------------

# Import a private bundle (capture directory or a single file), recording build, hashes and timestamps
.\tools\gw2re.ps1 evidence import -Path .\captures\local\205780 -Build 205780 `
    -BuildLabel 205.780 -Manifest .\artifacts\manifests\205780.json `
    -Process gw2-64.exe -RuntimeBase 0x7FF600000000

# Re-hash every manifest entry against the bytes on disk
.\tools\gw2re.ps1 evidence verify -Manifest .\artifacts\manifests\205780.json

# Which bundle produced this file, and which artifacts cite its hash?
.\tools\gw2re.ps1 evidence provenance -Path .\captures\local\205780\gw2-64.memdump

# Findings --------------------------------------------------------------------------------

# Check that 64-hex record hashes quoted in the reviewed findings resolve to a manifest record
.\tools\gw2re.ps1 notes crosscheck -Manifest .\research\provenance\manifest.json
```

| Parameter | Default | Used by |
| --- | --- | --- |
| `-Catalog` | `protocol/catalog.json` | `catalog validate`, `catalog stamp` |
| `-Schema` | `tools/schema/catalog.schema.json` | `catalog validate` |
| `-Build` | required | `catalog stamp`, `evidence import` |
| `-Manifest` | required for evidence, `research/provenance/manifest.json` for notes | all `evidence`, `notes` |
| `-NotesRoot` | `research/contracts/notes` | `notes crosscheck` |
| `-Exclude` | `artifacts/manifests/**`, `artifacts/re-tooling/**` | `evidence import` |
| `-WireEligible` | off | `evidence import`, payloads only |

`catalog validate -CheckGeneratedSchema` additionally proves the hand-written schema still resolves the
definitions the tooling relies on.

## Evidence rules the tools enforce

- **No copies of native memory.** `evidence import` never records a dump as wire bytes. `--wire-eligible`
  on `asset`/`record` entries is only accepted when the representation is `wire-bytes`.
- **No cross-build identifiers.** Every catalog entry carries `build` plus `buildLabel`/`imageSha256`
  where known. A message id is never carried into another build: a new build gets new entries.
- **Unknown fields survive.** Ciphertext, compressed payloads and unparsed tails are recorded as
  `opaque` / `unknown` fields with raw hex preserved, never dropped.
- **No live-server claims.** `validation.liveInterop` stays false unless a controlled interoperability run
  was performed; synthetic and replay results are recorded separately.
- **Anonymous, hashed sources.** `sourceReferences` record a kind, a label and a SHA-256, not a raw capture
  path, so no capture content or filename leaves the ignored workspace.

## Ghidra anchor export

`tools/ghidra/export_anchors.py` reads the active Ghidra program in-process and writes one JSON file with:

- the image identity (program name, executable MD5/SHA-256, language, compiler spec, image base, span);
- assert/source-path anchors, from the `...\Code\...*.cpp` strings described in
  `research/contracts/methodology/ghidra.md`;
- vtable candidates: runs of three or more consecutive pointer slots whose targets are functions.

It does not rename, retype, comment or analyse anything, and it decides no class semantics. Treat the
result as a lead list. Absolute addresses stay coordinates inside one analysed build; record the export as
an `image-anchor-export` source reference in the artifact whose evidence it supports.

```
Window > Script Manager > Run            (or)
analyzeHeadless <proj> <name> -process <program> -postScript export_anchors.py <outPath>
```

## Tracked versus local

| Artifact | Location | Tracked |
| --- | --- | --- |
| Catalog, template, schemas, taxonomy | `protocol/`, `tools/` | yes |
| Message artifacts (`protocol/messages/<build>/<id>.json`) | `protocol/` | yes |
| Anchor exports | `artifacts/re-tooling/` | no |
| Evidence bundle manifests | `artifacts/manifests/` | no |
| Private captures, module snapshots, session material | `captures/local/` | no |
| Ghidra project, `.rep`, `.gbf` | anywhere local | no |

## Live inspection

Live tracing and byte-level schema probing stay in a debugger or an external inspector, because that is
where watchpoints, hardware breakpoints and interactive stack walking already work. The relevant external
reference is [`kxtools/kx-packet-inspector`](https://github.com/kxtools/kx-packet-inspector) (MIT): a
D3D11/MinHook overlay that hooks the client's send buffer and message dispatcher, decodes the schema table
and logs opcode/handler/schema triples. It is useful as an *external* cross-check for build-sensitive
addresses; nothing from it is vendored, and none of its addresses are durable identities here.

When this repository needs a live anchor, the supported route is the `ContextCollection` anchor and
intra-object offsets described in `research/contracts/methodology/validation.md`, not an image coordinate.

## Not implemented here, on purpose

- No x64 disassembler, PE parser or relocation engine: Ghidra owns decoded inspection.
- No scanner, AOB matcher or locator library: reusable locators belong in the discovery layer once one
  exists, and every image-coordinate case here would duplicate Ghidra byte search.
- No injector, hook engine, overlay or renderer: those are tied to the official client's in-process
  execution, which `docs/workflow.md` keeps out of this repository.
- No live byte probing: that needs watchpoints and hardware breakpoints, which an offline PowerShell
  wrapper cannot provide.
- No automatic build registration: `catalog stamp` records a build you identified, and
  `supportedWireBuilds` stays empty until a capture-backed codec exists.
