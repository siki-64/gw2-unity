# Reverse-engineering tooling

This document owns the practical tool/cache workflow used to re-find and inspect native GW2 code and data.

General evidence/promotion rules live in [`reconstruction.md`](reconstruction.md).

# Before creating tooling

Before creating or modifying an RE scanner, reader, debugger helper, or analysis script:

1. inspect the existing operator tooling;
2. reuse an existing command if it already supports the task;
3. otherwise extend the closest existing tool;
4. create a new tool only when the existing cache/tooling architecture cannot reasonably support it.

Prefer maintained technologies compatible with the repository's Windows 11 / `.NET 10` environment.

# Evidence and tool hierarchy

Use the narrowest tool that answers the question while preserving the repository's evidence standard.

1. **Live debugger / runtime observation** is the strongest evidence for actual execution, object state, dispatch, and mutation.
2. **Decoded opcode/disassembly and direct memory inspection** are the ground truth for static instruction semantics and exact layout/encoding questions.
3. **Ghidra analysis state** is the preferred navigation and synthesis layer for functions, xrefs, call graphs, strings, types, and cross-function structure.
4. **Ghidra decompiler output** is derived evidence. It is excellent for accelerating reconstruction and forming hypotheses, but reconstructed C/C++ semantics, variable names, inferred types, and control-flow simplifications must not outrank the underlying instructions or runtime behavior.
5. **Model inference** is provisional until supported by one of the evidence layers above.

For normal decomp work, start in Ghidra/MCP to find the relevant surface, then validate nontrivial source changes against instructions and use live debugging when static evidence is ambiguous. Do not re-read large raw binary regions when Ghidra can answer the navigation question directly, and do not keep a duplicate repository address cache beside Ghidra.

# Command convention

Build-sensitive operations must identify the game build they were performed against, so that captured
output stays tied to the binary it describes. Where an operation reads a running process it must also
name the target process, and where structured output is useful it should emit a single JSON object
containing the build, optional process id, normalized command, exit code, stdout lines, and stderr
lines. Text remains the human default.

Operations separate by risk and semantics:

- read-only live-process and debugger inspection;
- game-specific interpretation and readers;
- mutation and validation operations, kept visibly separate from the read-only surface.

Offline PE/disassembly inspection belongs in Ghidra/MCP rather than in a live-instrumentation surface.

The command surface is an operator-facing shell, not the canonical home of reusable native knowledge:

- layouts, enums, and shared access contracts belong in the reconstructed contract layer;
- scanners, instruction analysis, and reusable locators belong in the discovery layer;
- mutation operations, sites, and relocation mechanics belong in their own dedicated layer;
- tool-only process attachment, capture orchestration, formatting, and dump provenance validation remain
  in the tool layer.

Tools must not depend on a runtime feature layer merely to reuse a reader or native contract. Move that
contract to its owning lower layer instead.

# Ghidra workspace and dump evidence

Ghidra is the authoritative store for active binary-analysis state: symbols, namespaces, comments,
types, xrefs, signatures, and build-specific addresses. See [`ghidra.md`](ghidra.md).

Retired flat symbol catalogs and their CLI validator/editor have been removed. Do not create a second
manually maintained address map beside Ghidra. Build-specific analysis addresses belong in the active
Ghidra project; reusable locators remain code in the discovery layer.

Runtime/offline locators must validate the image they operate on rather than trusting a Ghidra VA.
Ghidra addresses are analysis coordinates for one build, not runtime ASLR-adjusted addresses.

Mapped-module dumps are local evidence and are ignored by Git. A module dump is written atomically, with
the supplied build recorded in an adjacent `.build.txt` sidecar and a JSON manifest giving the build,
runtime base, image size, SHA-256, and capture time. Generated dump sidecars, manifests, temporary files,
and memory snapshots are local artifacts and are ignored as well. Do not use a dump when its provenance
is absent, unknown, or inconsistent with the task.

# Rules of thumb for live instrumentation

- Prefer setters and rare-event functions over per-frame evaluators.
- If a hook produces repeated identical samples in a tight burst, stop instrumenting the hot path and inspect the captured object/state directly.
- Avoid instrumenting extremely hot functions unless the information cannot reasonably be recovered elsewhere.
- Prefer a narrow hook at a meaningful state transition over a broad callback shared by unrelated UI or gameplay systems.
- Do not place general software tracepoints in `sub_AB5BC0` or the `sub_AB7890` shader-input loop; use
  an upstream material-specific producer or non-debugger capture instead.

