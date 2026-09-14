# Source reconstruction and symbol ownership

The repository reconstructs `gw2-64.exe` as named native contracts rather than as a list of addresses.

`Gw2.Native` is an **object-oriented reconstruction of native C++ contracts**. Reconstructed C# should
organize recovered native objects, embedded subobjects, fields, enums, vtable relationships, and callable
contracts into coherent types while preserving the binary facts that justify them. Object-oriented
organization must not introduce source-level semantics that the binary has not established, hide exact
offsets or pointer relationships, or collapse unresolved storage into invented classes.

Absolute code/global VAs are build-specific analysis coordinates. Durable repository identity comes from
evidence-backed semantics/structure plus a reusable locator where runtime resolution is required; active
build addresses live in Ghidra rather than a repository address cache.

Prose and reconstructed code refer to semantic symbols. Active identities, namespaces, comments, types, xrefs, and build-specific addresses live in the local Ghidra project; see [`ghidra.md`](ghidra.md).

## Layout and accessor boundaries

`src/Gw2.Native/` contains the raw native layout overlays and their evidence-backed declarations.
`src/Gw2.Native/Access/` is intentionally narrower: it exists only to reproduce native accessor
behavior, including vtable dispatch, accessor slots, native call signatures, and the validity/null
semantics established by the recovered code. It is not a second object model or a place to add
convenient field-based shortcuts when a native accessor contract is known.

This separation is the long-term boundary: layouts describe what native memory contains, while Access
describes how native code reaches and validates that state. Direct field reads remain appropriate when
the recovered native path itself reads the field directly.

# Naming

Folder names and native declaration names serve different purposes. Organize `src/Gw2.Native/` into
human-readable semantic domains for navigation. Within those folders, preserve binary-supported native
names, subsystem prefixes, layout identities, and provisional names as closely as the evidence permits.
A readable folder name is not evidence for renaming a native type or member.

Use this naming precedence for functions, routines, globals, structs, enums, and members:

1. preserve an evidence-backed native name when one is recoverable;
2. when no native name is available, use a descriptive reconstructed name only after the role is confirmed;
3. otherwise retain an address-derived or offset-derived provisional name.

Do not replace a recovered native name merely because an inferred semantic name reads better. A name
suggested only by a decompiler, reflection string, nearby source/assert string, or correlation is not an
evidence-backed native identity without the independent support required by this methodology.

## Analysis-symbol namespace versus reconstructed-source namespace

Ghidra is the **analysis/discovery workspace**, not reconstructed C# source. When an entity has no
evidence-backed native name, keep its Ghidra identity analysis-like (`sub_<VA>`, `FUN_<address>`,
`DAT_<address>`, etc.) rather than normalizing it into C# style merely for readability. Put reconstructed
meaning in Ghidra comments, durable notes, locators, and reconstructed C#.

This separation is intentional:

```text
Ghidra analysis identity       reconstructed C# identity
FUN_141234560              ->  Routine1234560
DAT_142345670              ->  Data2345670
sub_54D3C0                 ->  Routine54D3C0
```

The right-hand names are source-side placeholders only. Promotion of a reconstructed C# identifier does
not by itself require renaming the analysis symbol; each name serves a different purpose.

## Decompilation provisional names

The `decomp` branch uses C#-style provisional names that state what is known without inventing semantics.
Use PascalCase names in reconstructed C# and keep the hexadecimal address/offset suffix visible until the
entity is understood well enough to promote.

| kind | provisional form | example | meaning |
|---|---|---|---|
| executable routine | `RoutineXXX` | `Routine54CEB0` | executable routine located, role unknown |
| static/global datum | `DataXXX` | `Data710AC6B9C` | global/static storage located, role unknown |
| struct member | `UnknownXXX` | `Unknown98` | field offset/type known, semantics unknown |
| opaque embedded region | `UnknownXXXStorage` | `Unknown0A4Storage` | inline storage size/layout boundary known, semantic type unknown |
| flags/bitfield member | `FlagsXXX` | `Flags178` | field is confirmed to behave as flags, exact meaning may remain partial |
| confirmed padding | `PaddingXXX` | `Padding314` | bytes are confirmed alignment/compiler padding rather than an unknown member |
| confirmed reserved storage | `ReservedXXX` | `Reserved40` | storage is known to be intentionally reserved |

