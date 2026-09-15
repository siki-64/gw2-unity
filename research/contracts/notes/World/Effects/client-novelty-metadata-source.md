# Champion novelty metadata source — current static frontier

**Build:** 207.032  
**Scope:** in-world receive-side metadata resolution only. Preview-panel data is unrelated.

## New result: SkillDefinition +0x28 is the SkillId

`AsBuff.cpp` provides a direct semantic anchor for `SkillDefinition +0x28`.

The Buff UI receives `buffData.skillDef` at record `+0x00`, reads the dword at
`skillDef +0x28`, and uses it as the key for its existing-buff lookup/replacement.
The same value is passed to the diagnostic:

```text
Tried to remove a buff that isn't (or wasn't) applied: ID %u
```

Therefore:

```text
SkillDefinition +0x28 = uint32 SkillId
```

`AvCharEffect` deliberately reuses this SkillId as its sorted per-character runtime
effect selector. It is not an `IEffectDef` content key.

## Character-owned resolved Buff/event collection

Static reconstruction of RVA `0x11315B0` shows that `AvCharEffect` can rebuild its
runtime effects from an already-resolved character-owned collection.

Starting from `AvCharEffect* self`:

```text
characterView = self +0x430
interface     = characterView +0x08
source        = interface->virtual +0x168
collection    = source->virtual +0x108
iterator      = collection->virtual +0x40
record        = iterator->virtual +0x28(&iteratorState)
next record   = iterator->virtual +0x28(&iteratorState)
```

Every returned record is passed directly to:

```text
RVA 0x11288E0 AvCharEffect_InstallEvent
```

The install event consumes:

```text
record +0x00 = SkillDefinition*
record +0x18 = primary Agent*
```

The same `+0x40` begin / `+0x28` next iterator protocol appears in
`Ui/Widgets/AgentStatus/AsBuff.cpp` when it rebuilds visible Buff state. This is a
strong structural link to the character's resolved Buff collection.

## Consequence for Chalice metadata resolution

The current Chalice probe can resolve and validate the resident world `IEffectDef`.
It no longer needs an arbitrary reverse-reference scan to identify metadata while a
genuine Chalice Buff/event is present.

A read-only classifier can enumerate the character-owned resolved Buff/event records
and validate each `SkillDefinition`:

```text
metadata = record +0x00
require metadata != null
require metadata +0x58 == Buff (1)
read SkillId at +0x28
read applications at +0x40
read count at +0x48
for each 0x20-byte application:
    if application +0x00 == validated Chalice IEffectDef*:
        this metadata is the Chalice SkillDefinition
        record SkillId, application pointer, flags and type
```

This would immediately recover the missing parent SkillId and determine whether the
loaded Chalice definition contains application types `0x200000` / `0x400000`.

It is also materially safer and more deterministic than scanning writable process
memory for reverse pointers.

## Remaining durable-lookup problem

The collection path only resolves metadata that is already present on the character.
For client-only activation before the Buff/event exists, the durable resolver is still
needed:

```text
SkillId -> persistent content registry -> SkillDefinition*
```

The executable now gives us the correct search key (`SkillId`) and the exact target
layout. Static work should focus on callers that resolve numeric skill ids into
`SkillDefinition*`, rather than effect-definition lookup or `EfCliContext`.

## Ranked static targets

1. **Persistent SkillDefinition lookup by SkillId.** Trace content/skill resolvers that
   return a pointer later checked at `+0x58` for Ability/Buff type. This is the only
   missing durable metadata seam.
2. **Resolved Buff collection interface.** Identify the concrete interface/class behind
   character-view virtual `+0x168`, source virtual `+0x108`, and iterator virtuals
   `+0x40/+0x28`. This can give stable names and possibly a direct lookup-by-id virtual.
3. **Chalice/Sun paired-form classification.** Once a genuine metadata pointer is
   available, enumerate `+0x40/+0x48` and record application types. No further factory
   reverse engineering is needed for this question.
4. **Application semantic enums.** Continue tracing consumers of application `+0x08`,
   `+0x18`, and `+0x1C`, but do not borrow unrelated `applicationType` assertions from
   other subsystems such as DynamicCamera.
5. **Low priority:** more `EfCliContext` internals. Creation/removal/lifetime are already
   reconstructed well enough that additional factory detail does not solve metadata
   resolution.

## Preview separation

Nothing in this note applies to the preview-panel path. The known Laurels preview
`IEffectDef` remains a separate authored object and must not participate in world
metadata discovery.
