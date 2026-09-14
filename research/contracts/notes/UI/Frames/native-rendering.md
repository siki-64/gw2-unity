# Native GUI rendering foundation

**Confirmed build:** 205.780.<br>
**Status:** current-build renderer, queue, traversal, pooled-model, and FrCache-to-device stages recovered; the C# native-window submission path is implemented behind a signature-gated observer, while live acceptance and resource ownership remain open.<br>
**Scope:** static Ghidra evidence, the live-validated healthbar submission path, and the unvalidated C# native-window implementation.<br>

This note tracks the minimum native surface required to render client-owned GUI primitives through the
game's frame renderer. It intentionally separates frame lifecycle, content submission, materials, text,
and input.

## Renderer layering

BGFX is not the widget-facing renderer API. The confirmed path contains ArenaNet frame and graphics
layers between widget behavior and the backend:

```text
widget / control behavior
  -> FrApi / FrFrame content submission
  -> FrModel builder
  -> GrModel / GrMaterial and submodel shader-input records
  -> FrContent traversal / transform binding
  -> FrCache invalidation and queue flattening
  -> GrDev model batching / command construction
  -> AMAT/BGFX techniques, passes, effects, and shaders
  -> backend graphics submission
```

`BgfxDraw.cpp` reads the `GrModel` shader-input records produced above it. For example, token
`0x8D80FCFC` is validated as a vector constant and uploaded to the material's `control` uniform. This
establishes BGFX as a lower material/shader/backend layer, not the native widget or layout system.

The client-facing abstraction should therefore target `FrApi` submission plus managed material handles.
It should not expose BGFX shader packages or D3D11 resources directly.

## Confirmed submission chain

The generic content submitter is:

```text
sub_14106A400(frameId, FrameContentParams*)
```

Its assertions identify `Gw2\\Engine\\Frame\\FrApi.cpp`, lines `0xCB0..0xCB3`. The function validates:

- `frameId != 0`;
- `Layer != 10` (`FRAME_CONTENT_LAYERS` has ten slots, indexed `0..9`);
- `Material != nullptr`;
- `Rect.X1 >= Rect.X0` and `Rect.Y1 >= Rect.Y0`.

The current-build chain is:

```text
sub_14106A400                         generic FrApi content submission
  -> sub_141082170(frameId)           validate and resolve FrFrame*
  -> sub_14107DB80(...)               build/reuse type-9 GrModel
  -> sub_141074950(frame+0x108, ...)  append model to the frame content queue
```

The first two arguments use the Windows x64 ABI as `ECX=uint32 frameId` and
`RDX=FrameContentParams*`.

## Frame id resolution

`sub_141082170 @ 0x141082170` validates the id through the manager rooted at
`0x142893C10`. The underlying direct-index resolver is `sub_141081390 @ 0x141081390`:

```text
manager +0x08  object pointer array
manager +0x14  bounds/count
array[id]      FrFrame*
```

Id zero is rejected by the FrApi submitter. A valid pointer in this table does not by itself establish
that a frame is visible, alive for the next frame, or safe for client ownership.

## `FrFrame` renderer-facing layout

The partial current-build layout is modeled by `Gw2.Contracts.FrFrame`:

| offset | type | role |
|---:|---|---|
| `+0x30` | `float` | absolute screen X0 |
| `+0x34` | `float` | absolute screen Y0 |
| `+0x38` | `float` | absolute screen X1 |
| `+0x3C` | `float` | absolute screen Y1 |
| `+0x60` | `nint` | parent relationship pointer (parent frame `+0x60`) |
| `+0x108` | `FrFrameContentQueue` | inline per-frame content queue |
| `+0x158` | `float` | content opacity multiplied through the ancestor chain |
| `+0x29C` | `FrFrameState` | Created `0x4`, Destroying `0x8`, Hidden `0x200` |
| `+0x2A0` | `uint` | creation flags; opacity walk tests low-byte mask `0x44` |

