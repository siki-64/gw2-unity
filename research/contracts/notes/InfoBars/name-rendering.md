# Name rendering

**Confirmed build(s):** `205.655` and `205.780`, scoped by the sections below.<br>
**Status:** build-local rendering and visibility reconstruction supported by static and controlled live evidence.<br>
**Unresolved:** the meanings explicitly marked unresolved and behavior outside the listed builds.

This document defines the confirmed rendering and visibility behavior for the InfoBar name sub-widget.

The key distinction is:

```text
NameRender
    general visible-name projection / brightness / color path

NameDraw
    selection decoration + conditional level-text / auxiliary emission
```

A previous interpretation that the second `EmitDrawQuad` in `NameDraw` represented the general visible
name was disproven. Treat these as separate paths.

Occlusion is documented in [`occlusion.md`](occlusion.md). The shared native emitter is reconstructed in
[`healthbar-rendering.md`](healthbar-rendering.md). Symbols are defined in
the active Ghidra project; see [`re/methodology/ghidra.md`](../../methodology/ghidra.md); layouts and stable constants live in
[`NameRenderCtx.cs`](../../../src/Gw2.Native/InfoBars/NameRenderCtx.cs),
[`NameCategory.cs`](../../../src/Gw2.Native/InfoBars/NameCategory.cs), and
[`InfoBarConstants.cs`](../../../src/Gw2.Native/InfoBars/InfoBarConstants.cs).

# Overview

The visible-name presentation path combines several independent mechanisms:

```text
NameRenderCtx.agent
    ↓
world anchor / projection
    ↓
per-frame color + selected/unselected brightness
    ↓
distance brightness dimming
    ↓
InfoBar visibility alpha fades / full-visible overrides
    ↓
native rendering
```

These mechanisms must remain distinct.

In particular:

- selected/unselected brightness is not distance dimming;
- distance dimming is not visibility alpha fade;
- visibility alpha fade is not world-depth occlusion;
- render layer is not the depth selector.

# `NameRender`

`NameRender` is the general visible-name path.

It owns the confirmed world-position/projection and brightness/color computation used for ordinary name
text.

The path is reached through the name-render unit associated with the `"teamColor"` string anchor.

A widget-property gate at key `0x400` controls entry into the caller that reaches `NameRender`.

# `NameDraw`

`NameDraw` is not the general visible-name path.

Its confirmed role includes:

- selection decoration;
- conditional level-text emission;
- auxiliary draw emission through the shared native emitter.

Do not infer the presence or visibility of the ordinary name solely from the second `EmitDrawQuad` in
`NameDraw`.

# Name render input

## Allocation and lifecycle identity

Message `10` in the name callback (`sub_1403999B0`, build `205.780`) allocates the object consumed by
`NameRender`, `NameRenderCtx_SetAgent`, and `NamePresentationUpdate` as exactly `0x110` bytes. This is
the concrete `NameRenderCtx` allocation, rather than a view into a framework frame.

Confirmed lifecycle members include:

| offset | identity | evidence |
|---|---|---|
| `+0x18` | frame id | written from the message header; used for child-frame/property calls |
| `+0x58` | Character tracked-client record | registered through `ChCliCharacter` vtable `+0x558`, removed through `+0x560` |
| `+0x88` | Gadget tracked-client record | registered through gadget vtable `+0x2E8`, removed through `+0x2F0` |
| `+0x98` | Guild listener | removed through the cached Guild object's vtable `+0xD0` |
| `+0xA0` | name-category listener | registered and removed by `NameCategoryPresentationStateAccessor` |
| `+0xD8` | `m_guild` | callback assertion and teardown registration match |
| `+0xE0` | `m_markerContext` | constructed in `sub_14039A500`; listener at `+0x80` is registered against it |
| `+0xE8` | `m_masterCharacter` | native callback assertion |
| `+0xF8` | `m_registeredPlayer` | native callback assertion |
| `+0x108` | `m_registeredTransformationMgr` | native callback assertion |

The object ends after `+0x108`; no `+0x110` member is valid for this build.

`NameRenderCtx.agent` is the name's projection/render input.

It must remain distinct from:

- `InfoBar.unit` at `+0xA0`;
- the update-time `agent` passed to `InfoBarUpdate`;
- `AsHealthWidget.agent`.

`NameRenderCtx.agent` gates the world-space projection block. `NameRenderCtx_SetAgent` maintains this
reference through the native linked/reference assignment helper; it is not a transient visibility bool.
Its occlusion implications are documented in [`occlusion.md`](occlusion.md).

