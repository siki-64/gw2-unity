# AsContext – target / selection / mouseover manager

**Confirmed build(s):** `204.132`, `204.489`, and `205.780`, scoped by the sections below.<br>
**Status:** build-local reconstruction combining static analysis and controlled live observations.<br>
**Unresolved:** behavior outside the listed build/section scopes and meanings explicitly marked unresolved.

`AsContext` is the static manager object that holds GW2's current **target**, **personal target**,
**selection**, and **mouseover** unit pointers. Discovered via the nameplate fade code (which reads
it to keep those units' names from fading — see
[`../InfoBars/name-rendering.md`](../InfoBars/name-rendering.md)), but it is a general selection
subsystem, not nameplate-specific. Symbols (`g_AsContext`, `AsContext_GetMouseover`, …) are defined in
the active Ghidra project; see [`re/methodology/ghidra.md`](../../methodology/ghidra.md); the authoritative field layout is
[`AsContext.cs`](../../../src/Gw2.Native/Context/AsContext.cs).

# The manager and its accessor

- **Class:** `AsContext` — named by the assert filename its vtable methods reference,
  `D:\...\Gw2\Game\AgentSelection\AsContext.cpp` (sibling names `dypContext`, `interact…` follow it).
- **Static singleton `g_AsContext`**, vtable `g_AsContextVtbl`.
- **Accessor `GetAsContext`** = `lea rax,[g_AsContext]; ret` — a trivial address-of-static (same shape
  as `GetSettings` and the widget id-table accessor; safe to resolve from a static dump, no
  heap/singleton init). Callers get the manager via `call GetAsContext`, then read fields / call vtable
  getters on it.

# State → field map (live differential + negative control, 2026-07-11)

Read the manager while setting one state at a time, diffed, then cleared all states as a negative
control. Every field below is **null when its state is inactive and holds a heap pointer to the
unit's agent/owner object when active** — confirmed bidirectionally against a clean cleared control.

| override state | keybind | `AsContext` field(s) |
|---|---|---|
| **mouseover** | hover | `+0x130` (mirror at `+0x1E8`) |
| **selection** | left-click | `+0x218`, `+0x298` |
| **personal target** | Ctrl+M3 | `+0x070`, `+0x1B8` |
| **primary picked** (whichever of selection / personal-target is current) | — | `+0x1A0` |
| **local player is the shared party call target** | another party member calls the local player | `+0x218` in the captured state |
| **local personal-target marker** | mark another unit locally | `+0x250` |
| **shared party call target** | another party member calls a unit | `g_AsContext +0x3A8 -> owner; owner +0x48` |
| **spectator camera follow** | lock spectator camera to a player | `+0x1A0`, `+0x218`, `+0x2B8` |
| **Take Target** | acquire another unit currently called by the party | `+0x098`, `+0x1A0`, `+0x1D0`, `+0x218`, `+0x298` |

Build `205.780` adds a second confirmed use for `+0x218`. While another party member had the local
player called, `+0x218` held the local player's unit and the red shared call-target reticle was visible;
primary-picked, personal-target, and `+0x250` were null. Moving the shared call target to another unit
cleared `+0x218` rather than storing the newly called unit. Therefore `+0x218` remains a local
selection-slot head but is not exclusive to ordinary left-click selection; it can also represent the
local player being the shared party call target. It is not a general pointer to whichever unit the
party currently has called.

The user confirmed that the game's **Take Target** action is unavailable when the local player is the
shared party call target. Therefore the captured `+0x218 = local player` state cannot be explained as
the result of locally taking/selecting the shared target; it is associated with the local player being
the called unit itself.

Build `205.780` then captured Take Target on a different party-called critter. Before the action, all
listed local target fields were null. Afterward the critter's unit appeared at `+0x098`, `+0x1A0`,
`+0x1D0`, `+0x218`, and `+0x298`; `+0x250` remained null. This confirms the user-observed behavior:
Take Target copies the party-called unit into ordinary local selection/primary-picked state rather than
writing the local call/personal-target marker at `+0x250`.

