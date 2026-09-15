# Native profession state

**Confirmed build(s):** `207.032`.<br>
**Status:** build-local native state and selected relationships confirmed by live reads and structural evidence.<br>
**Unresolved:** semantics of the remaining unresolved fields and cross-build validity.


`ChCliCharacter +0x510` points to the character's native profession object.
Its `ChCliProfession +0x40` field is `ECharProfessionState`. The field is
updated by the native profession-state dispatcher before its notification is
sent. The state is native memory data; it must not be identified by comparing
it directly with Mumble or public API values.

## Confirmed states

| Native state | Meaning | Native table entry |
| ---: | --- | ---: |
| `0x01` | Elementalist Fire attunement | n/a |
| `0x02` | Elementalist Water attunement | n/a |
| `0x03` | Elementalist Air attunement | n/a |
| `0x04` | Elementalist Earth attunement | n/a |
| `0x06` | Guardian Luminary Shroud | n/a |
| `0x07` | Necromancer shroud state | n/a |
| `0x08` | Thief Specter Shroud | n/a |
| `0x0F` | Revenant Glint | `0x211` |
| `0x10` | Revenant Shiro | `0x212` |
| `0x11` | Revenant Jalis | `0x213` |
| `0x12` | Revenant Mallyx | `0x214` |
| `0x13` | Revenant Kalla | `0x215` |
| `0x14` | Revenant Ventari | `0x216` |
| `0x15` | Revenant Alliance stance | `0x217` |
| `0x16` | Revenant Razah | `0x218` |

The Revenant legend range is validated natively as `0x0F..0x16`. Kalla is
inferred from the exhaustive eight-legend set and the one remaining state;
the other legend names were confirmed by direct live reads. The legend table
at static RVA `0x25B56C0` maps the eight native states to entries `0x211`...
`0x218`.

For shrouds, the field reads `0x00` outside the shroud and changes while the
shroud is active. Reaper Shroud and Ritualist Shroud both produced
`0x00 -> 0x07 -> 0x00`, so this field does not distinguish those Necromancer
shroud variants. Luminary Shroud produced `0x06`; Specter Shroud produced
`0x08`.

The native symbols may contain the older internal `Reckoner` name for the
Revenant implementation. That is an internal legacy symbol, not the UI name.

## Base layout and neighboring links

The common `ChCliProfession` constructor initializes a
`0x48`-byte polymorphic base:

| offset | field |
| ---: | --- |
| `+0x00` | primary vtable |
| `+0x08` | 0x30-byte notification list |
| `+0x38` | owning `ChCliCharacter*` |
| `+0x40` | `ECharProfessionState` |
| `+0x44` | unresolved tail dword |

The matching deleting destructor frees exactly `0x48` bytes. A vtable observed
at `+0x48` in a live profession instance therefore belongs to a
profession-specific extension/derived object and must not be modeled as a
member of the common base.

The following relationships were observed from the live `ChCliContext` ->
`ChCliCharacter` -> subsystem chain at build `207.032`:

| Location | Observation | Confidence |
| --- | --- | --- |
| `ChCliCoreStats +0x250` | Exact pointer equality with `ChCliCharacter +0x3F0` (`Inventory`) | Confirmed |
| `ChCliCoreStats.Unknown258` (`+0x258`) | Exact pointer to `ChCliCharacter +0x548` (`ChCliTransformation` base) | High |
| `ChCliCharacter +0x400` | Inline `ChCliMovement`, with owner-character pointer at movement `+0x08` | Confirmed |
| `ChCliCharacter +0x4B0` | Inline `ChCliOrder` object | Confirmed |

The profession pointer at `ChCliCharacter +0x510` should therefore be read as a
pointer to the polymorphic profession base. Concrete profession implementations
may have additional state beyond the common `0x48` bytes.