# Projection output and child presentation handoff

Build `205.655` traces the guarded `NameRender` projection block through its immediate downstream
consumers. After obtaining the world anchor with `agent->vtbl[+0x1D0](out*)`, the graphics-service
virtual at `+0x288` reaches the normal projection backend. The backend performs matrix transformation
and perspective division only; no world-obstruction test was found in this virtual.

Its outputs are now structurally resolved:

```text
outXY     = projected/perspective-divided screen X,Y
outDepth  = homogeneous W / projection-depth-like scalar
```

`NameRender` does not consume `outXY` again in this block. It does consume `outDepth` as the input to
the settings-gated distance-brightness calculation below. This value is therefore **not** a raw
Euclidean world distance and must not be renamed to an occlusion result.

The next native helper reuses the temporary world-anchor buffer as a 16-byte presentation value. With
the build-`205.655` call arguments resolved, the value is exactly:

```text
{
    outDepth - 50.0f,
    50.0f,
    brightness,
    0.0f
}
```

`NameRender` copies that value and distributes the same 16 bytes to child components in two groups:

```text
component ids 4, 5, 8, 1, 7, 10
    -> child vtable +0x78 with the presentation value plus a shared keyed argument

component ids 6, 2, 3, 9, 11, 12, 13
    -> child vtable +0x68 with the presentation value
```

This establishes a concrete projection-to-child presentation handoff, but it also rules out the
`+0x288` projection virtual itself as the friendly-name world-obstruction test. Build `205.780` confirms
that relationship-specific visibility can enter through the category brightness gate described below;
any actual geometry/depth handling remains later or in a separate render phase.

# Build 205.655 name material and shader-input path

The first child group above is now traced beyond `NameRender` into the graphics material/shader system.
For the concrete `CtlText` implementation, vtable `+0x78` resolves to `sub_1045910`. Its build-`205.655`
ABI receives:

```text
RDX = material data
R8D = material byte count
R9  = pointer to the 16-byte presentation value
```

`CtlText.cpp` assertions identify the integer argument as `materialByteCount`. The material pointer and
byte count used by `NameRender` are initialized from resource-table entry `6` and resolve to:

```text
material symbol = DATA_195D900
material size = 0x7254 bytes
format        = AMATGRMT8 / BGFX
```

The same `CtlText` setter caches the 16-byte value at `CtlText+0xD0..+0xDF`. When changed, or when the
text control is rebuilt, ArenaNet republishes that exact value through the Frame-to-graphics relation.
The receiver is a `GrModel` graphics object (type `9`), and the value is applied to every submodel.

Each submodel stores sorted 24-byte shader-input records of the form:

```text
+0x00  uint32 token
+0x04  EDdiShaderInputType type
+0x08  float4 value
```

The `NameRender` value is stored with token `0x8D80FCFC`. The reader in `BgfxDraw.cpp` asserts that its
type is `DDI_SHADER_INPUT_VECTOR_CONSTANT` and returns the four floats directly to the backend shader
constant upload path. Reflection metadata in the exact `AMATGRMT8` package maps this token to the shader
uniform name:

```text
0x8D80FCFC -> control
```

Therefore the proven CPU-to-GPU path is:

```text
NameRender
    control = { projectionDepth - 50, 50, brightness, 0 }
        ↓
CtlText material presentation
        ↓
GrModel / each submodel
        ↓
DDI_SHADER_INPUT_VECTOR_CONSTANT token 0x8D80FCFC
        ↓
shader uniform `control`
```

`control` is a generic material-defined uniform name/token; other unrelated materials use the same
token with different component semantics. Do **not** infer the name semantics of `control.x/y/z/w` from
an unrelated shader permutation. In particular, `control.z` remains the existing name distance-
brightness input at the CPU producer and is not proven to be an occlusion alpha.

## Exact name-material depth shader evidence

The exact name material contains 20 embedded BGFX shaders and multiple authored effects/permutations.
One fragment shader, shader index `9`, reflects:

```text
ScreenDims
DepthPeelPush
TexelOffset
gSs150
```

The serialized `0x24` effect record that selects pixel shader `9` is at material offset `0x6DCC`. Its
currently resolved fields are:

```text
+0x10  effect flags                = 0x60
+0x14  pixelShaderIndex            = 9
+0x18  vertexShaderVariantsCount   = 5
```

Arena's `BgfxShader.cpp` loader confirms the authored hierarchy and the serialized container layout.
For this exact material, build `205.655` resolves the hierarchy to one technique and one pass:

```text
AMAT/BGFX root @ material+0x5C
    techniqueCount = 1
    technique[0]    = material+0x684

technique[0]
    passCount = 1
    pass[0]    = material+0x6D2C

pass[0]
    effectCount = 10
    effects      = material+0x6D3C
```

The serialized pointers are self-relative 64-bit displacements. The technique exposes its pass count at
`+0x04` and pass pointer at `+0x08`; each packed pass descriptor is `0x0C` bytes with effect count at
`+0x00` and effect pointer at `+0x04`; each effect descriptor is `0x24` bytes.

Pixel shader `9` is effect `4` of the sole pass. The ten effect keys and pixel-shader indices are:

| effect | material offset | effect key | pixel shader |
|---:|---:|---:|---:|
| 0 | `0x6D3C` | `0x000000000002CC22` | 0 |
| 1 | `0x6D60` | `0x0000000000001081` | 1 |
| 2 | `0x6D84` | `0x0000000000471582` | 3 |
| 3 | `0x6DA8` | `0x0052B0A55E8A40A4` | 2 |
| 4 | `0x6DCC` | `0x00000C29608A40A4` | 9 |
| 5 | `0x6DF0` | `0x00000001481311EC` | 10 |
| 6 | `0x6E14` | `0x00000000018011ED` | 11 |
| 7 | `0x6E38` | `0x0914C6A8A883B1EE` | 0 |
| 8 | `0x6E5C` | `0x000000A20E963532` | 12 |
| 9 | `0x6E80` | `0x0051C59061370654` | 7 |

Effects `3` and `4` form a structural pair: both have effect flags `0x60`, zero at effect `+0x08`, and
the same five vertex-shader variants. This relationship is confirmed structurally; it does not establish
visible/occluded semantics.

The ordinary per-draw effect-key path has also been statically bounded. `sub_B0BE70` emits the selected
effect key unchanged except for two explicit non-PS9 special replacements. Its default-key path consumes
a per-draw key stored at draw-record `+0x80`. The known setters, fallback/base-key setter, requested-key
update path, and static key tables feeding that field do not produce effect `4`'s key
`0x00000C29608A40A4`. Thus the audited generic per-record key-selection route does **not** explain
selection of the name material's PS9 effect.

This still does **not** prove that shader `9`, `DepthPeelPush`, or effect flags `0x60` implement the
FriendlyPlayer through-geometry rule. The effect may be selected by another render-phase/material
interface, or it may be authored without contributing to ordinary name rendering.

# Shared selected/unselected brightness

Name and health rendering share:

```text
k_UnselectedBrightness = 0.7
```

The name path conditionally multiplies its brightness accumulator by this value for the unselected path.

This is the selected-vs-unselected brightness behavior.

It is not:

- a downstate multiplier;
- the distance-dimming cap;
- a visibility alpha fade;
- a world-depth occlusion value.

# Distance brightness dimming

`NameRender` contains a settings-gated distance brightness ramp.

The relevant settings call is:

```text
g_Settings.vtbl+0x40(g_Settings, [r15+0xC0])
```

Build `205.780` resolves this virtual to `NameCategoryUsesDistanceBrightness`. Its exact return contract
is:

```text
GroupedPlayer (3)  -> 1
FriendlyPlayer (4) -> (NameCategoryPresentationState.flags_0x18 >> 3) & 1
otherwise          -> 0
```

`NameRender` initializes the presentation brightness to `0`. A false return skips the entire
projection-depth ramp and leaves `control.z` at zero; a true return evaluates the ramp below.

When enabled, the confirmed approximation is:

```text
dim = clamp((projectionDepth - 1000.0) / 5500.0, 0, 1) * 0.6
```

Known constants:

| symbol | value |
|---|---:|
| `k_DimDistStart` | `1000.0` |
| `k_DimDistRange` | `5500.0` |
| `k_DimCap` | `0.6` |
| `k_One` | `1.0` |

The resulting value participates in the name text-color/brightness path.

Here `projectionDepth` is the homogeneous/projection-depth scalar returned by the graphics projection backend, not the Euclidean world-space distance returned by `InfoBarWorldDistance`.

This is **brightness attenuation**. It must not be described as visibility alpha fade.

Build `205.780` also provides a controlled party A/B/A confirmation for one partially occluded Character
agent held at the same camera/depth (`projectionDepth - 50 == 2178.7207`):

| party state | category | active first-group children | `control.z` |
|---|---:|---|---:|
| joined | `GroupedPlayer` (`3`) | `1` | `0.46595776` |
| left | `FriendlyPlayer` (`4`) | `4`, `5`, `10` | `0` |
| rejoined | `GroupedPlayer` (`3`) | `1` | `0.46595776` |

