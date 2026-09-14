# Healthbar rendering (`AsHealth`)

**Confirmed build(s):** `205.655` and `205.780`, scoped by the sections below.<br>
**Status:** build-local rendering and record-layout reconstruction supported by static and live evidence.<br>
**Unresolved:** gameplay names, incomplete renderer ABI details, and cross-build validity.

`AsHealth` is the InfoBar healthbar sub-widget:

```text
Gw2\Game\Ui\Widgets\AgentStatus\AsHealth.cpp
```

It is a distinct widget class. It does **not** render from a generic health float stored on the parent
InfoBar. Every draw reads per-agent display state from `g_CombatTracker`.

This document owns the confirmed `AsHealth` draw path, CombatTracker record layout, resource/color
selection, and the shared native emitter boundary. Healthbar occlusion policy is documented in
[`occlusion.md`](occlusion.md). Character-side authoritative health is documented in
[`../Context/context-and-health.md`](../Context/context-and-health.md).

Symbols are maintained in the active Ghidra project; see [`methodology/ghidra.md`](../../methodology/ghidra.md). Authoritative
reconstructed layouts live in [`AsHealthWidget.cs`](../../Gw2.Contracts/InfoBars/AsHealthWidget.cs),
[`CtRecord.cs`](../../Gw2.Contracts/InfoBars/CtRecord.cs), and
[`InfoBarConstants.cs`](../../Gw2.Contracts/InfoBars/InfoBarConstants.cs).

# Overview

The healthbar path is:

```text
InfoBarUpdate
    ↓
AsHealth creation / maintenance
    ↓
AsHealthMsgThunk
    ↓
AsHealthOnMessage
    ↓
AsHealthDraw
    ↓
CombatTracker CtRecord + agent state
    ↓
AsHealthSelectColors
    ↓
EmitDrawQuad
    ↓
native render queue
```

The important architectural distinction is:

```text
InfoBar
    presentation container / visibility state

AsHealth
    dedicated healthbar widget

CombatTracker CtRecord
    draw-time display values consumed by AsHealth
```

`AsHealth` must not be documented as reading a generic `InfoBar.field_CC` value for the healthbar fill.

# Creation and message dispatch

`InfoBarUpdate` creates and maintains `AsHealth` as part of the normal InfoBar Stage-3 path.

Confirmed registration facts:

- component slot: `2`
- config tag: `0x10030` or `0x10830`
- callback: `AsHealthMsgThunk`

`AsHealthMsgThunk` is a one-instruction thunk:

```asm
jmp AsHealthOnMessage
```

`AsHealthOnMessage` contains a 75-case jump table.

`AsHealth` is registered at **four** known sites:

- three registrations in the normal InfoBar unit;
- one registration outside that unit in unrelated UI.

This matters for instrumentation: patching the shared callback or broad AsHealth behavior can affect
non-world UI. The fourth registration must not be labeled as spectator UI without direct proof.

# Widget state

`AsHealthOnMessage` allocates the draw handler as a `0x78`-byte `AsHealthWidget`. The component's
framework frame is a separate allocation; `AsHealthFrameView` begins at `FrApiFrame + 0x20`.

Two adjacent **frame-view** fields have different roles and must remain distinct:

| offset | field | role |
|---|---|---|
| `+0x27C` | `AsHealthFrameView.stateFlags` | runtime widget state |
| `+0x280` | `AsHealthFrameView.configTag` | static creation/config tag |

The runtime occlusion state is:

```text
AsHealthFrameView.stateFlags & 0x1000
```

The config values include:

```text
0x10030
0x10830
```

`stateFlags & 0x1000` is not the config tag. See [`occlusion.md`](occlusion.md) for the visibility and
through-wall behavior associated with this runtime bit.

# Draw entry

The draw case in `AsHealthOnMessage` calls `AsHealthDraw`.

`AsHealthDraw` receives its agent from:

```text
AsHealthWidget.agent
```

This is the healthbar's **draw-time agent reference**. It must not be silently equated with:

- the fresh update-time `agent` passed to `InfoBarUpdate`;
- `InfoBar.unit` at `+0xA0`;
- `NameRenderCtx.agent`.

The draw path uses `AsHealthWidget.agent` for type-specific resolution, world-anchor lookup, and
CombatTracker indexing through `[agent+0x0C]`.

# Selected-target health panel

