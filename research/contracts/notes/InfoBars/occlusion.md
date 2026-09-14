# InfoBar occlusion

**Confirmed build(s):** `205.655` and `205.780`, scoped by the sections below.<br>
**Status:** build-local occlusion and category behavior reconstructed from controlled live observations.<br>
**Unresolved:** later draw/pass/effect decisions and behavior outside the listed builds.

This document defines the confirmed occlusion behavior shared by InfoBar names and `AsHealth`
healthbars, and separates that behavior from higher-level visibility, fading, and widget-state gates.

The client design should preserve ArenaNet's native projection, occlusion, fading, scaling, and draw
submission. Through-wall experiments are reverse-engineering evidence, not the intended product behavior.

Name rendering is documented in [`name-rendering.md`](name-rendering.md). Healthbar rendering and the
shared emitter are documented in [`healthbar-rendering.md`](healthbar-rendering.md). InfoBar lifecycle
and Stage-3 policy are documented in [`pipeline.md`](pipeline.md).

Symbols are maintained in the active Ghidra project; see [`methodology/ghidra.md`](../../methodology/ghidra.md). Authoritative
layouts include [`AsHealthWidget.cs`](../../Gw2.Contracts/InfoBars/AsHealthWidget.cs) and
[`NameRenderCtx.cs`](../../Gw2.Contracts/InfoBars/NameRenderCtx.cs).

# Observed native behavior

Build `205.655` live observation establishes that names and healthbars do not share one simple
"occluded = hidden" rule.

For allied players in PvP:

```text
directly visible
    name: brighter / stronger presentation
    healthbar: visible when otherwise eligible

world-occluded but still inside native nameplate eligibility/range
    name: still visible through the occluder at reduced presentation strength
    healthbar: world-occluded / suppressed

world-occluded and beyond native nameplate eligibility/range
    name: no longer shown
```

Thick Party/Squad healthbars remain world-occluded in ordinary gameplay. A previous observation that a
large allied bar survived a wall was not reproducible and must not be used as evidence that thick-bar
style disables occlusion.

World-depth clipping itself has hard edges that follow the occluder's silhouette. This is distinct from
the reduced-alpha/fade behavior of an otherwise eligible allied name.

# Separate native layers

The recovered system has at least these independent layers:

```text
creation / lifetime
    5000-unit InfoBar creation-time cull
    agent/model lifetime

Stage-3 show reasons and opacity
    target / call target / relationship policy / damaged proximity / fades / other reasons

name projection and child presentation
    NameRenderCtx.agent gate
    world-anchor + projection output
    distance brightness and child-component state

health widget runtime state
    AsHealthFrameView.stateFlags

shared native emitter / world-depth rendering
    spaceFlag path
    layer/order state
    final native depth/material behavior
```

Do not flatten these into one `visible`, `occluded`, or `depthDisabled` bit.

# Render layer is not the depth selector

