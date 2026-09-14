# Patch resilience

Live patching must assume addresses and layout can move between GW2 builds.

The goal is not to hardcode addresses; it is to resolve the intended native contract reliably and fail closed when that identity cannot be established.

# Core rules

- Resolve patch targets at runtime from durable locators.
- Treat cached addresses as build-specific accelerators, not identities.
- Prefer the smallest locator surface area that remains uniquely identifying.
- Wildcard volatile bytes such as relative branch displacements, RIP-relative displacements, and absolute-address material.
- Anchor on instruction structure and stable logic where possible.
- Prefer a stable nearby anchor plus a validated walk/offset when the exact patch bytes are not themselves a good durable signature.
- Resolve RIP-relative displacements instead of hardcoding the resulting global/data address.
- Validate the resolved site before writing.
- Ambiguous or missing resolution must skip the patch rather than guess.
- Logical patch groups should apply atomically when all required sites belong to one feature.

# Resilience hierarchy

Choose the smallest stable surface that uniquely establishes the target.

1. **Resiliently located data/state write**
   - Patch a constant or field the code actually reads.
   - Locate it through a durable data signature or code-derived structure reference.
   - Use only when the decision is truly data-driven.

2. **Minimal unique code signature**
   - Small AOB/signature that is unique across the intended scan scope.
   - Include only the bytes needed to establish identity.

3. **String/xref or structural anchor**
   - Anchor to a durable string, call relationship, vtable, or nearby structural feature.
   - Resolve the final patch location through validated code structure.

4. **Large flat signature / collision-prone pattern**
   - Avoid when a smaller structural locator is available.

5. **Fragile pointer/base-offset chain**
   - Use only when no better code/data locator exists.

6. **Hardcoded build-relative or absolute patch location**
   - Not a durable production locator.

A known fixed instruction encoding is not itself forbidden. For example, once a site is durably located and validated, a verified 5-byte `E8 rel32` call can be patched with direct encoding logic.

# Validation and failure behavior

Every patch site must validate the native state it expects before modification.

Check as applicable:

- exactly one intended locator result;
- expected instruction/opcode;
- expected original bytes;
- patch length;
- branch reachability;
- RIP-relative target;
- trampoline allocation constraints;
- calling convention/register contract.

On failure:

- skip the patch;
- leave or restore the target unchanged when possible;
- do not continue from guessed addresses or instruction boundaries;
- retain diagnostics through the project's existing mechanism.

Patch failures must not propagate across the proxy/native boundary or terminate the game.

# Hooks and trampolines

Hooks must preserve the machine-state contract of the replaced path.

Account for:

- argument registers;
- return value;
- volatile and nonvolatile registers;
- stack alignment;
- Windows x64 shadow space;
- flags when relevant;
- overwritten instructions;
- relocation of overwritten instructions;
- return-address expectations.

General instruction relocation should use `X64Relocator`.

Do not claim a native renderer/function ABI is recovered until the required register/stack/state contract has been established.

# Patch-site ownership

A patch definition should describe:

- the durable symbol/locator identity;
- what native behavior is being modified;
- what bytes/state are expected;
- how the patch is applied/restored;
- what failure means.

Runtime feature code should not need to know absolute addresses.

# Known production limitation

`Camera/DisablePositionInterpolation` has a confirmed patch-induced camera/geometry clipping regression on game build `204.489`.

With the patch active, jumping on staircase geometry can make the camera visibly enter/cross the stairs and then snap back out. The same movement did not reproduce with the patch disabled.

The responsible internal transition has not been isolated. The forced direct path updates `WvCamera.m_position` and takes the snap-flag path for the secondary vector pairs, but current evidence does not establish which update or collision-processing stage places the view inside geometry.

No mitigation is currently confirmed. Disproved mitigation attempts belong in [`../notes/RE_NOTES.md`](../notes/RE_NOTES.md).
