# EfCliContext static reconstruction

**Build:** 207.032  
**Scope:** generic client effect runtime; static executable analysis only

`EfCliContext` is the process-lifetime owner and factory for created client effect
objects. Authored `IEffectDef`, `SkillEffectApplicationEntry`, `AvCharEffectEntry`,
and runtime `EfCliEffect` objects are separate structures.

## Lifetime and interfaces

```text
singleton                         0x1428AA230
constructor RVA                   0x1343CF0
in-place destructor RVA           0x1343FD0
scalar deleting destructor RVA    0x1344270
accessor RVA                      0x1345BD0
minimum object size               0x1A8

primary vfptr   +0x00 -> 0x142239818
secondary vfptr +0x08 -> 0x1422398F0
tertiary vfptr  +0x10 -> 0x142239998
```

The CRT initializer constructs the singleton and registers an `atexit` thunk to
the in-place destructor. Primary vtable slot `+0x00` is the update/tick routine,
not a destructor. The scalar-deleting wrapper is at slot `+0xD0`.

## Confirmed primary vtable surface

```text
+0x00  RVA 0x13448E0  update/tick
+0x08  RVA 0x1344C50  core creation
+0x10  RVA 0x1344B70  classified creation wrapper
+0x30  RVA 0x1345AD0  request graceful removal
+0x38  RVA 0x1345B50  request immediate stop/removal
+0x40  RVA 0x13460D0  refresh owned state/mode
+0x48  RVA 0x1346110  reset/drain
+0x50  RVA 0x1346390  initialize/register owned helpers
+0x58  RVA 0x1346FD0  recursively preload definition resources
+0xC0  RVA 0x1346230  definition substitution
+0xC8  RVA 0x1346440  runtime-handle binding
+0xD0  RVA 0x1344270  scalar deleting destructor
```

Other slots remain recorded by address in Ghidra but are not assigned semantic names
until their contracts are independently established.

## Partial object layout

```text
+0x018 definition/resource registry
+0x080 active runtime-effect collection
+0x090 active-effect traversal head
+0x0A8 deferred-removal/agent-association collection
+0x0D8 state/filter subobject
+0x0F8 smoothing value (initialized to 1.0)
+0x0FC/+0x100/+0x104 smoothing state
+0x108 frame accumulator
+0x110 owned helper pointer
+0x118 owned subobject
+0x158 tracked-effect array
+0x164 tracked-effect count
+0x170 update-state flags (tick temporarily sets bit 0)
+0x174 runtime mode refreshed from another subsystem
+0x178 and +0x190 tail container records
```

Constructor/destructor evidence proves these boundaries and ownership, but not every
container key/value type. Unresolved regions remain opaque in `Gw2.Contracts`.

## Authored and runtime object fields

```text
IEffectDef
+0x10 uint32      content type (`0xB7` for an effect definition)
+0x28 uint32      applicationTargetMode (native name; factory values 0 through 11)
+0x30 void*       implementation-specific authored payload
+0x38 uint32      flags
+0x48 IEffectDef* alternate/redirect definition
+0x50 uint32      character-class discriminator used by an adjacent guard

EfCliEffect
+0x18 IEffectDef* authored definition
+0x20 uint32      runtime state flags
+0x30 uint32      tracked-effect slot id
+0x68/+0x70       active-list link
```

`IEffectDef +0x28` has the native assertion name `applicationTargetMode` and directly
selects the runtime implementation family. It must not be confused with the separate
target-selection discriminator in `SkillEffectApplicationEntry +0x08`.

## Creation and factory

Normal world creation uses wrapper virtual `+0x10`. It derives a primary-agent
classification bit and forwards to core virtual `+0x08`. The core can redirect or
substitute the definition, apply definition/agent/player/map gates, allocate the
selected implementation, insert it into `+0x80`, bind a tracked slot id, and index
a non-null auxiliary target through `+0xA8`.

```text
type 0       size 0x0D8  constructor RVA 0x1348D30
type 1, 11   size 0x0A0  constructor RVA 0x1349EA0  compound family
type 2       size 0x080  constructor RVA 0x134BF30
type 3       size 0x108  constructor RVA 0x134C620
type 4,6,10  size 0x210  constructor RVA 0x134FD40  model family
type 5       size 0x0E0  constructor RVA 0x134CC00
type 7       size 0x088  constructor RVA 0x13542A0
type 8       size 0x0A8  constructor RVA 0x13547F0
type 9       size 0x090  constructor RVA 0x1354EE0
```