The layer map is owned by [`pipeline.md`](pipeline.md#native-rendering-boundary). Those values are
queue/order state; re-layering does not remove world-depth clipping.

# Health runtime occlusion state

The framework frame associated with `AsHealthWidget` carries a runtime state bit:

```text
AsHealthFrameView.stateFlags & 0x1000
```

at:

```text
AsHealthFrameView.stateFlags @ +0x27C
```

Clearing this live state can re-enable healthbar drawing, so it is a widget-level occlusion/cull state.
It is not proven to be the GPU depth-test enable itself.

The adjacent:

```text
AsHealthFrameView.configTag @ +0x280
```

contains static/configuration style bits. The Thick Party/Squad path produces `0x10030` or `0x10830`;
the `+0x800` difference controls thick-healthbar geometry. Static tracing shows `0x800` being consumed
for dimensions, offsets, and segment geometry, not as a depth bypass.

Therefore:

```text
stateFlags & 0x1000 != configTag & 0x800
```

and:

```text
thick healthbar != ignore occlusion
```

# Name projection/render gate

`NameRenderCtx.agent` at `+0xA8` gates the guarded world-anchor/projection block in `NameRender`.
`NameRenderCtx_SetAgent` maintains this as a tracked native reference; it is not an occlusion bool.

Conceptually:

```text
if NameRenderCtx.agent == null:
    skip the guarded world-anchor/projection/render-preparation block
```

Forcing that skip produces through-wall names, proving that this gate reaches the native world-space
projection/obstruction path. It does **not** prove that vanilla allied-through-wall names naturally set
`NameRenderCtx.agent` to null.

In fact, the current evidence favors the opposite: the vanilla friendly-player path keeps the agent
reference and executes normal projection, while relationship/category-specific presentation allows the
name to remain visible at reduced strength when the player model is obstructed.

# Name projection output and downstream handoff

The complete projection and child-presentation path is owned by
[`name-rendering.md`](name-rendering.md#projection-output-and-child-presentation-handoff). Its relevant
occlusion conclusion is that the recovered projection virtual performs projection math, not the
world-obstruction test. The friendly-player rule must enter later in the child/render/material path or
through another presentation state.

# Name material, shader constant, and depth-peel evidence

The material layout, shader inputs, effect records, and bounded effect-key trace are documented once in
[`name-rendering.md`](name-rendering.md#exact-name-material-depth-shader-evidence). For occlusion, the
result remains negative: no recovered branch maps `FriendlyPlayer` plus obstruction directly to shader
`9`, `DepthPeelPush`, or effect flags `0x60`. Treat the effect as a render-side lead, not a recovered
semantic switch.

# Friendly-player and hostile categories

`NameCategory.FriendlyPlayer` is confirmed as numeric value `4`; `NameCategory.Hostile` is `7`.
Both values are produced by the native type-0 relationship/category resolver. Build `205.780` live
capture confirms that `Hostile` includes hostile NPCs and is not a player-only category.

Build `205.655` live behavior shows that a friendly PvP name may remain visible through geometry while
in native range, but with reduced presentation strength. This is a target behavior fact; the exact
internal branch that converts obstruction into the reduced friendly-name state remains unresolved.

A category-selective patch that skips the entire projection block for `FriendlyPlayer` is therefore an
experimental bypass, not a reconstruction of vanilla friendly-name occlusion behavior.

# Ruled-out name-side lead: type-0x0A bit 20

A type-`0x0A` wrapper predicate at vtable `+0x280` reads:

```text
(wrapper.flags_0x1F8 >> 20) & 1
```

and participates in one `3500 -> 3200` presentation-fade branch. It is **not** the general friendly-PvP
name mechanism. Its only direct setter is reached from a `GdCliMsg.cpp` path whose native assertion
requires `GADGET_TYPE_PLAYER_SPECIFIC`.

Keep this bit in the gadget/player-specific state family until stronger semantics are recovered.

# Health keyed property `0x4D`

`InfoBarFinalize` computes keyed property `0x4D` from a self-vs-other unit comparison. The UI context
path resolves the controlled character and its associated unit; the property is:

```text
property4D = InfoBar.unit != controlledCharacterUnit
```

The extended-property tail of `AsHealthOnMessage` handles `0x4D` by storing:

```text
AsHealthWidget.emitterSpaceFlag @ +0x74 = propertyValue != null
```

`AsHealthDraw` forwards that field to each `EmitDrawQuad` call as `spaceFlag`.

This is a real emitter-mode input, but it cannot be the selective Party/PvP occlusion switch: ordinary
allies and party allies are both normally "other" units and therefore receive the same property value.

Keyed property `0x4C` is separate: the same extended-property tail uses it to update
`AsHealthWidget.agent @ +0x58` through the native reference-assignment helper.

# Global settings bit 0

`Settings_GetFlagBit0` returns:

```text
g_Settings.flags & 1
```

Stage 3 consumes it as a visibility-routing branch. When the bit selects the alternate route, native code
updates the InfoBar presentation/interpolator state and bypasses the normal downstream show-reason block.
The existing experimental patch inverts the consuming branch; it does not permanently set the global
bit.

Because this bit is global, it also cannot explain why two simultaneously visible allied units can have
different healthbar occlusion behavior.

The earlier forced-through-wall health experiment required both this alternate visibility route and a
change to the `0x4D`/emitter-mode side. That experiment proves the two layers can be manipulated; it does
not identify either input as ArenaNet's final GPU depth-test switch.

# Name brightness/fade is not world-depth occlusion

The distance-brightness ramp and `Clamp01Fade` bands are owned by
[`name-rendering.md`](name-rendering.md#distance-brightness-dimming). Their occlusion boundary is:

```text
distance brightness != visibility alpha fade != world-depth clipping
```

The reduced appearance of an occluded friendly PvP name may include more than one of these presentation
inputs; current static evidence does not justify collapsing it into a single `occludedAlpha` constant.

## GroupedPlayer name presentation boundary

Build `205.780` controlled visible/occluded captures followed a `GroupedPlayer` party-member name through the
first `NameRender` child group and into `sub_A857E0`. The category-dependent virtual returned `1` in
both states. The active `CtlText` child set, concrete vtable, name material, and material byte count were
also unchanged.

Across 1024-byte snapshots of the active children, only the cached presentation vector at `+0xD0` and
`+0xD8` changed; child `5` mirrored the same pair at `+0x2F0` and `+0x2F8`. Those values exactly matched
the already-recovered `{ projectionDepth - 50, brightness }` components. Event-time inspection of the
child-`4` `GrModel` found the same model type, submodel count, submodel flags, shader-input token/type,
and input topology in both states. Its `control` vector changed only with projection depth and the
distance-brightness ramp.

Therefore the captured `GroupedPlayer` through-geometry behavior is not selected at the category virtual,
the first child-group membership/material boundary, the cached child presentation fields, or the
inspected `GrModel`/local `control` input. The unresolved decision lies in a later draw/pass/effect/depth
boundary or in a separate render phase.

## Party category A/B/A presentation switch

Build `205.780` captured one partially occluded Character through joined -> left -> rejoined party state
without changing the camera or projection depth. The same native agent and Character wrapper used
`GroupedPlayer` (`3`), child `1`, and `control.z == 0.46595776` while joined. Leaving selected
`FriendlyPlayer` (`4`), children `4/5/10`, and `control.z == 0`; rejoining restored the original grouped
context and value.

Static disassembly explains this split. `NameCategoryUsesDistanceBrightness` returns true unconditionally
for `GroupedPlayer`, returns `NameCategoryPresentationState.flags_0x18 bit 3` for `FriendlyPlayer`, and
false for other categories. A false return leaves the CPU-produced brightness at zero. Thus the observed party
toggle changes category/context/child presentation before the renderer; it is not evidence that joining
mutates a world-obstruction result or a GPU depth-test bit. Any separate per-pixel depth behavior remains
unresolved.

# Known boundaries — do not conflate

- world-depth occlusion != `Clamp01Fade`
- world-depth occlusion != name distance brightness dimming
- render layer != depth-test selector
- `AsHealthFrameView.stateFlags & 0x1000` != GPU depth-test enable
- `AsHealthFrameView.stateFlags +0x27C` != `configTag +0x280`
- `configTag & 0x800` != ignore occlusion
- thick Party/Squad healthbar != through-wall healthbar
- `NameRenderCtx.agent` gate != vanilla friendly-player through-wall policy
- property `0x4D` != Party/Squad-specific visibility state
- settings bit 0 != per-unit occlusion state
- through-wall RE bypasses != intended client behavior

# Open leads

Useful unresolved areas are now:

- identify the exact native branch/state that turns an obstructed `FriendlyPlayer` name into the reduced
  but still visible presentation while inside native range;
- follow the `GroupedPlayer` name beyond the unchanged child/`GrModel` presentation boundary into final
  draw/pass/effect/depth selection or a separate render phase;
- identify every writer/transition source for `AsHealthFrameView.stateFlags & 0x1000`;
- establish the exact relationship between health runtime `OCCLUDED` state and final native draw
  submission;
- recover the final material/command state governing per-pixel world-depth behavior after
  `EmitDrawQuad`;
- preserve the observed distinction between allied-name through-occlusion behavior and healthbar
  occlusion during renderer-input replay.