The native agent, agent id, Character wrapper, material, and projection depth remained identical. Party
membership therefore adds/restores a `GroupedPlayer` child-1 presentation with the enabled distance-brightness
value; leaving selects the `FriendlyPlayer` children with its gate disabled in the captured state.
This does not make `control.z` an obstruction result and does not by itself prove different GPU depth
state between the two child sets.

Setting:

```text
k_DimCap = 0
```

removes this distance dimming.

# Constant-domain constraint

`k_DimDistStart` is validated downstream.

Using a value far outside the expected domain, such as:

```text
1e8
```

triggered the engine assertion:

```text
val <= 0x003FFFFF
Math.cpp:200
```

Therefore modifications to this path should remain within the native accepted value domain.

`k_DimCap` also has readers outside the nameplate path. Global patching of that constant can affect
unrelated rendering.

# Visibility alpha fade: `Clamp01Fade`

The name visibility system uses a separate helper:

```text
Clamp01Fade
```

Its semantics are:

```text
if min == max:
    return value < min ? 0 : 1

ratio = (value - min) / (max - min)
return clamp(ratio, 0, 1)
```

The function performs a saturating inverse-lerp using caller-supplied `value`, `min`, and `max`.

It is not a depth-test function.

# Known callers

Five confirmed callers in the InfoBar render unit use `Clamp01Fade`:

- `sub_5600F8`
- `sub_560CFD`
- `sub_560FB3`
- `sub_5612F7`
- `sub_56137D`

These callers contribute to name visibility/opacity behavior.

Known static threshold values include:

```text
575
1800
3200
3500
4700
128
```

Caller-specific values also include:

```text
500
5000
```

Do not imply that every `min`/`max` pair is loaded directly from one static threshold table.

# Caller behavior

## `sub_5600F8`

Known range:

```text
~575 → 500
```

with fade multiplier:

```text
0.7
```

Known behavior includes:

- target/context override checks;
- mouseover override checks;
- fade contribution into `InfoBar.opacity`;
- a full-visible override that can set `InfoBar.field_E4 = 1.0`.

## `sub_5612F7`

Known ranges include:

```text
~575 → 500
~575 → 5000
```

depending on branch.

Its result feeds a visibility/occlusion-related virtual call at:

```text
vtable +0x280
```

Do not equate that call itself with the world-depth test without further proof.

## `sub_56137D`

Known range:

```text
~3500 → 3200
```

It affects:

- `InfoBar.opacity`;
- `InfoBar.field_F0`.

## `sub_560CFD`

Known range:

```text
~5000 → 4700
```

It uses an opacity accumulator in `xmm6`.

This path also intersects one of the health through-wall experiments documented in
[`occlusion.md`](occlusion.md).

## `sub_560FB3`

Uses a register-supplied range and applies a fade multiplier of:

```text
0.9
```

Its opacity accumulator is in `xmm7`.

# Fade input value

`sub_55FBD0` provides a per-object value consumed by these fades.

It is a multi-vtable query.

Do **not** reduce it to "raw Euclidean distance" without further proof.

The name fade system is therefore best documented in terms of the recovered caller ranges and observed
visibility behavior, not by assigning unsupported geometry semantics to `sub_55FBD0`.

# Full-visibility overrides

Confirmed conditions that can force full name visibility are:

- selected;
- combat target;
- personal target / call target;
- mouseover;
- near horizontal camera center.

These override the ordinary visibility fade.

Targeted units therefore remain visible farther away in vanilla behavior.

When the shared fade paths are neutralized, targeted and untargeted units at the same distance behave
identically. The target-range extension is part of the shared fade/override system rather than a
separate residual rendering path.

# `g_AsContext` mappings

Known mappings:

| vtable slot | manager field | confirmed semantic |
|---|---|---|
| `+0x40` | `+0x368`, fallback through `+0x2B8` / `+0x298` | effective target |
| `+0x70` | `+0x250` | call/personal target |
| `+0x88` | `+0x130` | mouseover |
| `+0x60` | `+0x150` | unknown |

The `+0x60` getter was observed null in tested normal states.

The camera-center override source is still unresolved.

Do **not** assign the camera-center override to `+0x60` merely because `+0x60` remains unknown.

# Visibility behavior after fade neutralization

Neutralizing the five `Clamp01Fade` paths keeps names visible until the underlying agent/model
render/unload range while preserving occlusion.

This establishes a useful separation:

```text
visibility alpha fade
    removable independently

world-depth occlusion
    remains
```

It also confirms that the five callers belong to the name visibility system rather than a generic
global UI fade mechanism.

# Name occlusion handoff

`NameRenderCtx.agent` gates the projection/world-position block that leads into the native world-space
rendering behavior.

Forcing the null/skip behavior produces through-wall names.

That mechanism belongs to the occlusion story and is documented in detail in
[`occlusion.md`](occlusion.md).

The important boundary here is:

```text
Clamp01Fade != world-depth test
```

and:

```text
distance brightness dimming != occlusion
```

# Emitter and submission

`NameDraw` uses the shared native emitter for its confirmed selection-decoration and conditional
level-text/auxiliary elements:

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

The confirmed name-side call uses:

```text
spaceFlag = [obj+0x60]
arg9 = 0
```

The full emitter reconstruction is documented once in
[`healthbar-rendering.md`](healthbar-rendering.md).

# Render layer

The name uses render layer:

```text
2
```

Known InfoBar layers are documented in [`pipeline.md`](pipeline.md). The layer is queue/order/sort
state.

It is **not** the depth-test selector.

# Name color inputs

The name path's color and brightness inputs come from several independent sources:

- per-category settings used by the `"teamColor"`-anchored path;
- selected/unselected brightness via `k_UnselectedBrightness`;
- distance brightness attenuation via `k_DimDistStart`, `k_DimDistRange`, and `k_DimCap`;
- full-visibility override state from `g_AsContext`;
- opacity/fade accumulation through the `Clamp01Fade` callers.

Do not flatten these into one "name alpha" or "name brightness" field.

# Name-category resolution

`ResolveNameCategory` dispatches on `NameRenderCtx.agent` type. Its build-`205.780` control flow is:

```text
null agent
    -> 8

type 0 Character
    -> ResolveType0Wrapper
    -> context-manager raw-category virtual at vtable +0xB0
    -> if presentation-state bit 14 is clear, 2 and 3 become 4
    -> otherwise return the raw value unchanged

type 0x0A
    -> ResolveType0AWrapper
    -> three virtual predicates select 5, 7, or 8

all other types
    -> 8
```

The type-`0x0A` return values are synthesized by `ResolveNameCategory`; they do not pass through the
context-manager virtual. Conversely, values observed on type-0 Characters are virtual-call results,
subject only to the explicit `2`/`3` normalization above.

`NameRenderCtx_RefreshState` stores more than this presentation category. Build-`205.780` disassembly
confirms the following contiguous integer handoff immediately before `NamePresentationUpdate`:

| offset | confirmed producer |
|---|---|
| `+0xC0` | `ResolveNameCategory(NameRenderCtx.agent)` |
| `+0xC4` | the context collection's `+0x98` object is queried through virtual `+0x60`; the returned object's virtual `+0x1D8` is then queried with the agent; defaults to `6` when either provider is absent |
| `+0xC8` | agent-derived display level from `sub_399760(NameRenderCtx.agent)`; zero suppresses the level presentation path |
| `+0xCC` | `sub_399170()` when the owning widget's property key `0x400` is active |
| `+0xD0` | defaults to `2`, with an optional value supplied through the object cached at `NameRenderCtx+0xF0` |

These are distinct inputs even when two happen to carry the same number. In the build-`205.780`
personal-target capture, the matching live context held `6` in both `+0xC0` and `+0xC4`; only `+0xC0`
is established as `NameCategory.Neutral`. The semantic enum domains of `+0xC4/+0xC8/+0xCC/+0xD0`
remain unresolved.

Build-`205.780` disassembly and one live pre-call capture resolved a concrete type-0 virtual-slot target
to `sub_11B0DE0`. Static disassembly bounds that function's direct outputs to
`{0,1,2,3,4,5,6,7,8,9,10}`. Its final four-way classification switch maps inputs as follows after
earlier override paths have failed:

| attitude | fallback result |
|---|---:|
| `Friendly` (`0`) | `5` |
| `Hostile` (`1`) | `7` |
| `Indifferent` (`2`) | `8` or `9` from one queried flag when an earlier getter returns the sentinel `0x80000000`; otherwise `7` |
| `Neutral` (`3`) | `6` |

The switch input is the `Attitude` byte returned by `ChCliContext_GetAttitude` at the context vtable
`+0x50` slot. The default arm asserts through the native `ChCliContext.cpp:1206` path. Earlier branches
can return `1`, `2`, `3`, `4`, `8`, or `10` before this switch. Their virtual predicates remain
unresolved.