The selected-target panel has a separate control hierarchy from the world InfoBar healthbar. The
following correlation is confirmed for the live `205.780` process and must remain build-scoped.

## Target control and child frame

The current target was rebuilt through the character-style setup function `sub_1403A4F10`. Its static
registration sequence at RVA `0x3A5103` is:

```text
sub_14106B5D0(
    parentFrameId,
    0x430,
    callback = AsHealthOnMessage,
    slot = 5
)
```

The instruction immediately after that call, at RVA `0x3A5108`, observes the returned child frame in
`EAX`. A live target rebuild produced:

```text
parentFrameId = 0x4BF
healthFrameId = 0x4B9
tag = 0x430
slot = 5
callback = module + 0x3B9080  # AsHealthOnMessage thunk
```

The parent and child frame IDs are runtime allocations, not constants. The selected-target shell frame
`0x4A` is a different control: `sub_140468140` emits its portrait crop and border pieces, but no health
fill. Health instrumentation must acquire the child frame during the target-control lifecycle rather
than hard-code either observed ID.

## Selected-target messages

`AsHealthOnMessage` is `sub_1403B9730` in this build. Its message header and tag were observed as:

```text
message[0] low 32 bits  = frame ID
message[0] high 32 bits = message code
message[2] low 32 bits  = registration tag
```

For the live selected-target health child:

```text
frame = 0x4B9
tag   = 0x430
code  = 0x08  # draw
```

The code-`0x08` parameter was captured as six floats:

```text
{ 256, 16.59375, 0, 0, 256, 16.59375 }
```

`AsHealthDraw` uses the parameter bytes at `+0x08`, `+0x0C`, `+0x10`, and `+0x14` as the input
rectangle, giving `{ left=0, top=0, right=256, bottom=16.59375 }` in this full-health sample. The
first two floats are observed inputs whose native semantic names remain unresolved.

## Selected-target renderer submissions

The selected-target child emitted two native queue payloads for the full bar:

| submission | native path | observed return RVA | queue layer | packed color |
|---|---|---:|---:|---:|
| background | `EmitDrawQuad` (`sub_1403A7B20`) | `0x3BA515` (`sub_14106A400` return `0x3A7C3E`) | `2` | `0xFF401508` |
| colored fill | `sub_1403A7FD0` | `0x3A8159` | `4` | `0xFFBB2A17` |

Both payloads reached `sub_14106A400` with child frame `0x4B9`. Their live `FrameContentParams`
rectangles were:

```text
{ -17.777779, -8.296875, 285.62964, 29.631695 }
```

The rectangle is not the raw message rectangle. `AsHealthDraw` maps the input rectangle through
`sub_1403127D0` using the authored atlas constants at `0x141AE0680` and `0x141AE0688`, including the
source rectangle `{15, 7, 231, 21}` and the `{256, 32}` texture-size pair. This explains the expanded
native submission rectangle around the requested `256 x 16.59375` bar.

The live queue payload also contained `+0x10 = 0x4200000043800000` (the observed 32-bit pair is
`256, 32`). This value is recorded as raw material-related payload data; its final semantic name is not
established.

The fill path is therefore not represented solely by the direct `EmitDrawQuad` entry. The colored pass
is submitted by the separate `sub_1403A7FD0` component emitter, which constructs its own
`FrameContentParams` and queues it through `sub_14106A400`.

# Draw path

The known draw path has four major responsibilities:

1. resolve whether the current agent/state should emit a bar;
2. select the bar geometry/divisors;
3. fetch per-agent display fractions from `g_CombatTracker`;
4. select authored colors and emit the fill segments.

## Agent/state gate

Build `205.655` statically recovers the final coarse `AsHealthDraw` gate exactly. The bool is built before
geometry and is tested once immediately before the whole healthbar emit body. A false value branches to
the function epilogue, so this is an emit-cluster eligibility gate rather than a color/segment choice.

The recovered producer is:

```text
if agent == null:
    gate = false
else if agent.type == 0x00:
    gate = true
else if agent.type == 0x0A:
    wrapper = type-0x0A resolver
    gate = wrapper.vtbl[+0x200]() || wrapper.vtbl[+0x210]()
else if agent.type == 0x0B:
    state = type-0x0B resolver
    obj = state ? state.vtbl[+0x10]() : null
    gate = obj != null && obj.vtbl[+0x200]()
else:
    gate = false
```