The inline queue occupies `0x68` bytes, ending before frame `+0x170`; opacity is queue `+0x50`.
Build 205780 child creation allocates `0x2E0` bytes, now reflected in `Gw2.Contracts.FrFrame`.
The former queue-size interpretation included unrelated frame state. See [FrApi](frapi.md) for
constructor evidence, frame ID, pending-list links, and recovered lifecycle operations.

## Frame content queue

`sub_141074950 @ 0x141074950` appends a model to a selected layer. The queue layout is:

| queue offset | role |
|---:|---|
| `+0x00` | optional ordered-entry array header |
| `+0x30` | array header for `0x20`-byte layer objects |
| `+0x50` | content opacity |
| `+0x58/+0x60` | intrusive pending-list links |

The opacity path also reads containing-frame state at frame `+0x29C` (queue-relative `+0x194`);
bit `0x400` changes opacity application. This is outside the queue's `0x68`-byte storage.

Each layer object is an array header whose elements are `0x18` bytes:

```text
+0x00  GrModel*
+0x08  uint32 type             // caller passes 2
+0x0C  uint8 opacity
+0x10  uint32 unresolved       // caller passes 0
```

`sub_141075CE0 @ 0x141075CE0` consumes this queue after submission. It walks layers from the highest
index down, walks each layer's entries in insertion order, toggles clipping around entries whose type is
`1` through `sub_141082B80`, and binds the current graphics transform to every model with
`sub_140A85050 @ 0x140A85050`. This function does not itself call the low-level device draw routine.

For the type-`2` path emitted by `sub_14106A400`, `sub_141075830 @ 0x141075830` returns each model to
the frame-model pool through `sub_14107E040`. The pool is drained during frame teardown by
`sub_14107DF40`; these model pointers are therefore frame-lifetime values and must not be retained by
client code.

When `FrameContentParams.OrderingKey != -1`, a second `0x10`-byte entry is appended:

```text
+0x00  int32 ordering key
+0x08  GrModel*
```

The outer layer array is grown to `Layer + 1`; this independently confirms ten valid content layers and
the assertion against layer `10`.

## `FrameContentParams`

The completed descriptor is exactly `0x130` bytes. Current-build use in `sub_14106A400` confirms:

| offset | role |
|---:|---|
| `+0x00` | optional ordering key; `-1` disables the ordered-entry append |
| `+0x04` | content layer |
| `+0x08` | retained material pointer |
| `+0x10` | two material-related 32-bit values, still exposed opaquely by the existing emitter ABI |
| `+0x18` | packed model color |
| `+0x1C` | screen/model rectangle |
| `+0x2C` | nonzero enables an additional model transform path |
| `+0x30` | float2 transform origin; two infinities request rectangle half-size |
| `+0x38` | rectangle-adjustment flag |
| `+0x3C` | float4 shader-control input bound with token `0x8D80FCFC` |
| `+0x4C..+0x6B` | four float2 inputs associated with the four texture-coordinate transforms |
| `+0x6C..+0x12B` | four `0x30`-byte texture-coordinate transforms |

Four trailing bytes complete the `0x130` descriptor but remain unresolved.

## Model construction

`sub_14107DB80 @ 0x14107DB80` identifies `Gw2\\Engine\\Frame\\FrModel.cpp` through assertions at
lines `0xEB`, `0xF5`, and `0xF6`. It:

- allocates or reuses a pooled graphics model;
- tags it with `0x66726D65`;
- requires exactly four texture-coordinate transforms;
- applies the packed color to the model;
- builds rectangle geometry from X/Y and width/height;
- binds shader token `0x8D80FCFC` to the supplied float4 control value.

The model is pooled. No client code should retain the returned pointer across frames.

The material pointer is not the `DAT_142893888` resource used as the default rectangle geoset.
`sub_140A83190` creates a type-9 model from that geoset plus a one-element material-handle array;
when a pooled model is reused, `sub_14107DB80` calls `sub_140A856F0` to replace the material on each
submodel. The lower helper `sub_140A8BAE0` retains the new handle and releases the previous one on
the direct shared-handle path, with a separate material-resolution path for other model flags. This
is why a native GUI adapter must use a live game-owned material handle and must not treat the
rectangle geoset pointer as a substitute material.

