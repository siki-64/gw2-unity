# World-view context

**Confirmed build(s):** unknown; confirmed image is the supplied `Gw2-64.exe` dated 2026-08-19.<br>
**Status:** singleton getter/global identity recovered; object layout remains unresolved.<br>
**Unresolved:** `WvContext` fields, camera-manager relationship in the current build, ownership, and lifecycle.

## `WorldViewInt::s_context`

The current client contains assertion/source references to `Gw2\\Game\\WorldView\\WvContext.cpp` and `WorldViewInt::s_context`.

A compact getter begins at image RVA `0x09908A0`. The load at `RVA 0x09908A4` resolves the singleton storage to image RVA `0x2677390`.

The byte sequence `48 85 C0 75 20 41 B8 2E 04 00 00` matches exactly once in the supplied image at `RVA 0x09908AB`, inside that getter.

Use the function/source identity rather than the raw byte signature as the durable RE anchor.

## Independent world-view scanner evidence

A separate live client implementation also resolves a world-view context through a RIP-relative global and treats a nearby static-field location at `+0x88` from that global block as a camera-manager pointer.

That `+0x88` relationship is **not** promoted to a `WvContext` field: the observed code addresses the static/global block, not `s_context + 0x88`.

## Relationship to existing camera RE

The repository already models `WvCamera`, but does not yet model the owning world-view context or camera manager.

Keep these layers distinct: `WorldViewInt static/global state -> WvContext* -> unresolved manager/context graph -> WvCamera`.

Do not infer that the current `WvCamera` pointer is stored directly inside `WvContext` until current-image constructors/accessors establish that relationship.

## Next steps

- label the singleton getter and `WorldViewInt::s_context` storage in Ghidra;
- find xrefs to the getter and classify returned-context consumers;
- recover the current-build camera-manager accessor/global rather than importing a historical offset;
- connect world-view state to the existing native projection path used by InfoBars.