For the type-`0x0A` wrapper, the two virtuals reduce to exact bit reads:

```text
vtbl +0x200 -> (wrapper.flags_0x1F8 >> 5) & 1
vtbl +0x210 -> (wrapper.flags_0x200 >> 13) & 1
```

Creation eligibility is broader: it also consumes the separate `vtbl +0x258` / `flags_0x1F8 bit 9`
predicate. Do not conflate creation eligibility with this draw-time gate.

These bits are entity-state inputs, not the Stage-3 target/damage/party/squad show-reason OR-chain. The
bit-5 state has a dedicated boolean setter and bit 13 is copied through type-`0x0A` snapshot/update state.
Their gameplay names remain unresolved.

# Bar geometry

The healthbar has two confirmed divisor pairs:

| mode | width divisor | height divisor |
|---|---:|---:|
| small bar | `216.0` | `14.0` |
| big bar | `1024.0` | `128.0` |

Known widget/config flags:

- `0x4000` — explicit parameter path;
- `0x800` — thick-healthbar geometry/style bit produced by the Thick Party/Squad policy path.

Build `205.655` uses `0x800` repeatedly for dimensions, offsets, and segment geometry. Static tracing and
live observation do **not** support treating it as an occlusion/depth override; thick allied healthbars
remain world-occluded in normal gameplay.

The geometry path is:

```text
if widget flag 0x4000:
    use explicit divisors from parameters
else if sub_54D3C0(agent):
    use 1024 × 128 divisors
else:
    use 216 × 14 divisors

if widget flag 0x800:
    apply the additional height multiplier
```

The resulting values are used as scale divisors for the emitted rectangles.

## `sub_54D3C0`

`sub_54D3C0`:

- handles only agent types `0x0A` / `0x0B`;
- resolves a type-specific object;
- calls vtable `+0x108`;
- returns true when that result is `0x10`;
- for the recovered type-`0x0A` wrapper, vtable `+0x108` is a leaf read of `wrapper+0x208`;
- gates the big-bar divisor path;
- participates in one special color path;
- participates in a distinct emit path.

It is **not established as a downstate predicate** and must not be documented as one.

# Shared selected/unselected brightness

Names and healthbars share:

```text
k_UnselectedBrightness = 0.7
```

The healthbar brightness accumulator is conditionally multiplied by this value on the unselected path.
The name path uses the same constant with its own accumulator.

This is a **selected-vs-unselected brightness multiplier**.

It is not:

- a downstate multiplier;
- a health fraction;
- a visibility alpha;
- the name distance-dimming cap.

# CombatTracker data source

Every healthbar draw reads display values from `g_CombatTracker`.

The service owns an agentId-indexed record table:

| field | offset |
|---|---:|
| record-array pointer | `g_CombatTracker + 0x30` |
| bounds/count | `g_CombatTracker + 0x3C` |

Index:

```text
agentId = [agent+0x0C]
record = table[agentId]
```

Each non-null entry is a `CtRecord`.

## Confirmed `CtRecord` fields

| offset | field | confirmed role |
|---|---|---|
| `+0xA0` | `lagFrac` | damage-lag trail |
| `+0xA4` | `healthFrac` | primary displayed resource fraction |
| `+0xB0` | `alpha` | unknown display value; not ordinary visibility alpha |
| `+0xB4` | `frac2` | additional fraction used with `healthFrac` |

The known getter mapping is:

| vtable slot | value |
|---|---|
| `+0x18` | `healthFrac` |
| `+0x20` | `lagFrac` |
| `+0x28` | `frac2` |
| `+0x30` | `alpha` |

Missing/out-of-range records return `0.0`.

## `lagFrac`

`lagFrac` is the damage-lag trail value. It follows the displayed health fraction during damage rather
than being the primary fill itself.

## `alpha`

`alpha` has been observed as `0.0` while healthbars were visibly rendering.

Therefore it must **not** be documented as the ordinary render-visibility alpha.

Occlusion and InfoBar visibility fading are separate from this CombatTracker field.

## `healthFrac` and `frac2`

`AsHealthDraw` sums:

```text
healthFrac + frac2
```

and splits the result around `1.0`.

The base portion is capped at `1.0`; the overshoot is kept separately. This supports display behavior
above 100%, including barrier/shield presentation.

Do not reduce these two fields to a simplistic "HP + alpha" model.

# Resource indices

