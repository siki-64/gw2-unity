# Validation

Validation must match the risk of the change and must distinguish compilation, automated tests,
offline locator evidence, and live confirmation.

## During implementation

Use build checks as needed to keep edited projects compiling. Do not run the full test suite after every
partial edit. Once implementation is complete and compiles, run the relevant tests once as final
validation. If they fail, fix the defect, compile again, and rerun only the affected tests.

## Documentation and reconstruction

- Verify durable claims against disassembly or a live test on an identified game build.
- Keep higher-level semantics provisional until independently supported.
- Cite canonical symbol names as the durable identity rather than absolute code coordinates. Runtime
  contracts may contain only intra-object offsets and validated anchor relationships; build-local code
  coordinates belong in the ignored analysis workspace and must not be copied into accessors.
- Keep wholly unpromoted unresolved observations out of the durable notes; durable notes may retain
  unresolved scope when their metadata says what remains provisional and why.
- Remove stale contradictions from the authoritative note and preserve useful disproven interpretations
  in [`../notes/RE_NOTES.md`](../notes/RE_NOTES.md).

Every durable note should identify its evidence scope near the top:

```markdown
**Confirmed build(s):** `<build or explicit unknown>`<br>
**Status:** `<confirmed, provisional, or quarantined scope>`<br>
**Unresolved:** `<remaining questions and cross-build limits>`
```

If the source build is not known, state that explicitly and do not infer it from a nearby native
source file or a current game build.

## Symbols, layouts, and Ghidra state

- Confirm symbol identity and layout offsets against the relevant image or live process.
- Keep active analysis names, comments, types, and build-local code coordinates in Ghidra.
- Require reusable locators to return a unique, structurally valid result.
- Treat an unmatched identity as unresolved; never reuse another build's code coordinate.
- Promote confirmed layout/type facts into the reconstructed contract layer and preserve the supporting
  reasoning in the durable notes.
- Use the runtime `ContextCollection` as the access anchor. Walk from that anchor through the
  reconstructed structs using intra-object offsets; do not add image-relative function, vtable, or
  trace addresses.
- See [`ghidra.md`](ghidra.md) for the analysis-workspace policy.

## Native layout update gate

The reconstructed contract layer is the authority for recovered in-process layouts, identifiers, and
build-scoped native contracts. Discovery may locate a live root dynamically, but root discovery alone
does not make stale field offsets safe. Runtime consumers must use the contract-owned validation gate
and publish no native data for an unknown or newer game image.

When a game update changes the image, first identify the new build and revalidate the affected structs
and relationships with disassembly and live reads. Then update the contract, focused layout/accessor
tests, and the corresponding RE note together. Only after those checks pass should the build be admitted
to the runtime support set. A successful locator or managed build by itself is not evidence that an old
layout remains valid.

## Architecture changes

Check both project references and source usage:

- The UI layer has no offsets, signatures, scanner calls, or native address policy.
- The runtime feature layer has no scanner calls and confines native pointers to narrow ABI/access
  boundaries.
- The contract layer has no feature toggles or feature policy.
- The discovery layer makes no feature decisions.
- Mutation machinery carries no configuration policy.
- Tools do not own reusable native layouts, locators, or mutation mechanics.

## Final review

Review the diff for unsupported semantics, guessed names, duplicated canonical definitions, stale or
wrong-build addresses, bare VAs in durable prose, stale comments, scratch leakage, unrelated edits, and
conflation of native behavior with client design.

The final report states what changed, the evidence and build when applicable, validation performed,
unresolved items, and any tooling/cache changes.