## PvP state observation

PvP gear changes are observed by polling the same live object graph used by the
runtime accessors. The runtime resolves and publishes the `ContextCollection`
anchor once during initialization. A standalone inspection resolves an
equivalent anchor because it runs outside that process, then walks
`ChCliContext.Players` and compares each player's `ChCliPlayer.PvpGearManager`
payload. It does not install a breakpoint or depend on a build-local code
address.

The output distinguishes provider creation, field changes, and provider removal.
An absent provider is preserved as “not populated”; the inspector must not call
the native ensure/allocate routine to manufacture one.

# Address lifetime

- Do not treat absolute VAs as durable identities.
- Runtime access uses ContextCollection anchors and Native intra-object offsets. Do not promote image
  coordinates into reusable tooling contracts; use signatures, strings, relationships, or live anchors.
- Heap pointers are runtime-only and must not be carried across process restarts.
- If the game build changes, treat existing module snapshots and Ghidra-derived addresses as stale until revalidated.

The durable identity of a symbol is its evidence-backed semantic/structural identity, not an old build address.

# Module snapshot

Capture the main module in mapped-memory layout with a module-dump operation, which replaces the dump
atomically and records the associated build.

# Native font capture

For the build-205.780 font investigation, a read-only font capture takes a live operation-scoped
`GrFont` using the absolute `font=0x...` value recorded by the native text probe.

The capture writes a timestamped directory with a top-level `font.json`, per-range metadata,
glyph-record fields, and the decoded-length-bounded encoded stream for each loaded range. The pointer is
not durable: the capture must run against the same process that produced the probe address. The dump is
used to validate the native range format and metric payload before any runtime code is written.

Offline image commands should operate against either an installed PE image or the mapped-memory dump, depending on what the specific resolver requires.

# Finding a class vtable without ArenaNet RTTI

ArenaNet gameplay/UI classes generally do not expose useful native RTTI.

A practical constructor-anchor workflow is:

1. find a distinctive string associated with the compilation unit, such as an assert `.cpp` filename;
2. use Ghidra data references to locate code referencing that unit;
3. inspect pointer tables and their function targets in Ghidra to identify plausible vtables;
4. inspect Ghidra xrefs on the candidate;
5. verify the constructor/destructor pattern, e.g.:
   ```text
   lea rax,[vtable]
   mov [rcx],rax
   ```

Do not infer class semantics from the vtable location alone.

# Instruction layer

General x64 decoding and relocation go through a single shared instruction layer backed by an
established decoder library, rather than ad-hoc opcode parsing.

New instruction-analysis code should go through that layer for:

- instruction length;
- branch classification;
- `CALL` / `JMP` targets;
- conditional branches;
- RIP-relative references;
- relocation.

Keep the decoder library contained behind the instruction layer unless exposing one of its types is a
deliberate API decision.

Simple fixed-format operations remain reasonable when no general decoding is needed, for example a
verified 5-byte `E8 rel32` call site.

All instruction-layer changes must continue to work in a `win-x64` NativeAOT build.

# Xref scanning

A generic cross-reference scanner can use decoded instructions to identify:

- RIP-relative data references;
- direct `CALL` targets;
- direct `JMP` targets;
- conditional-branch targets.

Prefer decoded instruction semantics over ad-hoc opcode parsing.

# Maintained tool surface

Keep repository tooling focused on capabilities that Ghidra does not replace or that independently verify
Ghidra-derived conclusions:

- mapped-module capture and provenance;
- exact decoded opcode/image inspection in Ghidra, plus reusable discovery-layer locators for
  verification;
- reusable GW2 locator/scanner execution;
- read-only live debugger, watchpoint, tracepoint, and memory sessions;
- mutation and validation operations kept in a visibly separate command group;
- small development generators that still produce maintained source artifacts;
- a single operator wrapper that requires an explicit build argument.

Do not add subsystem-specific wrapper scripts when the operator wrapper can invoke the command directly.
Delete one-off investigation helpers once their durable findings have been promoted and no maintained
workflow or generated artifact depends on them.

# Tool-change reporting

If a task changes tooling, the final report should state:

- what existing tool/cache was checked;
- what was reused;
- what was extended;
- what capability was missing if a new tool was created.
