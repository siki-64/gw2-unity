# Ghidra analysis workflow

Ghidra is the authoritative workspace for active reverse engineering of `gw2-64.exe`.

The local Ghidra project itself is intentionally not committed to Git because its `.rep` database is
large, binary, and churns heavily. Repository-tracked material records durable reasoning, reconstructed
native contracts, and reusable analysis scripts.

## Ownership

- **Ghidra project**: functions, symbols, namespaces/classes, signatures, data types, enums, comments,
  bookmarks, xrefs, and build-specific analysis state.
- **`src/Gw2.Native/`**: confirmed reconstructed layouts, enums, identifiers, and native access
  contracts suitable for compiled code.
- **`re/notes/`**: durable evidence, reasoning, negative findings, and subsystem behavior.
- **`re/scratch/`**: provisional observations that are not yet ready for durable notes.
- **`re/ghidra/scripts/`**: deterministic Ghidra maintenance and analysis scripts when a maintained
  script is needed; it currently contains only its repository guidance.
- **`src/Gw2.Discovery/`**: reusable runtime/offline locators that must work independently of the local
  Ghidra database.

There is no manually maintained TOML symbol catalog or build-address cache. The old `re/symbols/`
maps were retired after migration into Ghidra.

## Naming

Unknown functions use virtual-address placeholders:

```text
sub_<VA>
```

Internal branch labels use:

```text
loc_<VA>
```

Use `j_` only for genuine trivial thunks/jump wrappers. Unknown data should use width- or
representation-based placeholders such as `byte_`, `word_`, `dword_`, `qword_`, `off_`,
`tbl_`, and `vftable_` when justified.

A placeholder is intentionally provisional even though Ghidra stores the rename as user-defined.
Promote to a semantic name only when the evidence is strong enough.

## MCP and decompiler use

Ghidra MCP is the default high-level interface for agent-assisted analysis. Use it to navigate functions,
xrefs, callers/callees, strings, imports, globals, types, and decompiler output without copying large raw
binary regions into model context.

The decompiler is an accelerator, not a higher evidence tier than the binary. Treat inferred source
constructs, variable names, recovered types, casts, loop shapes, and simplified expressions as provisional
until the relevant instruction/data flow supports them. For changes that affect a native contract,
calling convention, object layout, bit/flag meaning, branch condition, virtual dispatch, ownership, or
runtime state transition, verify against decoded instructions and use live debugging when static evidence
does not settle the question.

A productive loop is:

```text
Ghidra/MCP navigation -> decompiler hypothesis -> opcode validation -> live validation when needed
                     -> Gw2.Native / notes -> tests
```

Keep Ghidra writes conservative during exploration. Semantic renames and types may be useful locally, but
they must not silently promote decompiler guesses into repository facts.

## Promotion flow

```text
gw2-64.exe
    |
    v
Ghidra investigation
    |
    +--> provisional comments / names / types
    |
    v
evidence + validation
    |
    +--> re/notes/
    |
    v
Gw2.Native reconstruction
    |
    v
tests
```

Ghidra may contain hypotheses and partially reconstructed types. `Gw2.Native` should contain only
facts that are sufficiently confirmed for a compiled native contract.

## Build changes

When the game updates:

1. import/analyze the new binary in Ghidra;
2. use Ghidra Version Tracking and structural evidence to transfer or re-find known identities;
3. revalidate important symbols, signatures, layouts, constants, and call relationships;
4. keep unmatched identities unresolved rather than borrowing an old address;
5. update durable notes and `Gw2.Native` only after the new-build evidence supports them.

Absolute VAs are useful local coordinates inside one analyzed build, not durable cross-build identities.

## Repository policy

The local Ghidra project is ignored by Git:

```text
re/ghidra/*.gpr
re/ghidra/*.rep/
/re/**/*.gbf
/re/**/gw2-64.exe
```

Do not commit `.gbf` database files or the original game executable. Commit reusable scripts and
human-readable notes instead.

Historical unresolved entries from the retired symbol catalog are quarantined in
[`../notes/migration/unresolved-legacy-symbols.md`](../notes/migration/unresolved-legacy-symbols.md).
They are leads only and must be re-proved against the active binary before promotion.