`AsHealthOnMessage` performs color selection three times. The indexed results have distinct semantic
roles.

Confirmed meaning:

| index | meaning |
|---|---|
| `0` | currently displayed primary resource |
| `1` | underlying real health when a replacement resource is active |
| `2` | barrier |

Index `0` may therefore represent either:

- normal health;
- a replacement resource such as Necromancer shroud;
- a replacement resource such as Specter Shadow Shroud.

Index `1` remains the underlying health layer while a replacement resource is shown.

Index `2` is the barrier layer.

# Three-pass color-selection boundary

The retired `HealthbarProfessionColorPatch` used the direct `AsHealthSelectColors` call inside a
three-iteration loop.
The loop index is passed as the fourth semantic input and takes values `0`, `1`, and `2`, matching the
runtime observer's `indexMask = 0x7` captures.

This is a reliable repeating downstream healthbar-processing anchor, but it is **not** itself the final
whole-bar visibility gate. The coarse `AsHealthDraw` gate described above executes earlier, and final
opacity/visibility reasons are constructed upstream in Stage 3.

That patch trampoline had to preserve the draw-time `RDX` agent across the original
`AsHealthSelectColors` call. Its callback ABI is therefore `RCX=agent`, `RDX=firstColor*`,
`R8=secondColor*`, and `R9D=index`; the color pointers originate from the original stack arguments
and the index from `R8D` at the call site. This mapping is part of the patch contract, not a guessed
object-layout relationship. Profession coloring no longer patches this call; it now operates on the
completed fill submission described below.

# Authored colors

Colors below are authored native pairs recovered from live behavior. They are not derived by multiplying
a generic base color.

## Normal enemy health

```text
primary   0xFFBB2A17   #BB2A17
secondary 0xFF401508   #401508
```

## Necromancer shroud

```text
primary   0xFF8B9A77
secondary 0xFF0D2501
```

## Specter Shadow Shroud

```text
primary   0xFFCA66BD
secondary 0xFF5E0151
```

## Barrier

```text
0xC0FAE29A
```

Barrier is the dedicated index-2 resource color.

# Confirmed normal/downstate health pairs

Downstate uses a separately authored **primary** color. The secondary remains unchanged.

## Enemy

Normal:

```text
primary   #BB2A17
secondary #401508
```

Downstate:

```text
primary   #64140A
secondary #401508
```

## Friendly

Normal:

```text
primary   #6AC951
secondary #0D2501
```

Downstate:

```text
primary   #0F5A0A
secondary #0D2501
```

Downstate is therefore **not**:

- the secondary color;
- `k_UnselectedBrightness = 0.7`;
- a color derived from multiplying the normal primary;
- established by `sub_54D3C0`.

# Attitude and health presentation

The known native `Attitude` enum is:

| value | name |
|---:|---|
| `0` | Friendly |
| `1` | Hostile |
| `2` | Indifferent |
| `3` | Neutral |

Do not assign canonical health color pairs to `Indifferent` or `Neutral` until directly recovered.

A useful addon-side design concept is to classify **health presentation** separately from native
attitude, for example:

```text
WorldFriendly
WorldHostile
SpectatorBlue
SpectatorRed
Unknown
```

This is an addon architecture concept, not a confirmed native enum.

Spectator team bars likely use different authored color pairs. Exact pairs should be captured from the
native path rather than guessed from screenshots.

# Fail-closed color replacement

Health recoloring should use positive identification of known canonical pairs.

Recommended behavior:

- recolor only recognized canonical health pairs;
- leave unknown primary resources/shrouds unchanged;
- handle barrier index `2` separately;
- recolor index `1` only when it is a recognized health pair and index `0` is a replacement resource.

This prevents health-color patches from accidentally recoloring unrelated AsHealth users or unknown
resource presentations.

Broad callback patching is especially risky because AsHealth has a known registration outside the
normal world InfoBar unit.

# Native emitter boundary

Name and health drawing eventually converge on the shared native emitter:

```text
EmitDrawQuad(
    widgetId,
    layerIdx,
    rect*,
    color*,
    arg5,
    materialData*,
    worldPos*,
    spaceFlag,
    flag9
)
```