The shared source itself was located in build `205.780`. `g_AsContext +0x3A8` holds a pointer to an
owner object whose tracked-unit slot at `+0x48` contains the current shared party call-target `Unit*`.
A hardware watch across two different call-target transitions captured that field changing from the old
called unit to each new called unit while local selection was untouched. `SetSharedPartyCallTarget`
maps its incoming `agentId` through `AgWorld_GetUnitByAgentId` and assigns the returned `Unit*` to `this+0x48`
through `sub_254D70`. The owner's vtable constructor and the setter's assertions reference
`ChCliPlayer.cpp`, but that source string alone does not establish the native class name; the canonical
layout therefore retains the provisional `AsContext.Slot3A8` name.

Build `205.780` custom-arena spectator captures established another independent slot combination.
Locking the spectator camera to a blue-side player populated `+0x1A0`, `+0x218`, and `+0x2B8` with that
player's Character unit; unlocking cleared all three. Locking to a red-side player populated the same
three fields with the different red player's Character unit. `+0x098`, `+0x1D0`, `+0x250`, and
`+0x298` remained null throughout. No blue- or red-specific target slot was observed; this combination
tracks the spectator-followed player regardless of team.

The user then set that other, party-called unit as their local personal target. In this state `+0x250`
held the target's type-0 Character unit, while `+0x070`, `+0x1A0`, `+0x1B8`, and `+0x218` remained null.
This same-unit transition confirms that the local personal-target marker at `+0x250` is distinct from
the shared party call-target-on-local-player state observed at `+0x218`.

Method note: the negative control must be taken **after** the state is fully cleared — a snapshot
during clearing briefly shows selection fields still populated and reads as a false "never nulls".
The persistent fields at `+0x340`.. and `+0x388`.. stay populated in all states (not selection state).
`+0x340` is a **singly-linked list of per-unit nodes** (each node `{ key @+0, next @+8 }`):
`AsContext_FindTracked` (`vtbl[+0x160]`/`vtbl[+0x168]`) walks it looking for a node whose `key` equals
a queried unit pointer, asserting on `AsContext.cpp` (lines `0xB1A`/`0xB24`) — a registry of tracked
units, not the target/selection pointers themselves. Both are called by the InfoBar sub-widget
descriptor construction; `vtbl[+0x1F8]` in the same call sites is the shared no-op stub `sub_313990`.

# Tracked-unit slot array (`+0x130`..`+0x230`)

The single-state pointer fields above are the **heads of a contiguous array of `{value, next, prev}`
linked-list slots** (`value @+0`, `next @+0x8`, `prev @+0x10`), confirmed from `AsContext_Teardown`,
which unlinks and destroys each slot's occupant then nulls all three fields. Slot heads: `+0x130`
(mouseover), `+0x150`, `+0x168`, `+0x188`, `+0x1A0` (primary picked), `+0x1B8` (personal target),
`+0x1D0`, `+0x1E8` (mouseover mirror), `+0x200`, `+0x218` (selection/local-called-player), `+0x230`. This is why a field's
`+0x8`/`+0x10` neighbors move together in a differential (they are its list links, not separate
states). `+0x150` is a slot of this array, shape-identical to the mouseover slot, but the state that
populates it is unidentified (`AsContext_GetSlot150` returned null in eight live states); its semantic
setter is a shared per-slot register helper (both plain `mov [reg+0x150]` sites in the module are the
teardown and the constructor zero-fill), not yet traced.

# Vtable getters (`g_AsContextVtbl`)

Getters read directly out of the vtable (each is a leaf `mov rax,[rcx+disp]; ret`):

- **`AsContext_GetEffectiveTarget`** (`vtbl[+0x40]`) → reads `[this+0x368]`, and if null computes a
  fallback via `AsContext_GetEffectiveTarget2` (`vtbl[+0x78]`). `+0x368` was observed null in all four
  tested states; the fallback reads `+0x2B8` and then selection fallback `+0x298`.
- **`AsContext_GetSlot150`** (`vtbl[+0x60]`) = `[this+0x150]`. **Live-tested null in eight states**
  (baseline, selection, personal-target, call-target, mouseover, camera-center, in-combat-with-hostile,
  hostile-target; 2026-07-12) — its trigger is none of the states reachable in a training area. Next
  step: the static write-site scan (`mov [manager+150h], unit`), not more live state-guessing.
- **`AsContext_GetCallTarget`** (`vtbl[+0x70]`) = `[this+0x250]` = the local **call/personal-target
  marker** unit. It is distinct from the remote/shared party call-target state described above.