Compound, model, and mode-2 decal families have direct behavior/source evidence.
Mode 2 is `EfCliEffectDecal`: its constructor's vtable points to destruction and
placement routines asserting `EfCliEffectDecal.cpp`. Its runtime emits a separate
map decal; it is not the persistent ground-targeting model. See
Ground-targeting outline geometry is outside the scope of these findings. Audio, material, post-process, and
camera-shake associations remain hypotheses rather than compiled enum names.

## Model-family transform (types 4, 6, 10)

The [model drawing layout continuation](model-drawing-layout.md) adds the setup
descriptor (including rotation), created model/host distinction, listener and tracked
attachment ownership, and authored scale bounds with exact-image instruction evidence.

The shared `0x210` model-family object built at RVA `0x134FD40` owns a 3x4 row-major
transform at `+0x120`. The constructor initializes those 12 floats from the engine's
identity-transform helper. The setup routine at RVA `0x1352840` later populates that
matrix from the selected placement source.

```text
EfCliModelEffect
+0x120 float4 row 0
+0x130 float4 row 1
+0x140 float4 row 2

translation / world position
+0x12C float X
+0x13C float Y
+0x14C float Z

+0x198 attachment reference value from constructor auxiliary argument
+0x1A0 attachment reference next link
+0x1A8 attachment reference previous link
```

The translation offsets are not inferred only from matrix convention. One setup
branch explicitly copies a source position from `source +0x20/+0x24/+0x28` into
`effect +0x12C/+0x13C/+0x14C`. Another branch copies a complete transform into
`+0x120` and then overwrites those same three fourth-column fields from a position
returned by the placement source. This establishes the three fields as the runtime
world translation used by the model family.

All setup branches converge at RVA `0x1352A48`; the common epilogue begins at
RVA `0x1352A78`. At that return seam RBX still holds the `EfCliModelEffect*`, which
provides an observation-only point after the transform has been populated.

The controlled ally Throw Mine capture produced three type-6 authored definitions:

```text
definitionId 27203  key 7B2E6F7F93BD45A940106C65984DC948
definitionId  6899  key F990B509005D248E46EE1BC926AE402C
definitionId 13649  key CE210BF5FCAC488D4A5C8CB14C9A6467
```

These are still candidates rather than semantic assignments. Runtime transform
observation is required to determine which one tracks the actual mine placement.

## Tracked slot replacement

Creation passes its final argument to RVA `0x13485B0`, which replaces
`EfCliEffect +0x30`. A nonzero id indexes the context array at `+0x158`; registration
is unique. Replacing an occupied slot clears the previous effect's id and requests
its removal through virtual `+0x30`. A zero id creates no tracked-slot binding.

This tracked id is separate from the factory's `effectId` argument. The captured
Chalice call used dynamic `effectId = 0xFFFFFFFF` and tracked slot id zero.

## Removal, tick, and destruction

Graceful removal (`+0x30`) removes the secondary association, optionally sets runtime
flag `0x10`, then idempotently sets teardown bit `0x02` and calls effect virtual
`+0x168`. Immediate stop (`+0x38`) additionally calls virtual `+0x170` first.
Neither path unlinks or frees synchronously.

Tick RVA `0x13448E0` sets `EfCliContext +0x170` bit 0, saves the next list node before
callbacks, updates effects, and tests stopping effects for completion. Completed
effects pass through RVA `0x13481A0`: virtual `+0x1E0` cleanup, list unlink, then
deleting destructor `+0x18(effect, 1)`.

Host loss during tick marks completion bit `0x20` and defers deletion to the current
traversal. Outside tick it can destroy immediately. Context reset/destruction drains
the active list directly and bypasses graceful stop/completion.

Callers must never directly free an `EfCliEffect*`. The pointer becomes stale after
native completion and unlink.

## Thread boundary

No explicit thread-id assertion was found. Creation, removal, registration, and tick
mutate unsynchronized intrusive collections. The `inTick` flag handles same-thread
callback reentrancy, not cross-thread access. Client operations must use the established
game-thread dispatcher.