Build `205.780` now recovers the immediate submission object as a `0x130`-byte
`FrameContentParams` stack value. `EmitDrawQuad` initializes it, then
`sub_14106A400(frameId, params)` turns it into a pooled type-`9` `GrModel` and queues that model into
the frame content layer. The confirmed fields are layer `+0x04`, retained material `+0x08`, material
data `+0x10`, packed submodel color `+0x18`, model rectangle `+0x1C`, final draw flag `+0x38`, and
four `0x30`-byte texture-coordinate transforms at `+0x6C`. The world-space path additionally writes
a 16-byte shader input at `+0x3C`, which the model receives under token `0x8D80FCFC`.

This reconstructs the native queue payload and its model conversion, not a safe external submission ABI:
the material/data ownership, render-thread/frame lifetime, and remaining header semantics still belong to
the native path.

# Emitter paths

## `spaceFlag != 0`

The emitter uses its 3D/world path.

It constructs `FrameContentParams` through the recovered setter chain, including layer, world transform,
color, and rectangle state, then converts it to a pooled `GrModel`.

`sub_140A52130` writes the emitter's final flag argument at:

```text
command + 0x38
```

The completed `FrameContentParams` is then submitted through:

```text
sub_14106A400
```

## `spaceFlag == 0`

The emitter uses the project-to-screen path.

This path reaches the screen-projection context and corresponding 2D submission behavior.

# Health emitter inputs

For health, keyed property `0x4D` is handled by the extended-property tail of `AsHealthOnMessage`. It
stores `propertyValue != null` at:

```text
AsHealthWidget.emitterSpaceFlag @ +0x74
```

`AsHealthDraw` forwards that field to every health `EmitDrawQuad` call:

```text
spaceFlag = AsHealthWidget.emitterSpaceFlag
```

`InfoBarFinalize` computes property `0x4D` by comparing `InfoBar.unit` with the unit associated with the
controlled character. For ordinary non-self units this is normally nonzero. The emitter branches on
`spaceFlag`, but this field is **not** the selective Party/PvP occlusion switch: ordinary allies and party
allies share the same self-vs-other classification.

Health passes:

```text
arg9 = 1
```

Before health emits, the draw path obtains the world anchor through:

```text
agent->vtbl[+0x1D0](out*)
```

This is part of the native world-space handoff.

# Native healthbar observer

The observer hooks the native `EmitDrawQuad` entry and accepts only the three return addresses of the
direct health-emission calls recovered from `AsHealthDraw` in build `205.780`:

```text
EmitDrawQuad return RVAs:
0x3BA313
0x3BA515
0x3BA69A
```

These accepted callsites cover the currently instrumented direct health-emission path. The selected
target background was live-correlated with the accepted `EmitDrawQuad` return RVA `0x3BA515`; the
`0x3A7C3E` value is the internal `sub_14106A400` queue-call return inside `EmitDrawQuad`. The selected
target colored fill is emitted separately by `sub_1403A7FD0` at `0x3A8159`, so it does not enter the
observer's `EmitDrawQuad` entry hook. Consequently, the existing observer/replacement probe can see
and replace the selected-target background but does not by itself replace or capture the colored fill.

For accepted calls it records the native frame/layer, rectangle, packed color, emitter arguments, and
the `AsHealthWidget`/agent links into a bounded frame-scoped ring. The agent id is read through the
confirmed `AsHealthWidget +0x58` and `Agent +0x0C` links. Pointers in the record are diagnostic
addresses only and must not be retained as owned objects.

The internal mechanism is named `NativeHealthbarSubmission`: it captures native direct inputs and can
replace or suppress confirmed direct/fill submissions. Profession coloring and diagnostic capture can
remain active together because profession colors are applied only to a copied completed fill command;
the direct-input capture ring continues to contain the game's original values.

The retired validation probes established that a copied direct command is accepted by `EmitDrawQuad`
and that returning a suppression result skips the displaced native call. The final live test replaced
the direct background and colored fill with shifted magenta commands. Their hard-coded Awareness
toggles and fixed probe values were removed after this confirmation; the validated mechanism now sits
behind the value-based module submission contract. Material/data handles and world-anchor arguments
remain borrowed from the live native healthbar call and must not be retained across frames.

Live follow-up at the fill builder's queue call (`0x1403A8154`, returning at `0x1403A8159`)
confirmed completed layer-`4` commands with packed color `0xFFBB2A17` at
`FrameContentParams +0x18`. The fill submission path therefore intercepts this specific call site
after `sub_141070270` writes the color and before the builder releases its retained material. It copies
the complete `0x130`-byte command, applies the requested rectangle and color changes, submits it through
`sub_14106A400`, and suppresses the displaced queue call only after that submission succeeds. The
builder then resumes at `0x1403A8159`, preserving its native material-release path.

