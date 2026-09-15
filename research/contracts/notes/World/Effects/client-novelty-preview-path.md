# Champion novelty preview path — build 207.032

> **PREVIEW PANEL ONLY.** This note describes the Hero/Equipment toy preview path. None of the preview `IEffectDef` pointers or preview timing/placement fields are substitutes for the in-world Buff/`SkillDefinition` path.

## Shared semantic ancestor

Static backtracking now identifies a common ancestor above the previous `CnContext`/`EfCliContext` crossing points:

```text
ChCliWardrobe.GetEquippedToy (+0x250)
        |
        v
ToyDefinition  (content class 0x019A)
  +0x28 DefinitionId
        |
        +----------------------------+
        |                            |
        v                            v
EqpPageToy / Hero preview       normal toy activation
local preview data             SbToySlot -> wardrobe +0x2B8
        |                            |
        v                            v
CPaperDoll::PlayEffect          uint16 0x00F0 + uint8 toySlot
                                      |
                                      v
                                  server resolve
                                      |
                                      v
                              CmbtCli Buff receive state
```

The common object is the **toy definition**, not a shared effect definition. Preview and in-world effect authoring split after this point.

### ToyDefinition identity

`ChCliWardrobe.cpp` explicitly asserts `toyDef != nullptr`, reads `toyDef +0x28`, and sends that 16-bit-safe numeric definition id in a separate `0x00EF` wardrobe toy-definition selection request. A receive-side wardrobe path resolves content class `0x019A` back to a toy definition by numeric id. This is separate from the `0x00F0` toy-slot activation request.

Recovered partial layout:

```text
ToyDefinition
+0x28 uint32 DefinitionId
+0x70 ToyPreviewEffectEntry* PreviewEffects
+0x78 uint32 PreviewEffectCount
+0x80 float PreviewScale
+0x84 float PreviewAngleDegrees
+0x88 float PreviewParameter88
```

## Authored preview sequence

`EqpPageToy.cpp` RVA `0x5D2940` indexes `ToyDefinition +0x70` with a stride of `0x18` and forwards the selected record to the paper doll.

```text
ToyPreviewEffectEntry  // 0x18 bytes
+0x00 IEffectDef* EffectDefinition
+0x08 float ScaleMultiplier
+0x0C float NextUpdateMilliseconds
+0x10 float AngleDegrees
```

The field names above are supported by static behavior:

- `+0x00` is passed as the effect-definition argument of the paper-doll virtual `+0x28` call.
- that virtual call matches the recovered `CPaperDoll::PlayEffect` signature, so `+0x00` is an actual preview `IEffectDef*`.
- `+0x0C` is multiplied by `0.001f` in the sequence scheduler. The ArenaNet assertion at that site is `nextUpdate >= 0`.
- `+0x10` is multiplied by `0.017453292f` (`pi / 180`) immediately before the PlayEffect call.
- `ToyDefinition +0x84` is also converted from degrees to radians during paper-doll setup.

The page advances the sequence index when more than one preview entry exists, confirming that a toy can author multiple timed preview effects.

## Known preview sample

Champion's Laurels preview remains the known captured sample:

```text
IEffectDef id: 45777 / 0xB2D1
content key: 49 B7 88 FF 6B CF 00 40 8A 98 99 21 0E EB D3 36
```

Nothing in this static trace maps that preview definition to an in-world Laurels definition.

## Separation from in-world authoring

The two arrays are unrelated structures:

```text
PREVIEW
ToyDefinition +0x70
    -> ToyPreviewEffectEntry[0x18]
    -> preview IEffectDef*
    -> CPaperDoll::PlayEffect

WORLD
SkillDefinition +0x40
    -> SkillEffectApplicationEntry[0x20]
    -> world IEffectDef*
    -> AvCharEffect
    -> EfCliContext
```

Do not copy preview entry flags/timing/placement into the world path, and do not infer world `IEffectDef` identity from the preview sequence.