The category-`3` override starts from the local Character wrapper at context `+0x98`, calls
`ChCliCharacter::GetKennel` at virtual `+0x78`, and asks `ChCliKennel::MatchesAgent` at virtual `+0xD0`
about a value derived from the target wrapper. Original assertion strings identify
`ChCliCharacter+0x3F8` as `m_kennel`, and the `0x1B8`-byte kennel retains `m_ownerCharacter` at `+0x188`.
The predicate dispatches on target agent type, follows Character-linked objects, and handles a Gadget
path with an additional context query. A true result returns category `3`. This establishes a native
kennel/owner relationship query and explains why grouped players and a solo player's own minion can
share the bucket; it does not prove that every category-`3` result has a single party, squad, ownership,
team, or affiliation meaning.

In the `Attitude.Indifferent` `8/9` path, wrapper virtual `+0x2C8` is called with argument `1`; bit 21 of
its result selects `9`, and a clear bit selects `8`. This statically confirms the category-`9` path
independently of the live critter captures. Category `10` is a direct output of a preceding state branch
and passes unchanged through `ResolveNameCategory`, but its semantics have not been observed live.

The category-`4`/`10` decision belongs to the earlier special-record path. These methods receive the
Character's embedded interface at primary-object `+0x08`. Embedded-interface virtual `+0x68`
(`sub_11D53B0`) admits only wrappers where `(interface.field_0x98 & 0xF0000000) == 0x30000000`;
that storage is primary `ChCliCharacter+0xA0`. Virtual `+0x18` (`sub_11D3CB0`) supplies the lookup index from
interface `+0x218` (primary `+0x220`), or derives it from interface `+0x98` (primary `+0xA0`) for that
flag class. Virtual `+0xE0` (`sub_11D3C70`) maps that index through
`ChCliContext::GetPlayerByListIndex`, which indexes the `ChCliContext` player-wrapper array at `+0x80`. A getter on the returned record yields another object; that object's
virtual `+0x1C0` selects category `10` when true and category `4` when false. The concrete record/object
vtable and semantic meaning of the final predicate are not recoverable from the module-only dump, so
category `10` remains unnamed.

`sub_11B0DE0` is not InfoBar-only logic. `sub_11B0C10`, reached through `ChCliContext` vtable `+0xB8`,
is a broader agent-type dispatcher. It handles non-type-0 agents locally and, for a valid type-0
Character wrapper, tail-jumps through the current context's vtable `+0xB0` slot. The captured context's
slot targeted `sub_11B0DE0`. Its corrected direct-output reconstruction includes the raw category `9`
observed in the later selected-critter captures; those captures do not require another implementation.
`ScoreMouseoverCandidate` consumes this generic dispatcher while evaluating mouseover eligibility.

# Known name categories

Confirmed categories relevant to the occlusion path are:

| category | value |
|---|---:|
| `GroupedPlayer` | `3` |
| `FriendlyPlayer` | `4` |
| `FriendlyMinion` | `5` |
| `FriendlyNpc` | `6` |
| `Hostile` | `7` |
| `Neutral` | `8` |
| `Critter` | `9` |
| `Unknown10` | `10` |

The `FriendlyPlayer` value is produced by `ResolveNameCategory` for the type-0 relationship/category
path rather than being an addon-only classification. Category-specific through-wall behavior belongs in
[`occlusion.md`](occlusion.md).

Build `205.780` custom-arena spectator captures found no separate blue/red name category for the
camera-followed player. A blue-side followed Character produced final `FriendlyPlayer` (`4`) in 50
filtered NameRender samples. A different red-side followed Character produced raw and resolved category
`4` in 50 wrapper-filtered provider samples; its final NameRender context was not observed during the
bounded filtered capture. Both team sides used the same spectator-follow slots in `AsContext`, so team
color is not encoded by this category or by a distinct observed target slot.

Build `205.780` live A/B/A captures confirmed `GroupedPlayer` (`3`) as an additional render category for
the same friendly Character while that Character was in squad; the grouped render disappeared after
leaving squad while the ordinary `FriendlyPlayer` render remained. A separate party join/leave/rejoin
capture tracked the same agent and wrapper and switched its grouped context and child presentation with
party membership. Together these controlled squad and party captures confirm the grouped-player role.
Static disassembly confirms that `ResolveNameCategory(NameRenderCtx.agent)` supplies
the value stored at `NameRenderCtx.category +0xC0`. For type-0 Characters, the helper first resolves a
raw relationship/category through the game-context manager and then consults a separate presentation
state bit before returning it. Build `205.780` resolves that normalization gate to
`NameCategoryPresentationState+0x18 bit 14` via vtable `+0xB8` (`NameCategoryPreserveRelationshipVariants`): when the bit is clear,
raw values `2` and `3` are normalized to `FriendlyPlayer` (`4`); when the bit is set, those raw values
are preserved. The presentation-state updater derives bit 14 inversely from a query reached through
`PvpCliContextAccessor` and a PvP-context virtual at `+0x130`. The exact semantic name
of that PvP query remains unresolved.

