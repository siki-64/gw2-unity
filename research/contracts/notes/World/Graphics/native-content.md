# Native content and UI texture lookup

**Confirmed build(s):** unknown; confirmed image is the supplied `Gw2-64.exe` dated 2026-08-19 for the MaterialCache anchor below.<br>
**Status:** native skill/item/icon-to-texture path is functionally observed; exact ArenaNet object layouts remain partial.<br>
**Unresolved:** content-root structure, skill/item icon-handle offsets, texture-store entry layout, texture-manager ownership, and stable SRV translation.

## High-level lookup path

A native icon consumer resolves supported icons without installing a render hook. Its observed stages are:

`validated game TLS/content root -> global skill/item content lookup -> skill/item native object -> native icon handle / remembered file id -> native texture store -> texture index record -> texture manager -> D3D11 shader-resource view`.

The implementation independently tracks rediscovered addresses corresponding to `native_content_lookup_rva`, `native_store_global_rva`, and `native_texture_manager_rva`.

This is useful evidence for the missing bridge between `ItemDefinition` / `SkillDefinition` and the `GrTex` / `GrTex2d` family already modeled in `Gw2.Contracts`.

Do not expose the final D3D11 SRV path as the native UI renderer abstraction. The more durable target is the ArenaNet content/texture/material layer above it.

## Current-build MaterialCache anchor

One embedded texture-related signature matches exactly once in the supplied current image at `RVA 0x0321A20`:

`48 89 5C 24 20 55 57 41 56 48 81 EC 80 00 00 00 83 79 08 00`

The function's assertion/source references identify `Gw2\\Game\\Ui\\Services\\MaterialCache\\McTexture.cpp` and `m_isInitialized`.

The first branch tests a dword at `this + 0x08` as `m_isInitialized`.

This establishes a concrete current-build `McTexture`/MaterialCache reconstruction target, but the function's exact original name and full object layout are not yet recovered. Keep `+0x08` as MaterialCache-local evidence until the owning class/constructor is identified.

## Relationship to `GrTex` / `GrTex2d`

The existing partial graphics types model ArenaNet texture objects. The native content path adds a second layer: `content definition -> icon/file handle -> UI MaterialCache / texture store -> ArenaNet texture/backend resource`.

The exact handoff between MaterialCache entries and `GrTex2d` is still unresolved.

This is the right place to continue the native UI image primitive work: recover how ArenaNet turns a content/file/icon handle into a material/texture accepted by the same native submission path used by `EmitDrawQuad`, rather than translating to an SRV and drawing it through an addon D3D backend.

## Validation behavior

Observed native-icon validation rejects invalid/missing content roots, ID mismatches, missing icon handles, missing/non-resident texture records, invalid texture indices/managers, and invalid SRV candidates.

The presence of explicit ID revalidation is a useful ownership/lifetime clue: content lookup results should not be treated as durable pointers across arbitrary game-state changes.

## Next steps

- recover the `McTexture` constructor/service owner around the current-build `RVA 0x0321A20` target;
- locate the global skill/item lookup function and derive `ItemDefinition`/`SkillDefinition` icon fields;
- recover the texture-store/index record layout;
- connect a resolved UI texture to `GrTex2d` or directly to the native material used by `EmitDrawQuad`;
- prefer native material submission over exposing backend SRVs to modules.
