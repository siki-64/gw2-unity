# Native vtable inventory

**Confirmed build(s):** `205.780`.<br>
**Status:** build-local live vtable inventory; entries are recorded only where the object evidence supports them.<br>
**Unresolved:** uninspected slots, remaining overlay tables, and cross-build validity.<br>
**Address scope:** listed analysis VAs are evidence coordinates, not durable addresses; Ghidra is authoritative.

Build `205.780` live captures were taken with `debug vtable` while the game was
running. The addresses below are analysis VAs (`0x140000000 + module RVA`),
not process VAs, and are retained as build-local evidence rather than an active
address cache. A table is recorded only when its object header or embedded
subobject supplied a module-backed vtable; the remaining native overlays keep
their table fields but intentionally have no confirmed Ghidra identity yet.

Confirmed table identities:

- `ChCliCharacterVtable` `0x142159780`, with its secondary interface table
  `ChCliCharacterInterfaceVtable`.
- `ChCliMovementVtable` `0x142171C88`; the inline object begins at
  `ChCliCharacter +0x400` and retains the owning character at movement `+0x008`.
- `ChCliOrderVtable` `0x142172940` and secondary
  `ChCliOrderInterfaceVtable` `0x142172B88`; the inline order object begins
  at `ChCliCharacter +0x4B0`.
- `ChCliTransformationVtable` `0x142172F40`; the inline transformation
  object begins at `ChCliCharacter +0x548`.
- `ChCliAdventureVtable` `0x142159710`; the character-owned adventure
  allocation is `0x100` bytes.
- `ChCliCharacterContextInterfaceVtable` `0x14215BAE0`.
- `ChCliSpecializationVtable` `0x1421640C0` and
  `ChCliSpecializationProgressListenerVtable` `0x142164148`.
- `ChCliCoreStatsVtable` `0x142161940` and
  `ChCliCoreStatsInterfaceVtable` `0x142161A20`.
- `ChCliInventoryVtable` `0x14215BDC8` and
  `ChCliInventoryInterfaceVtable` `0x14215C370`.
- `ChCliItemStorageEquipmentVtable` `0x142173608`, `ChCliProfessionVtable`
  `0x142166D68`, and `ChCliProgressVtable` `0x142165550`.
- `ChCliHealthVtable` `0x142161898`, `GdCliContextVtable` `0x14223D1E0`,
  `ItCliContextVtable` `0x142254910`, and `ChCliContextVtable`
  `0x142156B78`.
- `PvpCliContextVtable` `0x1422E0740` and
  `PvpCliContextQueryInterfaceVtable` `0x1422E1150`.

The live dumps showed all inspected slots for these tables pointing into the
main module's executable text. The `ChCliPlayer` repair remains separate:
`GetSpecializationManager` is slot `+0x30`, returning `this + 0x9CB8`.