A later build-`205.780` live capture observed `GroupedPlayer` while the player was in a party rather than a
squad. Two `NameRenderCtx` instances alternated for the same Character agent: one submitted child `1`,
and the other submitted children `4`, `5`, and `10`. Both contexts retained category `3`. Together with
the earlier squad capture and the subsequent same-agent party A/B/A, this supports the promoted
`GroupedPlayer` name.

Category `3` is not restricted to player agents. In build `205.780`, a user-spawned and selected minion
was a distinct type-0 Character agent and produced raw category `3` in 66 wrapper-filtered provider
samples with relationship variants preserved; five exact final NameRender samples also returned
`GroupedPlayer` (`3`). The spawning player was in neither a party nor a squad, so category `3` does not
require either form of player-group membership and was not inherited from the owner's current party or
squad state. The enum name describes the previously confirmed player presentation/relationship case,
not a guarantee that the underlying Character is a player or currently grouped. The exact broader
relationship semantic remains unresolved.

The same build provided controlled contrasting samples from two friendly minions that the user
identified as belonging to other players. The first was type-0 Character agent `0x1EE`; the independent
second was type-0 Character agent `0x3DB`. For each minion, five wrapper-filtered provider samples
returned raw and resolved category `5`, and five exact final NameRender samples retained category `5`.
These independent identities confirm the `FriendlyMinion` role for category `5`. The user's own solo-
spawned minion instead used raw and final category `3`, so entity kind alone does not select category
`5`; the local owner/relationship case can select the category-`3` presentation. The exact native
relationship predicate remains unresolved.

A build-`205.780` live capture additionally confirmed the native source boundaries above. In one
current scene, type-0 Character wrappers produced raw values `4` and `5` with presentation-state bit
14 set. A filtered capture correlated one raw-`5` wrapper back to the exact
`NameRenderCtx.agent`, agent id, and `NameRenderCtx.category`; the render category remained `5`.
Separate `NameRender` captures observed type-`0x0A` agents at category `8`. A later exact target
correlation identified one of them as a user-selected named Gadget: its selection/target pointer matched
`NameRenderCtx.agent`, agent type `0x0A`, and final category `8` across four filtered events. These
observations confirm the numeric values and their data flow, but not the shared presentation semantics of
category `5`; those captures alone did not establish the later-promoted broad `Neutral` semantic for `8`.

A second build-`205.780` selection correlated a user-identified object with a visible healthbar to a
different type-`0x0A` Gadget agent. Its exact selected-unit pointer matched `NameRenderCtx.agent`, and
50 filtered final-render samples all returned category `8`. The healthbar is an additional presentation
property of this Gadget example; it does not narrow the shared category-`8` semantic by itself.

Together with the independently selected neutral Character and the earlier named Gadget, these captures
support the broad name `Neutral` for category `8`. The name deliberately does not say `NeutralNpc` or
`Object`: the confirmed native bucket spans both Character and Gadget agent types, and the resolver can
also synthesize `8` on null/default paths.

Build `205.780` live captures also correlated `FriendlyNpc` (`6`) with two exact user-identified friendly
NPCs. The first was static; the second moved and was held through the personal-target slot. They had
distinct agent ids and were captured in separate process lifecycles. In each case the selection/target
unit pointer exactly matched `NameRenderCtx.agent`, and wrapper-filtered outer-resolver captures repeatedly
confirmed raw category `6` reaching final category `6`. Movement therefore does not distinguish the two
confirmed category-`6` examples.

Build-`205.780` captures confirmed `Critter` (`9`) using two distinct user-identified critters. For each,
the selection/call-target unit pointer exactly matched a type-0 Character `NameRenderCtx.agent`; eight
filtered render events returned final category `9`, and five outer-resolver events for the exact wrapper
returned raw and resolved category `9`. The independent agent identities (`0xC88` and `0x126B`) confirm
the critter role rather than a one-unit correlation.