- **`AsContext_GetEffectiveTarget2`** (`vtbl[+0x78]`) = `[this+0x2B8]`, falling back to the selection
  field `[this+0x298]` — the fallback `AsContext_GetEffectiveTarget` calls.
- **`AsContext_GetMouseover`** (`vtbl[+0x88]`) = `[this+0x130]` — the **mouseover** unit.
- **`AsContext_GetSlot368`** (`vtbl[+0xE0]`) = `[this+0x368]`.

The fade short-circuit reads several of these per `Clamp01Fade` caller: `sub_5600F8` reads
`GetSlot150`, `GetEffectiveTarget`, `GetMouseover`; `sub_560CFD` reads `GetCallTarget`,
`GetEffectiveTarget`, `GetMouseover`; `sub_5612F7` reads `GetEffectiveTarget`, `GetMouseover` (each
comparing the render unit and jumping to the full-visibility path on a match — see
[`../InfoBars/name-rendering.md`](../InfoBars/name-rendering.md#distance-fade--overrides-clamp01fade)).

# Mouseover recomputation (build 204.489)

`RecomputeMouseover` copies the first pointer from each 24-byte record in the `AsContext +0x58`
candidate array, evaluates eligible entries through `ScoreMouseoverCandidate` and two other scoring
paths, retains the best-scoring pointer, and assigns it to the mouseover slot through `sub_254D70`.
It then compares the mouseover mirror at `+0x1E8` with the new `+0x130` value and updates the mirror
when they differ.

A hardware data-breakpoint capture on `+0x130` observed 26 null/non-null transitions. Every changed
value was written by `sub_254D70` with the immediate return site inside `RecomputeMouseover`. A
separate software-breakpoint capture hit `ScoreMouseoverCandidate` directly from that function's
candidate loop while visible nameplates were being hovered. These captures confirm the writer and
scorer handoff; the
meaning of the candidate array's other two fields and its relationship to draw eligibility are not
yet identified.

`AsContext_UpdateCursorRayHit` traces from the cursor-ray origin at `AsContext +0x310` in the
direction at `+0x2D8`, with a `10000.0`-unit endpoint. A query result supplies the position stored at
`+0x300`; no result stores positive infinity there. `RecomputeMouseover` computes the distance from
the origin to that position and passes it to `ScoreMouseoverCandidate`.

Build `205.780` further resolves an early classification gate in `ScoreMouseoverCandidate`. The scorer
calls `sub_11B0C10` through a context interface at vtable `+0xB8`. That dispatcher handles agent types
and tail-jumps through vtable `+0xB0` to `sub_11B0DE0` for a valid type-0 Character wrapper. The
returned presentation-category integer is stored by the scorer. When scorer field `+0xB0` has mask
`0x400` set, only the contiguous result range `3..5` survives this early gate; other values branch to
the scorer's common rejection path.

This classification gate precedes the world-obstruction cutoff below. Its numeric result is shared
with name-presentation classification, but that does not make mouseover eligibility an InfoBar-owned
decision.

For an ordinary type-`0` candidate that is not already one of the scorer's exempt target slots,
`ScoreMouseoverCandidate` rejects when the squared candidate-center distance exceeds the square of
the candidate radius plus that cursor-ray hit distance. The later type-`0` path still projects and
scores the candidate's model area, so this comparison is the earlier world-obstruction cutoff, not
the model-area hit test.

This cutoff was confirmed live on build `204.489`: changing only its compare operand so the
following unsigned-above branch could not be taken made an obstructed, still-drawn character
mouseoverable and clickable. The user confirmed the behavior worked throughout the test window;
the helper then restored and reread the original byte.

# Click consumption (build 204.489)

The click handler obtains its unit through `AsContext_GetMouseover`. An unobstructed click changed
the `AsContext +0x298` selection slot through `AsContext_SetSelection`. On an obstructed click over a
still-drawn nameplate, a function-entry trace instead observed `AsContext_SetSelection` receive null
from `AsContext_ClearSelection`; no non-null selection write occurred for that click. The click-time
mouseover was therefore null before selection handling. The obstruction-distance comparison in
`ScoreMouseoverCandidate` is the rejection point, as confirmed by the live bypass above.

Re-clicking an already combat-targeted unit while it is obstructed can leave the combat target
visible. This does not establish a successful obstructed mouseover: the same live trace observed the
null selection-clear call, and the combat-target and `+0x298` selection states are distinct.

# Selection-state consumers in the nameplate unit

Two leaf predicates test a unit against the manager and return a bool; a family of nameplate functions
call them to give **targeted / selected / mouseovered** units distinct treatment. The visible effect
(live A/B, golem selected vs not): the selected unit's name renders **brighter (white vs grey)** and
gains a **red underline** + **red target chevron**.

- **`IsTargetOrMouseover(unit)`** — `unit == AsContext_GetEffectiveTarget2()` (target `+0x2B8`/`+0x298`)
  → return 1, else `unit == AsContext_GetSlot368()` (`+0x368`) → return that bool. Null unit → 0.
- **`IsPrimaryPicked(unit)`** — does **not** use the manager; reads `DATA_FB61940` → `[+0x98]` →
  `call [+0x68]` → `call [+0xD0]` and compares the result to `unit`.

Consumers (each branches on one or both predicates):

| fn | uses | what it does |
|---|---|---|
| `sub_55E9E0` | `IsTargetOrMouseover` | saves the bool, combines it with the widget opacity fields `[+0xD0/+0xE0/+0xE4]` (the fade/opacity path) |
| `sub_55F0C0` | both | adds a screen-position offset to the widget when target/mouseover **and not** picked — a nameplate position nudge |
| `SelectAppearanceKey` (called only from `NameDraw`) | both + a type enum (`[unit+8]==0xA/0xB`) | selects a per-unit **appearance-key**: returns the *address* of one of five 8-byte key-hash slots, or null (normal unit) — full map below |

`NameDraw` is the selection-decoration and conditional level-text **draw**: it takes the
appearance-key from `SelectAppearanceKey`, sets the color (white default, attitude override), and
emits via `EmitDrawQuad`. So the confirmed chain is **selection state → predicate → appearance-key
(`SelectAppearanceKey`) → draw (`NameDraw`)**. Its second emitter call is not the general
visible-name path; build `204.489` live testing limited it to the level-text subset.

# Appearance-key map (`SelectAppearanceKey`, static disassembly 2026-07-11)

`SelectAppearanceKey` returns the address of an 8-byte slot in `k_AppearanceKeys`; the draw
dereferences it. The stored values are **hashed appearance/style resource IDs** (they drive the red
underline + target chevron decorations), **not colors**. Slot chosen by selection state:

| slot | hash | condition |
|---|---|---|
| `+0x00` | `0x01294A5D` | primary-picked **&&** `AsContext_GetCallTarget()==unit` |
| `+0x08` | `0x01294A5C` | **!**picked **&&** `AsContext_GetCallTarget()==unit` |
| `+0x10` | `0x010260F6` | primary-picked **&&** `AsContext_GetCallTarget()!=unit` |
| `+0x18` | `0x01026690` | target/mouseover (`IsTargetOrMouseover`), unit type `[unit+8]∈{0xA,0xB}` |
| `+0x20` | `0x0115F6D7` | target/mouseover, other type, `GetSettings`→`vtbl[+0x50]()` true |
| (null) | — | none of the above → non-special unit; the white-name block in `NameDraw` is skipped |

# Selection-marker emitter and occlusion

When `SelectAppearanceKey` returns a non-null key, `NameDraw` performs a dedicated first
`EmitDrawQuad` call for the selection/target/focus decorations. A conditional level-text call follows
separately. Skipping only the first call removed every selection/target/focus symbol while leaving
the unit name unchanged (live-confirmed, build `204.132`).

The decoration call normally passes the widget's nonzero `spaceFlag`. Forcing only this call's
`spaceFlag` to zero preserved the symbols' position, scale, and appearance while making them render
through world geometry. Unit names and healthbars were unchanged (live-confirmed, build `204.132`).

A residual jump-table path through `g_NameDrawAppearanceJumpTable` (`rbx = …vtbl[+0xB8]()`,
`rbx≤0xA`) refines the not-short-circuited cases; not fully expanded (moot for the color lever).

# Live differential — what selection actually changes on the nameplate (2026-07-11)

Enumerated all live `Tag30_Callback`-tagged nameplate widgets via the live framework recipe.
and diffed each widget + its value object (`g_ValueVtbl_shared`) for the **same unit** selected vs
not, holding the camera still. The golem's value object was identified unambiguously by the embedded
UTF-16 string **`Target Golem - Medium`** inside it (nameplate value objects carry their own name
text) — a reliable way to pick a specific unit's widget without a unit-pointer field.

**Confirmed persistent selection state.** On the selected unit's value object, a block flips: `+0xEF`
(`0x88↔0x80`), `+0xFC` (`0x004F0810↔0`), and a float block at **`+0x104..+0x11F`** that is
**populated when DESELECTED and zeroed when SELECTED**. Deselected floats parse as
`{0.956, ~0, -0.292, -18.79, ~0, 1.0, ~0}` — `0.956² + 0.292² ≈ 1`, i.e. a **unit vector + offset +
scale**, a billboard/fade transform (not an RGBA — zero would be black, but the selected name is
white). This is consistent with the confirmed fade short-circuit: unselected nameplates run the fade
path (which fills this transform scratch); selected units **skip** fade (`sub_55E9E0`'s
`IsTargetOrMouseover`), leaving it zeroed.

**Font brightness is NOT a persistent field** — see
[`name-rendering.md`](../InfoBars/name-rendering.md#name-color-inputs). It never appeared
in the value object (0x120 B) or widget (0x400 B) across the selected/deselected diff, so the
grey→white boost is computed **per-frame at draw** (`NameDraw`→`ResolveAttitudeColor`→`EmitDrawQuad`).
But the exact color turned out to be readable from **static disassembly** — no draw-time breakpoint
needed (see next section).

# Draw-time name color: white default + attitude override (static, 2026-07-11)

Read directly from `NameDraw` / `ResolveAttitudeColor`. The special-unit draw sets the name text color
as follows:

- **Default = pure white `NameRenderingConstants.ColorWhite`** (`0xFFFFFFFF`), an *immediate* written into a stack color
  slot (twice — once per decoration pass). This color is passed by pointer into `EmitDrawQuad`,
  alongside the appearance-key and the style descriptor `k_NameStyleDesc`.
- **Conditional attitude override.** Between the white init and the emit, `ResolveAttitudeColor` is
  called **only when** the per-unit predicate `GetSettings`→`vtbl[+0x50](obj, unit)` returns true.
  When it fires, it overwrites the color slot with the unit's **attitude/team color**; otherwise the
  name stays white.

`ResolveAttitudeColor(unit, &colorOut)` resolves the attitude color: reject if `[unit+8]!=0`; get
attitude enum via `vtbl[+0x310]`; look it up through the UI color manager (`DATA_FB61940` → `[+0xE0]`
→ `vtbl[+0x58](0x190, attitudeId)`) → color entry; read `[entry+0x60/+0x61/+0x62]` = R/G/B and write
`{R,G,B,0xFF}` to `&colorOut`. If the entry is null or RGB is all-zero (no `[entry+0x58]` float
fallback), it returns **without touching** `colorOut`, leaving the white default.

**So the "brightness boost" is not a computed value — it is `NameRenderingConstants.ColorWhite`**, applied because special
units render through `NameDraw` (white base) instead of the normal distance-dimmed name path
(`NameRender`, the `k_DimCap` ramp — see [`../InfoBars/name-rendering.md`](../InfoBars/name-rendering.md)).
Grey is therefore *not* a constant here: it is either the neutral **attitude** color from that table
or the **distance-dimmed** white of the normal path — there is no separate "grey" literal to capture.

**Residual (open, minor):** whether `EmitDrawQuad` further modulates font brightness/alpha from the
appearance-key argument itself (beyond the white base tint) is not traced — `EmitDrawQuad` is a large
generic text emitter. The base-color lever (`NameRenderingConstants.ColorWhite`) is confirmed; a per-key brightness
modifier inside the emitter, if any, is not.

# Why this matters

- **Single static root, no pointer chain.** `g_AsContext` + a trivial accessor (`GetAsContext` =
  `lea; ret`) gives every override-unit pointer directly, without walking a chain.
- **Explains the nameplate-fade override.** The "targeting/hovering/selecting stops fadeout" behavior
  is these getters feeding the fade short-circuit.