`XXX` is the hexadecimal analysis address, RVA, slot, or member offset already used to identify the
entity. Match the local file's offset-width convention rather than adding or removing leading zeroes only
for appearance.

Use the full word `Unknown`, not `Unk`. The abbreviation saves little space, is less searchable, and
makes reconstructed source look more like disposable decompiler output. `UnknownXXX` is the repository's
single neutral member placeholder.

Use `Storage`, not `Data`, for opaque inline regions. `DataXXX` already identifies provisional
static/global data, while `UnknownXXXStorage` specifically means "an inline byte range whose extent is
known but whose semantic native type is not." Calling that range `UnknownXXXData` would blur global data
identity with embedded storage and would imply more semantic structure than has been recovered.

Use `RoutineXXX`, not `SubXXX` or `SubroutineXXX`, for an unidentified executable routine in reconstructed
source. `Routine` is a full neutral word without carrying the analysis-tool flavor of `sub`, while staying
short enough to remain readable in call sites. The Ghidra side may continue to use `FUN_...` or `sub_...`
when those are the actual analysis identities.

The placeholder name describes **semantic uncertainty**, not type uncertainty. Let the C# declaration
carry information already established by the binary:

```csharp
[FieldOffset(0x098)] internal Agent* Unknown98;
[FieldOffset(0x0A0)] internal uint UnknownA0;
[FieldOffset(0x178)] internal uint Flags178;
```

Do not rename these to `UnknownPtr98`, `UnknownUInt32A0`, or similar merely to repeat the declared C#
type. Add semantic detail to the name only when that detail is independently supported.

A provisional member suffix always comes from the member's **own offset in its containing type**. Do not
encode the offset of a pointed-to target into the member name. For example, a pointer at
`ChCliCoreStats +0x258` that currently points to `ChCliCharacter +0x548` is `Unknown258`; the target's
`+0x548` relationship belongs in a comment or behavioral note until its semantics are recovered.

### Canonical names in documentation

When a reconstructed C# type or member already has a canonical name, behavioral documentation should use
that name rather than retaining an older decompiler alias. For example, if `WvCamera.cs` contains
`Unknown40`, prose should not continue to call the same member `field_40` unless the decompiler spelling is
being quoted explicitly for provenance.

The same rule applies to promoted members: once `Unknown3E8` is promoted to `Health`, current behavioral
documentation should use `Health`. Git history preserves the old name; durable docs should describe the
current reconstruction.

Once a provisional C# member has been promoted, the owning native layout contains one canonical
declaration for that starting offset. Rename repository consumers and tests atomically with the promotion;
do not retain source-compatibility aliases for the old provisional spelling. A canonical master layout
must not declare a second field at the same starting offset under another name. When independently
recovered interpretations need to coexist, represent them as distinct reconstructed view/subobject types
rather than aliases in the same master structure, and document the relationship.

Analysis-symbol names are the exception described above: when documentation refers specifically to a
Ghidra analysis symbol, use its current Ghidra identity rather than silently rewriting it into C# casing.

### Native pointer relationships

Make confirmed object relationships visible in the reconstructed field type. Unsafe syntax is part of the
native model here; do not erase a known pointee identity into `nint` merely to make the declaration look
more managed.

Use these representations consistently:

```text
T field                         confirmed embedded native object/subobject
T* field                        confirmed pointer to a reconstructed native object
T** field                       confirmed pointer table/array of reconstructed native-object pointers
nint field                      pointer/address-shaped storage whose target type is unresolved
function pointer                delegate* unmanaged<...> when the callable signature is established
```

A known pointee type does not require a semantic member-name promotion. For example, if `+0x98` is
confirmed to point to an `Agent` but its role in the containing class is unresolved, prefer
`Agent* Unknown98` over `nint Unknown98`. The type communicates structural knowledge while the name
continues to communicate semantic uncertainty.

Do not type pointers speculatively. A pointer into a known object's unresolved interior is still `nint`
until the target subobject/type identity is recovered. Likewise, a string-like pointer whose exact native
wrapper/ownership contract is unresolved may remain `nint` even when live memory reveals character data.
The goal is for `T*` to mean that the pointee identity is evidence-backed.