The promoted partial `Gw2.Contracts.GrModel` view records the fields directly used by the frame path:

| offset | role |
|---:|---|
| `+0x14` | graphics model type; frame helpers require `9` |
| `+0x30` | value written as `0x66726D65` (`'frme'`) by `sub_140A84CD0` |
| `+0x38` | unknown model state word; mask `0x2` gates the frustum/material path in `sub_140A883D0`/`sub_140A8BAE0` |
| `+0x40` | current transform handle replaced by `sub_140A85050` |
| `+0x48` | model flags word manipulated by frame-model flag helpers |
| `+0x58` | pointer to `0xE8`-byte submodel array |
| `+0x64` | submodel count |

Within each submodel, `+0x88` is the packed color/opacity value and `+0x90` tracks the texture-transform
input count. The remaining model and submodel fields stay opaque.

## Post-queue renderer boundary

The ordinary FrContent walk does not call `sub_140A6D150` directly. The current-build handoff is a
two-stage cache path:

```text
sub_141075FC0 / sub_141075CE0
    -> bind model transforms and consume FrFrameContentQueue

sub_141073900
    -> enumerate cached frames
    -> append FrCache operations at 0x1428929A0
    -> flatten each changed frame queue with sub_141075230

sub_1410742F0
    -> initialize/rebuild cache state when needed
    -> sub_141074350

sub_141074350
    -> op type 0: invalidate/rebuild a frame cache
    -> op type 1: draw cached model array with sub_140A6D150
    -> op type 2: set viewport with sub_140A6DB90

sub_140A6D150(count, GrModel**, render-context/state, 0x1008000)
    -> sub_140B09600
    -> sub_140A883D0 per model (filter, frustum, sort, batch records)
    -> sub_140B08760 / sub_140B0BE70
    -> BGFX DDI command buffer
```

`sub_141075230 @ 0x141075230` copies the frame's root content handle and the selected layer's
`GrModel*` entries into a cache buffer. It also ORs each entry's low flag bit into a caller-provided
dirty word. `sub_141073900 @ 0x141073900` emits a type-2 viewport operation when a cached frame's
rectangle changes and emits type-0/type-1 operations when frame state or flattened content changes.
The operation records are 0x10 bytes in the global list rooted at `0x1428929A0`:

```text
+0x00 uint Type              // 0=invalidate, 1=model range, 2=viewport
+0x04 uint IndexOrStart      // cache-frame index for type 0/2; model-array start for type 1
+0x08 uint Count             // model count for type 1; otherwise unused
+0x0C uint CacheFrameIndex   // type-1 source cache frame; otherwise unused
```

`sub_141074130` coalesces adjacent type-1 records when their cache-frame index matches, appending the
model pointers into the array rooted at `0x142892980` and extending the prior record's count. The
temporary per-frame flatten buffers are 0x20-byte records; their data pointer, capacity, and count
are at `+0x08`, `+0x10`, and `+0x14` respectively.

`sub_141074350 @ 0x141074350` is the confirmed FrCache draw consumer. Its type-1 branch calls
`sub_140A6D150`; the latter enters a native subframe, calls `sub_140B09600`, and closes the subframe.
`sub_140B09600` requires every pointer to be a type-9 model and invokes `sub_140A883D0`. The model
routine performs visibility/frustum/sort checks and builds the renderer's batch records; it is not a
standalone immediate draw. `sub_140B08760` serializes those records and `sub_140B04CC0` drains the
BGFX DDI command buffer.

## Upstream BGFX name correlation

The game contains ArenaNet wrappers and a modified `BgfxDdi`/`BgfxDraw` layer, so upstream names are
semantic labels for the recovered behavior, not replacement symbols or a claim that the binaries use
the public BGFX ABI. The useful correlation is:

