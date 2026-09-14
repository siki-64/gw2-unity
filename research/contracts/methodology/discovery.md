# Symbol discovery

Discovery turns a durable native identity into a validated address for one identified game build. It
does not assign feature meaning or decide whether a feature should be enabled.

General naming and promotion rules live in [`reconstruction.md`](reconstruction.md). Tool operation and
cache handling live in [`tooling.md`](tooling.md).

## Ownership

- The active Ghidra project owns analysis symbols, namespaces, comments, types, xrefs, and build-specific addresses.
- The durable notes under [`../notes/`](../notes/) own evidence and behavioral reasoning.
- A reusable discovery layer owns scanners, resolvers, and executable locator abstractions.
- An operator-facing tool layer owns command parsing, output, live diagnostic sessions, and dump
  provenance validation.
- The reconstructed contract layer owns the layouts, enums, and access contracts a locator relies on.

Do not recreate a manually maintained symbol/address catalog, embed build addresses in C#, or leave
reusable locator logic inside a tool command. See [`ghidra.md`](ghidra.md).

## Locator contract

A locator must:

1. accept an explicit image/span and base-address contract;
2. establish the intended identity from stable code or data relationships;
3. reject missing and ambiguous results;
4. validate the relevant instruction, reference, layout, or surrounding structure;
5. return failure without guessing or borrowing another build's address.

Use the smallest stable surface that remains unique. Prefer structural relationships, decoded
RIP-relative references, call relationships, and narrow signatures over large flat byte patterns.
Wildcard branch displacements and other volatile address material.

String and assert/source-file anchors can narrow a search area, but they do not establish high-level
semantics on their own.

## Instruction analysis

All general x64 instruction analysis goes through a shared decoding layer backed by an established
x64 decoder library, with relocation handled by the same layer. Do not add manual instruction-length,
branch classification, branch-target, or RIP-relative decoding when the instruction layer already
exposes the required semantics.

Fixed, already-validated encodings may use direct byte emission when no general decoding is involved.

## Build workflow

1. identify the build from the target process or image;
2. capture or select evidence from that build;
3. run the durable locator;
4. require a unique, structurally valid result;
5. compare it with supporting disassembly or a scoped live observation;
6. record the confirmed identity, comments, and types in the active Ghidra project;
7. update durable notes or the reconstructed contract layer only when the evidence is strong enough.

Agents can run the same normal shell commands as human operators. Use `--format json` when structured
output is convenient; do not scrape prose or infer live addresses from analysis VAs.

An unresolved result remains unresolved. A Ghidra address may speed investigation and comparison, but
runtime patch safety still depends on locator and precondition validation.

## Runtime boundary

Startup discovery and reusable resolution belong in dedicated lower layers, separate from any runtime
feature layer. A runtime consumer consumes resolved contracts and never invokes scanners. Applications
see semantic availability or feature state, never addresses, signatures, or offsets.
