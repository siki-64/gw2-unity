# Champion novelty in-world state — AvCharEffect static decode

**Build:** 207.032  
**Scope:** in-world/receive-side effect state only.  
**Do not use preview-panel definitions or preview-agent state here.**

This note extends `client-novelty.md` with the deeper `AvCharEffect` state decode. The existing preview/world split remains authoritative:

```text
PREVIEW PANEL
  Laurels preview IEffectDef 45777 / 0xB2D1
  preview content key 49 B7 88 FF 6B CF 00 40 8A 98 99 21 0E EB D3 36

IN WORLD
  controlled 2026-09-09 Chalice sample
  world IEffectDef 23369 / 0x5B49
  world content key 29 6B D1 82 28 58 1A 48 98 B9 D1 72 FC 69 3D 78
```

No preview definition is inferred to be a world definition.

## 1. Authored application versus runtime effect

The world path contains distinct authored and runtime structures.

```text
SkillDefinition +0x40 -> SkillEffectApplicationEntry[]
SkillDefinition +0x48 -> count
entry stride          -> 0x20

SkillEffectApplicationEntry +0x00 -> authored IEffectDef*
SkillEffectApplicationEntry +0x08 -> uint32 target-selection discriminator
SkillEffectApplicationEntry +0x18 -> uint32 application flags
SkillEffectApplicationEntry +0x1C -> uint32 application type
```

`AvCharEffect` separately owns created runtime state:

```text
AvCharEffect +0x80 -> AvCharEffectEntry[]
AvCharEffect +0x8C -> count
entry stride       -> 0x20

AvCharEffectEntry +0x00 -> selector
AvCharEffectEntry +0x04 -> runtime handle
AvCharEffectEntry +0x08 -> created runtime effect object
```

The authored `IEffectDef*` is selected before `AvCharEffect` dispatch and passed in `r9`; it is not recovered from the runtime entry.

## 2. Ability/Buff tagged SkillDefinition payload

`SkillDefinition` has a tagged payload:

```text
+0x58 uint32 payloadKind
+0x60 void*  payload

0 = Ability
1 = Buff
```

This is supported by separate assertion sites for `skillDef->GetAbility()` and `skillDef->GetBuff()`. The tag-1 path is also asserted as `skillBuff` in `AvCharEffect.cpp`.

## 3. Paired authored application types

RVA `0x1122040` receives a boolean and selects exactly one of two application types:

```text
false -> 0x400000
true  -> 0x200000
```

It uses an `AvCharEffect` binding table whose recovered layout is:

```text
container base: AvCharEffect +0x98
array:          AvCharEffect +0xA0
count:          AvCharEffect +0xAC
entry stride:   0x10

struct PairedApplicationBindingEntry
{
    +0x00 uint32 selector;
    +0x08 SkillEffectApplicationEntry* application;
}
```

For each matching binding, the helper resolves the corresponding `SkillDefinition`, selects the authored application whose `+0x1C` equals the requested pair member, and redispatches it through the normal world-effect path.

RVA `0x1130480` implements the matching removal side. It receives the same boolean, chooses the same `0x200000`/`0x400000` member, finds matching stowed/runtime state by selector, destroys a live runtime effect when present, and removes the associated state entry.

Therefore `0x200000` and `0x400000` are a deliberate mutually-exclusive application-state pair, not incidental constants.

## 4. Stowed buff effect table

A separate container at `AvCharEffect +0x230` is tied directly to assertion text `stowedBuffEffect`.

Recovered layout:

```text
container base: AvCharEffect +0x230
array:          AvCharEffect +0x238
count:          AvCharEffect +0x244
entry stride:   0x18

struct StowedBuffEffectEntry
{
    +0x00 uint32 selector;
    +0x08 IEffectDef* effectDefinition;
    +0x10 SkillEffectApplicationEntry* application;
}
```

The stow transition does not merely set a flag. For eligible Buff definitions it can:

1. locate the current `AvCharEffectEntry` by selector;
2. destroy the live runtime effect through `EfCliContext`;
3. locate the authored `SkillEffectApplicationEntry` from the owning `SkillDefinition`;
4. insert a `StowedBuffEffectEntry` containing the selector, authored `IEffectDef*`, and application pointer;
5. remove the corresponding live runtime entry.

The reverse path walks the stowed table, resolves the owning `SkillDefinition` again, and recreates the authored world effect through normal `AvCharEffect` dispatch.

This is strong evidence that `+0x230` is deferred/restorable buff-effect state.

## 5. Relation to the 0x200000 / 0x400000 pair

The state-transition routine obtains two separate state IDs from adjacent virtual methods (`+0xF0` and `+0xF8`) on the same character-side object. Each ID is checked through the same predicate helper. Missing/present state drives application/removal of opposite members of the pair:

```text
paired helper true  -> 0x200000
paired helper false -> 0x400000
```

The exact semantic names of those two state IDs are not yet proven. The surrounding behavior, `buffEffect`/`stowedBuffEffect` assertions, and the explicit stowed-effect table show that this machinery belongs to generic active/stowed character-effect synchronization.

### Sources of the live-effect predicate bits

The two bits used by the live-effect predicate have different upstream producers:

```text
RVA 0x1105DE0  AvCharBase character-state update
    input flags bit 3
    -> RVA 0x11290B0
    -> AvCharEffect +0x3E0 bit 0x08

RVA 0x1108820  AvCharBase transformation-manager update
    active transform definition from transformation-manager virtual +0x10
    transformDef +0x48 mask 0x10
    -> RVA 0x112CD70
    -> AvCharEffect +0x3E0 bit 0x04
```

This proves that bit `0x04` is transformation-derived while bit `0x08` mirrors an
ordinary character-state flag. It does not yet establish the gameplay-facing name
of character-state bit 3 or transformation-definition flag `0x10`, so the native
contract keeps neutral bit names.

## 6. Chalice and Sun two-form hypothesis

Champion's Chalice and Champion's Sun are known to have two visible forms.

The `0x200000`/`0x400000` pair is a plausible mechanism **only if** those visible forms are tied to the character's active/stowed state represented by this generic machinery.

Current static evidence does **not** prove:

```text
0x200000 = Chalice/Sun form A
0x400000 = Chalice/Sun form B
```

nor the reverse.

It also does not prove that every two-form novelty uses this pair. Do not add novelty-specific enum names until a Chalice- or Sun-bearing `SkillDefinition` is shown to contain the paired application records.

## 7. World creation remains downstream

The actual effect creation still goes through:

```text
AvCharEffect dispatch RVA 0x1126680
    -> authored IEffectDef selected upstream
    -> mode 0x01 / 0x03 / 0x11 / 0x13
    -> EfCliContext wrapper RVA 0x1344B70
    -> EfCliContext core RVA 0x1344C50
```

The controlled Chalice activation used mode `0x11`. The `0x11`/`0x13` values must not be interpreted as the two Chalice forms: they are assembled from separate generic character/SkillDefinition predicates.

## 8. Next static targets

1. Identify semantic names for the two state IDs returned by the adjacent `+0xF0` and `+0xF8` virtuals.
2. Trace insertion/removal of the `AvCharEffect +0x98` paired-binding table back to the `SkillDefinition` install path.
3. Determine whether Chalice or Sun installs `ApplicationType 0x200000` and `0x400000` entries under one selector.
4. Decode remaining `StowedBuffEffectEntry` ownership/lifetime edge cases.
5. Keep runtime activation disabled until the world contract is complete.