| current-build routine | evidence-backed local role | closest upstream BGFX concept |
|---|---|---|
| `sub_140B08760` | collect the prepared model batches, establish per-batch bindings, and hand the linked draw records to the command encoder | render-item finalization around [`Encoder::submit`](https://github.com/bkaradzic/bgfx/blob/master/src/bgfx.cpp#L4353-L4368) |
| `sub_140B0BE70` | `GrDevWin360` command encoder; selects the AMAT effect key and emits state, uniform, binding, geometry, and draw records | the aggregate of [`Encoder::setState`/`setTexture`/`setVertexBuffer`/`setIndexBuffer`](https://github.com/bkaradzic/bgfx/blob/master/src/bgfx.cpp#L4194-L4346) plus [`Encoder::submit`](https://github.com/bkaradzic/bgfx/blob/master/src/bgfx.cpp#L4353-L4368) |
| `sub_140B04CC0` | consume the byte-counted `BgfxDdi` stream and dispatch each encoded record | [`Context::rendererExecCommands`](https://github.com/bkaradzic/bgfx/blob/master/src/bgfx.cpp#L3245-L3320), with a game-specific DDI record format |
| `sub_140B03890` | decode one DDI record and dispatch its type-specific handler | the per-command switch inside `Context::rendererExecCommands` |
| `sub_140AB4A60` | `BgfxDraw.cpp` frame/backend submission body; applies frame state and executes the prepared draws | [`RendererContextI::submit`](https://github.com/bkaradzic/bgfx/blob/master/src/bgfx_p.h#L4414-L4460), concretely [`RendererContextD3D11::submit`](https://github.com/bkaradzic/bgfx/blob/master/src/renderer_d3d11.cpp#L6115-L6159) |
| `sub_140B03C80` | higher flush wrapper: drain DDI, invoke the draw/backend body, and record timing | [`Context::renderFrame`](https://github.com/bkaradzic/bgfx/blob/master/src/bgfx.cpp#L2829-L2894) (`rendererExecCommands` -> backend `submit` -> post commands) |

Two details matter for the replacement design. First, `sub_140B0BE70` is not a public
`bgfx::submit` thunk: it serializes an ArenaNet `GrDevWin360` record whose effect key and material
inputs are already selected. Second, `sub_140B04CC0`/`sub_140B03890` are not safe client entry points;
they require the game-owned DDI buffer, bind table, and active `BgfxDraw` state. Upstream BGFX makes
the same ownership split explicit: API-side encoders accumulate state and render items, `Context::frame`
publishes a frame, and the render thread later calls `renderFrame` and the backend `submit` method.

For the native UI path, the safe static conclusion is therefore to enqueue `GrModel` content at the
FrContent/FrCache boundary and let the existing `GrDev`/BGFX stages execute it. Calling a guessed
upstream-equivalent routine from the DXGI Present hook would bypass the frame/context ownership that
these routines assume.

Live trace confirmation (build 205.780, PID 12864, session-local) shows the consumer executing on the
same render/UI thread as the FrContent path. Four read-only hits at `sub_140A6D150` ran on thread
`13108`, all called from RVA `0x107457C` (the type-1 branch of `sub_141074350`); the observed calls
passed model counts `0x6C`, `1`, `0x13`, and `1`, model-array pointers in `RDX`, render context/state
`0x2243C127570` in `R8`, and flags `0x1008000` in `R9`. This validates the static operation-to-device
handoff, but not a client-call ABI or ownership contract.

The same session's live cache snapshot showed the expected operation records: a leading type-2
viewport operation followed by type-1 model ranges `(start=1,count=14)`, `(14,1)`, and `(15,19)` in
the model-pointer array rooted at `0x142892980`. The addresses and ranges are session-local, but the
record shape agrees with `sub_141073900`/`sub_141074350` and the traced `sub_140A6D150` calls.

This is separate from `sub_140A6DDB0`, which flushes pending device state and also reaches
`sub_140B03C80`/`sub_140B04CC0`. Both paths are device-state sensitive and should not be called from a
DXGI `Present` hook without a proven native frame phase.

`sub_14106F090` itself is a phase wrapper, not just a passive callback site. Its current-build body:

```text
RCX = current WorldView/render context
  -> update DAT_1428927C0 (release old context, retain new context + 0x8)
  -> sub_140A6DDB0 (pre-traversal device flush)
  -> sub_141075FC0(XMM0/XMM1 viewport values)
  -> sub_141076FC0 / sub_1410785D0 (post-traversal cleanup)
```

The wrapper receives the two viewport floats in `XMM1`/`XMM2`, moves them to `XMM0`/`XMM1` for
`sub_141075FC0`, and preserves the render-context pointer in the global. `sub_141075FC0` stores
those values in `0x142892A68`/`0x142892A6C`; it recursively traverses only when they change, but it
always drains the pending frame-list nodes. This is why an adapter at traversal entry must be
idempotent even on frames where the root walk is skipped.

The caller `sub_140956330` invokes `sub_140A6DDB0` once more immediately after `sub_14106F090`.
Thus `sub_140A6DDB0` is a shared flush boundary around the traversal, while the explicit model draw
path through `sub_140A6D150` remains a different FrCache operation. A client callback should be
placed after the internal pre-flush and before `sub_141075FC0` (the call-return boundary at
`0x14106F0F4`, or a detour at `sub_141075FC0` entry), rather than at the start of `sub_14106F090`.
This preserves the current render context and lets newly queued models be consumed by the traversal.

The enclosing current-build view tick is `sub_14094E3F0` (`ViewAdvance`). Its relevant order is:

```text
ViewAdvanceUi
  -> sub_14106E8E0 (UI/frame state update)
  -> sub_140956330
       -> sub_14106F090
            -> sub_141075FC0 (FrContent traversal)
  -> ViewAdvanceAgentRender
       -> sub_140956060
            -> sub_14106EF10
                 -> sub_1410742F0 / sub_141074350 (FrCache draw)
```

This makes the post-flush/pre-`sub_141075FC0` boundary the leading callback candidate for a
native-content proof. It is still only a static candidate; the patch ABI and lifetime contract have
not been validated. A read-only trace at `sub_14106F090` (same build/session) recorded four hits on
thread `13108`, all from caller RVA `0x9563DF` in `sub_140956330`. The current frame manager also
showed index `1` pointing at the root returned by `sub_141086220`; this is a session-local root-id
hypothesis, not a stable id to hard-code.

The generic submitter was observed on that same thread before traversal. A 12-hit trace of
`sub_14106A400` saw valid payloads for runtime frame ids `0xBEB` and `0xF2E`, across layers `0..2`,
with different live material pointers and ordinary screen rectangles. These ids are ordinary UI
frames, not the root id `1`, and demonstrate that frame ids and materials must be discovered or
provided by the native frame path at runtime.

Tracing `sub_141075FC0` directly produced four more hits on thread `13108`, all returning to RVA
`0x106F0FF` (the call in `sub_14106F090`). The integer argument registers do not carry the root
frame pointer; the function resolves the root internally through `sub_141086220` and receives its
viewport values in XMM registers. This makes a traversal-entry detour a better ABI target than trying
to synthesize a call to the FrCache consumer.

Separately, `GrRender2d` end-of-task processing at `sub_140AFFE60` creates a fresh task through
`sub_140B00190`. It is a task/resource lifecycle path, not evidence that ordinary FrContent models are
submitted there. The confirmed ordinary queue-to-device route is the FrCache path above. The exact
relationship between the FrCache command stream and a particular `GrRender2d` task remains unresolved.

## C# native-window replacement boundary

The retired overlay path was structurally different from the native path:

```text
module callback
  -> managed layout/state in the client UI
  -> DXGI `Present` hook
  -> retired C++ overlay backend
  -> client-owned D3D11 render target
```

The current C# path is:

```text
native sub_14106A400(frameId, FrameContentParams*)
  -> managed C# callback once per game-thread generation
  -> managed layout/input and transient rectangle list
  -> client-owned solid material acquisition
  -> native FrameContentParams descriptor
  -> sub_14106A400 on the current validated child frame
```

The frame-content submission observer preserves the native call and invokes the managed callback with
the exact current frame id and descriptor. The submission path copies the descriptor, uses its live
material and frame id, and never retains native model/material pointers. The host-facing
`Gw2HostGui*` names are the current narrow ABI. Their host implementation is managed C# and forwards
into the managed GUI renderer; there is no separate overlay backend or second D3D11 draw path.

The C# UI keeps its managed rectangle queue as the window API and submits it with the recovered
neutral solid material. It does not borrow a live healthbar/PvP-panel `EmitDrawQuad` payload.
The recovered native text path is recorded separately in
[`native-text-rendering.md`](native-text-rendering.md); both paths share the validated frame/phase
ownership rules and fail closed when those guards are unavailable.

Calling `sub_14106A400`, `sub_141074350`, or `sub_140A6D150` from a DXGI `Present` hook remains unsafe:
the native frame id, active device state, cache lifetime, and material handle ownership are
phase-sensitive. The implementation therefore only submits from the observed native content phase and
preserves the original native payload on every disabled, unmatched, or failed path.

## What this enables

The healthbar experiment proved the generic descriptor path, and the solid-material route now submits
client-owned rectangles through that same `sub_14106A400` seam. Healthbar/PvP live-template replay is
retired from the GUI renderer.

It does **not** yet prove that the managed module surface is accepted across all UI states, materials,
resizes, or game builds. The next milestone is live validation across tooltip/map transitions,
layer ordering, and resize behavior.

## Remaining native GUI hardening

1. Live-validate the once-per-game-thread-generation callback and its ordering relative to
   `sub_141073900`/`sub_141074350` cache consumption.
2. Recover material acquisition and retain/release rules, beginning with a neutral untextured UI material.
3. Validate layer ordering, clipping/scissor ownership, opacity propagation, and resize behavior.
4. Validate the recovered native text measurement/glyph-model path for arbitrary client strings, and
   keep font-resource/range extension separate from generic renderer material recovery.
5. Keep mouse/keyboard focus and frame lifecycle separate from renderer submission until their ownership is proven.

Do not construct or destroy native frames yet. The current layouts make inspection and submission possible,
but the statically recovered [parent/child destruction path](frapi.md#build-205780-lifecycle-surface)
still needs callback ABI, resource-retention, and live client-owned lifecycle validation.

## Neutral solid-material acquisition

Build 205.780 exposes a dedicated solid-material cache through the same `McMaterial` service used by
ordinary filename-backed UI quads. This is distinct from borrowing a `FrameContentParams.Material`
pointer from whichever widget happens to submit first.

```text
sub_1402F48C0()
  -> McMaterial service
  -> vtable +0x48
  -> sub_1402F4430(service, uint32* solidValue, uint32 builtInShader)
       -> lookup in service +0x58 solid-material table
       -> cache miss: sub_1402F25B0
            -> acquire/create solid texture from solidValue
            -> select embedded material bytes through sub_1402F48D0(builtInShader, ...)
            -> sub_140AAA9E0(..., flags=0x2F000, ...)
       -> retain returned material handle at handle +0x08
```

`sub_1402F4430 @ 0x1402F4430` hashes the pair copied by `sub_1402F24F0`: the first
32-bit value is passed to the texture service on a cache miss and the second selects one of the
embedded built-in material definitions. Its table is the `m_solidMaterialTable` asserted at
`sub_1418EE080 @ 0x1418EE080` (`McMaterial.cpp:0x51B`). The filename-backed path used by
`EmitDrawQuad` is instead vtable `+0x38` / `sub_1402F32D0`, which reaches
`sub_1402F3560` and the service's filename-material table.

The returned object follows the native `Handle` convention already visible in `EmitDrawQuad @
0x1403A7B20`: the cache lookup increments the reference count at handle `+0x08`; after synchronous
`sub_14106A400` submission, the caller atomically decrements that count through
`sub_1409B48B0 @ 0x1409B48B0` and invokes handle vtable `+0x08` only when it reaches zero. A client
must mirror this scope and must not retain the handle across native UI traversals.

Built-in shader id `3` is used by concrete solid-quad callers. `sub_140378CF0 @ 0x140378CF0` is the
smallest useful arbitrary-color example: it acquires an opaque-white solid (`0xFFFFFFFF`, shader `3`)
once, then emits several differently colored rectangles by applying `sub_141070270` to each fresh
descriptor. `sub_140386C40 @ 0x140386C40` is an even smaller fixed-color example using solid key
`0xC0404040` for an ordinary 18-unit control segment.

There is no separate high-level FrApi solid-rectangle helper in these paths. The reusable native
recipe is the repeated sequence:

```text
sub_141069770(params)                 // complete 0x130-byte defaults
sub_141070040(params, layer)
sub_1410701C0(params, material)       // material +8, writes 4.0f at +0x10/+0x14
sub_141070280(params, x0/y0/x1/y1)
sub_141070270(params, packedColor)    // optional tint; required for arbitrary RGBA
sub_14106A400(frameId, params)
```

The managed rectangle queue uses the recovered neutral solid-material recipe during the validated
`sub_14106A400` callback. If material acquisition or root-viewport resolution
fails, the queue is dropped for that frame; there is no healthbar/PvP live-template fallback.
Absence, incorrect texture/color, flicker, duplication, a crash, or failure across a GW2 tooltip/map
transition rejects the candidate; the build/phase guard must keep the native route fail-closed while
retaining the original game payload.


## Managed GUI layer on top of the native renderer

The renderer remains the recovered FrApi/FrText path described above. The tool-window layer above it
is a managed immediate-mode GUI; ImGui is not part of the active architecture.

Build-205.780 hardening now includes:

- FrMouse pointer coordinates are read from raw X/Y at RVAs `0x02893B58/0x02893B5C` and multiplied
  by the native UI scales at `0x02893D38/0x02893D3C`, matching the transform in
  `sub_14107E440`; the result is bottom-origin like the native frame rectangles and is converted
  back through the root height for the managed top-origin hit test;
- managed rectangles are converted into the receiving child frame's local coordinates on both axes;
  omitting the receiving frame's `ScreenY0` shifts the displayed controls upward while leaving
  their managed hitboxes in place;
- Win32 `ScreenToClient` is only a fallback and is scaled by root logical size versus client pixels;
- client-window interaction bounds are published to a thread-local mouse hook so button/wheel messages
  do not click through into GW2 without taking WndProc ownership;
- child containers retain their own scroll offset/content extent, consume wheel input only while
  hovered, intersect their clip with the parent clip, and draw a managed scrollbar;
- sliders retain an active item across frames and report a change on every value-changing drag frame
  rather than only on the initial mouse-down edge;
- tooltips are real transient managed containers instead of no-op API calls;
- `FrameContentParams` colors are packed as native `AARRGGBB`. The GUI palette intentionally keeps
  its warm/brown panel treatment rather than relying on the previous accidental byte swap;
- the window shell no longer draws a second implicit title; the tool-UI layer owns the visible header.

These changes do not alter the native model/material lifetime rules. The GUI still queues value-only
rectangles/text and the runtime still acquires/releases native materials synchronously in the validated
submission phase.

The managed `CalcTextWidth` path still does not call the recovered native proportional measurement helper,
and native font scaling remains unrecovered. Until those managed-call contracts are validated, the layout
uses a proportional-width estimate and suppresses managed font-scale effects while the native text renderer
is active so hit boxes cannot diverge from an unscaled native font. Managed scrolling rejects text lines
that cross a child clip instead of pretending that a native FrText scissor ABI has been recovered.