A build-`205.780` exact target correlation confirmed `Hostile` (`7`) on a user-identified hostile NPC.
The selection/target pointer matched a type-0 Character `NameRenderCtx.agent`; four filtered render
events returned final category `7`, and three outer-resolver events for its exact wrapper returned raw
and resolved category `7`. This disproves the earlier player-specific `EnemyPlayer` interpretation. The
category is relationship/presentation-level and is not restricted to player agents. A second exact
build-`205.780` correlation extended the same bucket to a user-identified enemy pet: its distinct type-0
Character agent produced raw and resolved category `7` in five provider samples and retained final
category `7` in five NameRender samples.

Do not infer semantics for additional numeric categories without direct evidence.

# Known boundaries — do not conflate

- `NameRender` != `NameDraw`
- selected brightness `0.7` != distance brightness dimming
- selected brightness `0.7` != visibility alpha fade
- `k_DimCap` != fade alpha
- distance brightness dimming != visibility alpha fade
- `Clamp01Fade` != world-depth test
- render layer `2` != depth selector
- `sub_55FBD0` != proven raw Euclidean distance
- `g_AsContext.vtbl+0x60` != confirmed camera-center source
- `NameDraw` auxiliary `EmitDrawQuad` != general visible-name signal

# Open leads

Useful unresolved areas:

- exact semantic meaning of `sub_55FBD0`;
- exact per-caller mapping between fade sites and override categories;
- exact source of the near-horizontal-camera-center full-visibility override;
- semantic identity of `g_AsContext.vtbl+0x60` / manager `+0x150`;
- complete category mapping for `NameRenderCtx.category`;
- which downstream child state consumes the projection presentation vector as actual render opacity/geometry and where the native obstruction result;
- exact division of responsibility between text-specific rendering and the shared quad/native renderer;
- exact native ABI contract required for a custom name-render state handoff.

The stable implementation model is to keep selection brightness, distance brightness attenuation,
visibility fade, projection/occlusion, and native submission as separate stages rather than treating
"visibility" as one monolithic value.

# Native text submission boundary

The build-`205.780` static path now identifies the name text handoff far enough to separate it from
the direct quad path. `NamePresentationUpdate` (`0x14039C4F0`) updates its child controls through
the generic frame messages:

- `sub_141042D90` sends message `0x5D` with a `codedText` value;
- `sub_141042CF0` sends message `0x57` with the child text/value payload;
- `sub_14106E770` sends message `0x36` for child visibility state.

`NameRender` (`0x14039D0B0`) supplies the projected presentation vector to the child controls through
their virtual methods at `+0x68` and `+0x78`. The CtlText callback family consumes that state rather
than calling `EmitDrawQuad` for the ordinary name glyphs. In particular, the CtlText message
dispatcher at `0x1410446B0` reaches text layout callbacks that call:

```text
CtlText callback
    -> sub_14106AF90 / sub_14106F280       (FrText API)
    -> sub_141071AA0                       (coded text, style, and line layout)
    -> sub_140AD6A80 / sub_140AD6BC0        (GrFont glyph-model creation)
    -> sub_141074A80                       (FrContent model-group submission)
```

`sub_141071AA0` resolves the active text style, applies color/alpha and font height, splits the
coded string into layout runs, and builds the per-run models. `sub_141074950` is the separate
`FrameContentParams`/type-9 quad path; it is not the text model append boundary. The exact semantic
mapping of every name child id and the final render/depth state are still unresolved. This is nevertheless enough to
establish that a managed GUI `Text` call cannot be a faithful first replacement: it would bypass the native
font/layout, world-space presentation, and frame-content submission path.

Font coverage is resolved below the `FrText` layer: `sub_140AD67F0` selects a `GrFont` range by the
original UTF-16 code unit, `sub_140AD7AA0` lazy-loads the range asset, and `sub_140AD6A80` consumes
the resulting glyph records. Build 205.780 has fixed native registration slots for Greek/Cyrillic and
kana as well as Han. A null glyph lookup only advances the pen and emits no pixels, so the tested
unsupported UTF-16 characters appear as blank spaces rather than fallback squares. Literal `U+25A1`
is a separate non-null square glyph. The full proof and descriptor table are maintained in
[`native-text-rendering.md`](../UI/Frames/native-text-rendering.md#grfont-glyph-coverage-and-font-registration).

For the first replacement milestone, the native CtlText/frame path must either remain the renderer
or be reproduced at this boundary with the same inputs. The shared `EmitDrawQuad` hook is therefore
not a complete name replacement boundary; it covers direct quads such as health, selection decoration,
and auxiliary name effects, but not the ordinary name glyph stream.
