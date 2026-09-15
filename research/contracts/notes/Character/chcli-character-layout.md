# ChCliCharacter native layout

**Confirmed build(s):** `207.032`.<br>
**Status:** build-local native layout reconstruction supported by allocation, destruction, and subobject evidence.<br>
**Unresolved:** fields explicitly marked unresolved below and cross-build validity.<br>
**Address scope:** absolute analysis VAs are build-local evidence; active symbols and addresses belong in Ghidra.

Build target: attached `Gw2-64.exe` aligned structurally with the build-207.032
symbol set.

`ChCliCharacter` is a native `0xB08`-byte object. Its allocation size is
confirmed both by the creation path and the matching deleting-destructor path.

## World-state payload lookup chain

`ContextCollection` is the world-state payload pointer lookup table itself.
The native TLS accessor returns that table base directly:

```text
tlsArray = gs:0x58
tlsBlock = tlsArray[g_tlsIndex]
ContextCollection* = *(tlsBlock + 0x10)
```

The already-recovered `ContextCollection +0x98` entry points to the
`ChCliContext` payload. The character graph then continues:

```text
ContextCollection
    -> ChCliContext*
       -> +0x60 ChCliCharacter** Characters
       -> +0x68 CharacterCapacity
       -> +0x6C CharacterCount
       -> +0x80 ChCliPlayer** Players
       -> +0x98 ChCliCharacter* LocalCharacter

ChCliPlayer
    -> +0x18 ChCliCharacter*
```

So `ChCliCharacter` is not embedded in the payload lookup table. It is reached
through the pointed-to `ChCliContext` world-state payload and its sparse
registries.

## Top-level ChCliCharacter map

| offset | span | recovered state |
|---:|---:|---|
| `+0x000` | `0x50` | primary/embedded interface tables |
| `+0x050` | `0x48` | embedded interface/base state with tagged sentinel at `+0x70` |
| `+0x098` | `0x08` | `Agent*` |
| `+0x0A0` | `0x08` | scalar/padding |
| `+0x0A8` | `0x08` | native `m_speciesDef` |
| `+0x0B0` | `0x10` | unresolved state |
| `+0x0C0` | `0x04` | `m_attitudeTowardControlled` |
| `+0x0C4` | `0x1C` | unresolved state |
| `+0x0E0` | `0x30` | `m_combatantNotifyList` |
| `+0x110` | `0x08` | `m_compositeData` |
| `+0x118` | `0x30` | three allocator-backed references |
| `+0x148` | `0x30` | allocator-backed native container |
| `+0x178` | `0x08` | native 64-bit `m_flags` |
| `+0x180` | `0x18` | scalar/pointer state |
| `+0x198` | `0x30` | allocator-backed native container |
| `+0x1C8` | `0x18` | fixed state |
| `+0x1E0` | `0x30` | native notification/list object |
| `+0x210` | `0x18` | scalar/pointer state |
| `+0x228` | `0x08` | `m_renownSubRegion` |
| `+0x230` | `0x04` | scalar initialized to `0x27` |
| `+0x234` | `0x10` | `m_skillChallengeBitGuid` |
| `+0x244` | `0x04` | unresolved |
| `+0x248` | `0x08` | mixed-width native state |
| `+0x250` | `0x04` | float initialized to `1.0f` |
| `+0x258` | `0x08` | pointer/state |
| `+0x260` | `0x28` | species-derived cache |
| `+0x288` | `0x20` | species/scalar state |
| `+0x2A8` | `0x20` | dynamic float collection |
| `+0x2C8` | `0x04` | cached maximum for `+0x2A8` |
| `+0x2CC` | `0x04` | float sentinel/cache |
| `+0x2D0` | `0x08` | overlapping scalar state |
| `+0x2D8` | `0x20` | second dynamic native collection |
| `+0x2F8` | `0x30` | overlapping update/snapshot state |
| `+0x328` | `0x08` | unresolved |
| `+0x330` | `0x58` | inline `ChCliChatter` |
| `+0x388` | `0x08` | `ChCliCoreStats*` |
| `+0x390` | `0x40` | inline vtable + notification-list subsystem |
| `+0x3D0` | `0x08` | `ChCliEndurance*` |
| `+0x3D8` | `0x08` | `ChCliEnergies*` / native `m_energyMgr` |
| `+0x3E0` | `0x08` | `ChCliAdventure*` |
| `+0x3E8` | `0x08` | `ChCliHealth*` |
| `+0x3F0` | `0x08` | `ChCliInventory*` |
| `+0x3F8` | `0x08` | `ChCliKennel*` |
| `+0x400` | `0xB0` | inline `ChCliMovement` |
| `+0x4B0` | `0x60` | inline `ChCliOrder` |
| `+0x510` | `0x08` | polymorphic `ChCliProfession*` |
| `+0x518` | `0x08` | `PvpGearProvider*` |
| `+0x520` | `0x08` | native `m_skillbar` / `ChCliSkillbar*` |
| `+0x528` | `0x20` | tagged intrusive sentinel/container |
| `+0x548` | `0x5B8` | inline `ChCliTransformation` |
| `+0xB00` | `0x08` | `ChCliWardrobe*` |
| `+0xB08` | — | native end |

## Embedded subobjects

### ChCliMovement

`ChCliCharacter::Ctor` constructs `ChCliMovement` with
`this = character + 0x400` and passes the owning character as the second
argument. Its vtable is `0x142171C88`; the source-string neighborhood is
`ChCliMovement.cpp`. The following `ChCliOrder` begins at `+0x4B0`, so
the inline movement object is exactly `0xB0` bytes.

### ChCliOrder

`ChCliCharacter::Ctor` constructs the separate object at `+0x4B0` with
constructor `0x14127A000`. It installs vtables `0x142172940` and
`0x142172B88`; the source-string neighborhood is `ChCliOrder.cpp`.
`ChCliProfession*` begins at `+0x510`, establishing size `0x60`.

### ChCliTransformation

The constructor builds the inline object at `+0x548`.
After a 0x40-byte header it invokes the same record constructor six times at:

```text
+0x040
+0x128
+0x210
+0x2F8
+0x3E0
+0x4C8
```

The stride is `0xE8`, giving six transformation records ending at
`+0x5B0`. The character's next top-level member is the wardrobe pointer at
`+0xB00`, so the transformation object is exactly `0x5B8` bytes.

## Owned subsystem allocation extents

The character destructor supplies several hard native allocation sizes:

| field | native allocation |
|---|---:|
| `ChCliCoreStats*` | `0x288` |
| `ChCliEndurance*` | `0x68` |
| `ChCliAdventure*` | `0x100` |
| `ChCliHealth*` | `0x88` |
| `ChCliInventory*` | `0x490` |
| `ChCliKennel*` | `0x1B8` |
| `PvpGearProvider*` | `0xD8` |
| `ChCliWardrobe*` | `0x708` |

`ChCliProfession*` is polymorphic. The common profession base constructor and
deleting destructor establish a `0x48` base object; concrete profession
implementations may extend it.

## Boundaries

The following distinctions are intentional:

- `ContextCollection` is the world-state payload pointer table itself.
- `ChCliContext` is one pointed-to payload object from that table.
- `ChCliContext.Characters` is the sparse registry that stores
  `ChCliCharacter*` entries.
- `ChCliPlayer` is a separate player payload/wrapper and links to a character
  at `+0x18`.
- pointer members in `ChCliCharacter` are not inline objects.
- inline `ChCliMovement`, `ChCliOrder`, `ChCliChatter`, and
  `ChCliTransformation` have independent ctor/dtor lifetimes inside the
  character allocation.