### Opaque storage

Use `UnknownXXXStorage` when a fixed inline range is known to exist but its native type or internal member
layout is not yet established:

```csharp
[FieldOffset(0x0A4)] internal Unknown0A4Storage Unknown0A4;

[StructLayout(LayoutKind.Sequential, Size = 0x1C)]
internal struct Unknown0A4Storage { }
```

A storage placeholder may still contain confirmed internal structure while the enclosing native type
remains unidentified:

```csharp
[FieldOffset(0x390)] internal Unknown390Storage Unknown390;

[StructLayout(LayoutKind.Explicit, Size = 0x40)]
internal struct Unknown390Storage
{
    [FieldOffset(0x000)] internal nint Vtable;
    [FieldOffset(0x010)] internal ChCliProgress.NotificationList Notifications;
}
```

A declared storage size means only that the represented inline range has that recovered extent. It does
not by itself establish the original C++ source type, ownership model, constructor boundary, or that every
byte in the range is one semantic object.

When a member is recovered inside a previously opaque span, shrink or split the surrounding
`UnknownXXXStorage` so that opaque storage describes only bytes that remain unresolved. Do not leave a
large placeholder covering independently reconstructed fields merely because the placeholder existed
first. An intentional overlapping native view is different: keep the overlap when the binary supports it
and document why both views are useful.

Do not call an unexplained gap `PaddingXXX`. A gap remains unknown storage until binary evidence shows
that it is actually padding.

### Reconstructed type size

Do not infer a native object's complete size solely from its highest recovered field. If the allocation,
deallocation, constructor/destructor range, array stride, enclosing-object boundary, or other independent
evidence does not establish the full extent, omit an overall `Size` where practical and describe the
layout as partial.

When `[StructLayout(..., Size = ...)]` is used as a canonical native object size, the evidence for that
size should be recoverable from the surrounding reconstruction or behavioral notes. A storage placeholder
may use `Size` simply to preserve a confirmed gap between neighboring fields; that is a layout boundary,
not a claim that the gap is a separately allocated native object.

### Routines

Use `RoutineXXX` for a located executable routine whose role is still unknown. The name should remain
neutral even if the routine is known to receive a particular object, return a pointer, or participate in a
specific call path.

Promote the routine only after its role is sufficiently established:

```text
Routine54CEB0
    -> GetUnknownContext      only if the getter role is actually established
    -> GetContextCollection   once the returned native contract is established
```

If the owning native type is known before the routine's purpose, preserve that ownership in file/folder,
call-site, or documentation context rather than inventing a semantic routine name.

### Partial semantic knowledge

Use the narrowest name justified by evidence. For example, `Flags178` is appropriate when bitwise use is
confirmed even if the individual bits are not understood. `Unknown178` remains preferable when even the
flags interpretation is speculative.

Likewise, use established structural names such as `Vtable`, `OwnerCharacter`, or `TrackedClientHead`
when those relationships are directly supported, even if the containing `UnknownXXXStorage` type has not
been identified.

Do not replace a provisional name merely to make prose or reconstructed code cleaner.

## Confirmed names

When no evidence-backed native name is available and the role is confirmed, use a descriptive,
address-free reconstructed name:

- functions/routines → role names such as `NameRender`, `Clamp01Fade`, `ApplyFadeOpacity`;
- globals → `g_` prefix such as `g_Settings`, `g_CombatTracker`;
- stable constants → `k_` prefix such as `k_DimCap`;
- structs/enums → reconstructed C# types in the owning `src/Gw2.Native/` folder;
- struct members → semantic names such as `Health`, `Inventory`, `Profession`, or `TrackedClientHead`.

Unknown members remain `UnknownXXX`; opaque ranges remain `UnknownXXXStorage`; confirmed flags may use
`FlagsXXX` until their semantic name is known.

# Promotion

Promotion is evidence-driven.

A provisional entity may be renamed when the evidence establishes the role strongly enough for durable use. Evidence may include:

- direct control/data-flow reconstruction;
- stable caller/callee relationships;
- confirmed layout use;
- an identified network/render/update path;
- a live test against a known game build.