The module-facing first-stage submission contract is value-based and same-frame. Its explicit decision
is keep native, replace, or suppress. A replacement request
selects any combination of direct quads, primary fill (layer `4`), underlying-health fill (layer `1`),
and breakbar fill (layer `5`), plus X/Y offsets and a packed color. The runtime copies the current native
command and applies those values synchronously. Unknown fill layers, invalid pass bits, non-finite
offsets, and offsets outside `[-4096, 4096]` fail closed to the native submission. This deliberately does
not expose retained material pointers or a module callback across the NativeAOT boundary.

Profession coloring scopes ownership at `AsHealthDraw @ 0x1403B9E30`: an entry callback records the
current `AsHealthWidget` in thread-local state and the single epilogue clears it. The fill callback reads
`AsHealthWidget +0x58`, resolves profession through the existing game-state snapshot sourced from the
character context, and replaces only recognized normal/downstate colors on fill layers `4` and `1`.
Layer `5` breakbar fills, unknown colors/resources, missing agents, and missing profession snapshots all
retain the native command. The unrelated `AsBreakBar` caller at `0x1403B284E` therefore cannot inherit
stale `AsHealth` ownership.

# Name-side comparison

The confirmed name-side emitter call uses:

```text
spaceFlag = [obj+0x60]
arg9 = 0
```

Name-side details belong in [`name-rendering.md`](name-rendering.md). The important shared fact is that
name and health eventually enter the same native emitter/submission infrastructure.

# Render layers

Known InfoBar render layers:

| element | layer |
|---|---:|
| health / `AsHealth` | `0` |
| tag `0x2430` | `1` |
| name | `2` |
| tag `0x10630` | `3` |

The layer is written into draw-command queue/order state and used to select the corresponding render
queue.

It is **not** the depth-test selector.

World-depth occlusion is a separate part of the native rendering path; see
[`occlusion.md`](occlusion.md).

# Relationship to authoritative health

`AsHealth` reads the `CombatTracker CtRecord` display layer. The authoritative character state,
reflected display model, tested agent-id join, and unresolved bridges are documented in
[`../Context/context-and-health.md`](../Context/context-and-health.md).

# Known boundaries — do not conflate

- `AsHealth` != generic InfoBar health-float rendering
- `AsHealthWidget.agent` != automatically `InfoBarUpdate(..., agent)`
- `stateFlags +0x27C` != `configTag +0x280`
- `stateFlags & 0x1000` != config tag `0x10030` / `0x10830`
- `k_UnselectedBrightness = 0.7` != downstate coloring
- downstate primary != secondary color
- `sub_54D3C0` != confirmed downstate detector
- `CtRecord.alpha` != ordinary visibility alpha
- `lagFrac` != primary health fraction
- index `0` != always real health
- index `1` != always active primary resource
- index `2` = barrier
- render layer != depth-test selector
- unrelated fourth AsHealth registration != proven spectator UI
- selected-target shell frame `0x4A` != selected-target health child frame
- observed target child frame `0x4B9` != a stable frame ID
- selected-target background observer return `0x3BA515` != its internal queue return `0x3A7C3E`
- selected-target colored fill `sub_1403A7FD0` return `0x3A8159` != the direct emitter hook

# Open leads

Useful unresolved areas:

- exact writers that populate/update `CtRecord`;
- exact bridge from `AgentOrderHealth*` messages into CombatTracker display state;
- exact native semantics of `CtRecord.alpha`;
- exact semantics of `frac2` beyond its confirmed participation in the `1.0` split;
- exact state represented by `sub_54D3C0`;
- semantic names of the type-0 character-side float getters used by the Stage-3 resource-deficit comparison;
- exact authored `Indifferent` / `Neutral` health pairs;
- exact spectator-team health pairs;
- a live damaged selected-target sample proving which code-`0x08` input changes and how it maps to the
  emitted fill rectangle;
- exact ABI contract needed to hand custom state safely into the native renderer.

The stable integration boundary is currently the recovered `AsHealth` state-selection and
`EmitDrawQuad` handoff, not a reconstructed version of ArenaNet's original C++ source.