Do not promote semantics from:

- decompiler variable names;
- nearby assert/source strings alone;
- reflection names alone;
- visual appearance alone;
- one-off pointer equality;
- an unverified correlation.

Promotion should increase semantic information without discarding structural facts already established by
the binary. For example:

```text
Unknown3E8 -> Health
Unknown400Storage -> <native/reconstructed type name once identified>
Flags178 -> CharacterFlags
Routine54CEB0 -> GetContextCollection
```

If the role remains uncertain, keep the provisional name and place the interpretation in `re/scratch/SCRATCH.md`.

# Canonical ownership

A finding may legitimately exist in several layers when each owns a different aspect:

```text
active Ghidra project
    analysis identity + build-specific address + analysis metadata

C# under src/Gw2.Native/
    canonical reconstructed layout / enum / stable value

re/notes/
    confirmed runtime behavior
```

Do not duplicate canonical definitions between layers.

### Evidence status in subsystem notes

Each authoritative `re/notes/<area>/...` document should make its evidence scope obvious near the top:

- **Confirmed build(s):** builds on which the described behavior was directly validated;
- **Status:** `build-local`, `cross-build confirmed`, or `current invariant`;
- **Unresolved:** material boundaries that remain provisional or need another build/live confirmation.

Do not silently promote one build's observation into an unqualified cross-build invariant. Architecture
docs may summarize recovered behavior, but build-sensitive details belong in the owning RE note.

### Comments in native layouts

Compiled native structs own the exhaustive field/offset layout. Keep comments sparse and local: explain
only a non-obvious invariant, overlap, variant field, or unresolved semantic boundary needed to read the
declaration safely. Evidence narratives, discovery history, build addresses, constructor/destructor
walkthroughs, and multi-line behavioral explanations belong in the owning RE note.


- Docs may mention offsets, vtable slots, masks, and stable constants where necessary to explain behavior.
- Docs must not become a second canonical struct/enum definition.

# What stays inline

| kind | example | durable form |
|---|---|---|
| absolute code VA | `0x54CEB0` | symbolize |
| static/global VA | absolute address | symbolize in Ghidra |
| struct offset | `+0x27C` | inline where relevant; canonical in struct |
| vtable slot | `vtbl+0x40` | inline |
| packet offset | `+0x21` | inline |
| mask/flag | `0x1000` | inline / canonical enum if appropriate |
| stable value | `1000.0`, `0.6` | inline; storage VA may have a symbol |

Absolute addresses are build-local coordinates, not semantic identifiers or repository address caches.

# Locator contract

Each address-bearing symbol should have a durable locator appropriate to the entity.

Common locator forms include:

| locator | typical resolver |
|---|---|
| string xref | Ghidra string references |
| byte/AOB signature | Ghidra byte search; reusable `Gw2.Discovery` scanner |
| data signature | Ghidra byte search; reusable `Gw2.Discovery` scanner |
| static global reached through code | Ghidra instruction listing and xrefs |
| call/xref relationship | Ghidra xrefs |
| vtable candidate / references | Ghidra pointer-table inspection and xrefs |

Use the existing tooling described in [`tooling.md`](tooling.md).

All build-sensitive tool runs should identify the game build:

```text
Gw2.Tools --build <game-build> ...
```

# Build updates

On a game update:

1. capture or obtain a fresh image for the new build;
2. rerun the symbol locators;
3. validate each resolved identity;
4. update only the active Ghidra analysis state for that build, then promote confirmed facts to notes or `Gw2.Native`.

Confirmed reconstructed source names, layouts, and behavioral docs should remain unchanged unless the
underlying native contract itself changed. Ghidra analysis identities may remain tool-facing even when the
reconstructed C# name is promoted.

Never carry an unresolved address from one analyzed build into another.

# Documentation promotion

Confirmed target behavior belongs in one authoritative behavioral document.

Other docs should cross-reference that subject rather than duplicate the full reconstruction.

If a previous interpretation is disproven:

- replace stale behavior in the authoritative doc;
- record the negative finding in `re/notes/RE_NOTES.md` only when it is useful enough to prevent future mistakes.

Git retains chronology; durable docs should describe the current confirmed model.
