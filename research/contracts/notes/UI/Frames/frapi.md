# FrApi and generic frame surface

**Confirmed build:** 207.032.<br>
**Status:** partial frame layout, creation, visibility, recursive destruction, and content/cache paths recovered.<br>
**Unresolved:** complete callback ABI, resource retention, stable locators, and live client-owned frame lifecycle validation.

The generic frame subsystem is broader than the InfoBar-specific use of frame ids already documented in
[../Widgets/framework.md](../Widgets/framework.md). The imported evidence exposes a reusable FrApi-like
surface for resolving, creating, positioning, showing, messaging, and destroying native frames.

Current-build renderer-facing evidence is documented in
[`native-rendering.md`](native-rendering.md).

The current-build content path is now split into explicit phases:

```text
FrApi submission
    sub_14106A400(frameId, FrameContentParams*)
        -> pooled type-9 GrModel
        -> FrFrameContentQueue entry

FrContent execution
    sub_141075FC0
        -> recursive frame traversal
        -> reverse layer/entry walk in sub_141075CE0
        -> clip state and current-transform binding

FrCache/device execution
    sub_141073900 -> sub_141075230
        -> flatten changed frame queues into cached model-pointer ranges
    sub_141074350
        -> sub_140A6D150 for type-1 draw operations
        -> sub_140A6DB90 for type-2 viewport operations
        -> BGFX DDI command stream
```

Type-2 models from the generic quad submitter are returned to the native pool during
`sub_141075830`; they are not persistent frame-owned objects. The ordinary queue does not immediately
draw each model: `sub_141073900` flattens changed queues, and `sub_141074350` later submits cached
model ranges through `sub_140A6D150`.

## FrCache operation handoff

`sub_141073900 @ 0x141073900` maintains a global 0x10-byte operation list rooted at `0x1428929A0`.
It emits type-2 viewport operations for changed frame rectangles and type-0/type-1 operations for
cache invalidation and model ranges. `sub_141075230 @ 0x141075230` copies a frame root handle and
selected-layer model pointers into the cache model array rooted at `0x142892980`.

The operation record is four `uint32` values: `Type`, `IndexOrStart`, `Count`, and
`CacheFrameIndex`. Type 0/2 use `IndexOrStart` as the cached-frame index; type 1 uses it as the model
array start, `Count` as the model count, and `CacheFrameIndex` as the source frame. Type-1 records are
coalesced when the source frame matches. The intermediate flatten record is 0x20 bytes with model
data pointer/capacity/count at `+0x08/+0x10/+0x14`.

`sub_141074350 @ 0x141074350` consumes that list. Type 1 calls the native device draw
`sub_140A6D150`, which requires the active device and type-9 `GrModel*` entries; type 2 calls
`sub_140A6DB90` with a normalized viewport rectangle. This is the first confirmed queue-to-device
handoff, but its cache globals and render context/state are not a client-callable ABI. In a live build-207.032 trace,
`sub_140A6D150` ran on thread `13108` from caller RVA `0x107457C` with counts `0x6C`, `1`, `0x13`,
and `1`; the render-context/state pointer and `0x1008000` mode word were also stable across those calls.
This confirms execution of the type-1 handoff, not that a caller outside the game may invoke the
function directly.

The BGFX-facing names are documented as upstream semantic correlations in
[`native-rendering.md`](native-rendering.md#upstream-bgfx-name-correlation). ArenaNet's `GrDevWin360`,
`BgfxDdi`, and `BgfxDraw` layers are wrappers with a private command format; they must not be treated as
the public `bgfx::Encoder` or backend ABI.

## Candidate native frame phase

The current-build view tick is `sub_14094E3F0` (`ViewAdvance`). It updates UI/frame state through
`sub_14106E8E0`, enters `sub_140956330`, and reaches `sub_14106F090` immediately before
`sub_141075FC0` traverses the root frame. The later `ViewAdvanceAgentRender` branch calls
`sub_140956060`, which invokes `sub_14106EF10` and the FrCache draw consumer. A native GUI adapter
should therefore be scheduled after `sub_14106F090`'s internal `sub_140A6DDB0` pre-flush and before
`sub_141075FC0` (the internal call-return boundary at `0x14106F0F4`, or an equivalent traversal-entry
detour), not in DXGI `Present`. A read-only trace recorded four `sub_14106F090` hits on thread `13108`
from caller RVA `0x9563DF`; a separate trace of `sub_14106A400` on that thread saw frame ids `0xBEB`
and `0xF2E`. The live root table happened to map id `1` to the `sub_141086220` root in this session,
but neither that id nor the UI frame ids are a cross-session contract. The candidate callback still
needs ABI, material-retention, and lifetime validation before any additional GUI ABI is switched over.

Direct tracing of `sub_141075FC0` produced four hits on the same thread, all returning to RVA
`0x106F0FF` inside `sub_14106F090`. Its root frame is resolved internally, while viewport values are
passed in XMM registers; the integer argument registers are not a client-visible root-frame ABI. This
supports using the traversal-entry boundary as the eventual adapter seam, subject to an inline-hook
ABI proof. The traversal function may skip only the recursive root walk when its cached viewport
floats are unchanged; its pending-list drain still runs, so an adapter must avoid duplicate submission.

The supplied game image has PE timestamp `2026-08-19T18:55:40Z`; the imported evidence binary has PE
timestamp `2026-08-24T22:28:42Z`. Treat all semantic assignments below as build-local until reproduced
against the target image.

# Promoted frame layout

A generic frame carries its laid-out absolute screen rectangle at:

| offset | field |
|---:|---|
| `+0x30` | screen X0 |
| `+0x34` | screen Y0 |
| `+0x38` | screen X1 |
| `+0x3C` | screen Y1 |

This partial view is modeled by `Gw2.Contracts.FrFrame`.

Build 207032 child creation allocates `0x2E0` bytes. Additional promoted fields are:

| offset | field |
|---:|---|
| `+0x00/+0x08` | intrusive pending-list links; not a vtable |
| `+0x60` | parent relationship pointer (parent frame `+0x60`) |
| `+0x108` | inline content queue, size `0x68` |
| `+0x270` | frame ID (`uint32`) |
| `+0x29C` | frame state (`uint32`): Created `0x4`, Destroying `0x8`, Hidden `0x200` |
| `+0x2A0` | creation flags (`uint32`); opacity walk tests mask `0x44` |

`sub_141082A10` initializes the first two links to the frame and frame+1. The content constructor
`sub_141074720` initializes queue opacity at `+0x50` and pending links at `+0x58/+0x60`, ending at
`+0x67`. The old queue `Flags194` interpretation incorrectly included the containing frame state:
`0x108 + 0x194 = 0x29C`. Opacity belongs to the queue, not an overlapping frame declaration.
Unlisted bytes remain unknown; the allocation size does not imply a fully recovered layout.

# Build 207032 lifecycle surface

Addresses below are static-image VAs (image base `0x140000000`), not live process pointers. Names are
descriptive. Integer IDs/flags are 32-bit; pointer arguments follow Windows x64 calling conventions.
These signatures describe static evidence, not approved client-call contracts.

| VA | recovered operation |
|---|---|
| `0x14106B5D0` | create child: returns ID; arguments parent ID, flags, child code, procedure pointer, creation parameter |
| `0x14106C360` | find child ID by parent ID and child code; parent zero selects root |
| `0x14106E770` | set visibility by frame ID and integer boolean |
| `0x14106B320` | request content redraw by frame ID |
| `0x14106BC50` | destroy nonzero frame ID, including descendants |
| `0x14106BCA0` | destroy descendants without destroying the parent |

Creation selects the root for parent zero and adds flag `0x8`, normalizes creation flags, allocates
the frame, initializes its subsystems, and registers the parent/child-code relationship through
`sub_141084C80`. The callback registry is at frame `+0x220`, initialized by `sub_141081770`.
Initialization queries callback/base procedures with message `0x4`, rejects self-base recursion,
and sends special CREATE message `0xA`. Creation then sets state Created and sends message `0xB`.
The complete procedure/base-procedure ABI is not yet promoted.

Visibility tests Hidden (`0x200`) at `+0x29C`, toggles it when required, and sends message `0x36`
with the visibility boolean. State helper `sub_141086910` computes
`((old & ~clear) | set) ^ toggle` and drives subsystem updates, effective-visibility message `0x40`,
and descendant propagation. Direct state writes would bypass these effects.

Destruction resolves the ID and enters `sub_141069F60`: assert Created, set Destroying, send first-time
message `0x2`, recursively destroy children, tear down subsystems, unlink, and return storage to the
pool. It revalidates its ID after callbacks/recursion because callbacks can reenter destruction.
`sub_141081D20` sends DESTROY `0xE` to callbacks in reverse order and clears the registry.
Ordinary dispatch (`sub_141081FB0`) explicitly rejects CREATE `0xA` and DESTROY `0xE`.

Do not confuse invalidation with destruction: `0x14106DED0` sets state `0x4000` and queues pending
layout; `0x14106DC50` invalidates geometry/layout. Redraw queues content through `sub_141075A80`.
None is a substitute for the recursive destructor.

# Imported callable surface

The imported resolver set contained stable semantic identities for:

```text
GetFrame
GetChildFrame
FrameCreateChild
DestroyFrame
SetFrameVisible
FrameGetSize
GetScreenSize
FrameSetPosSize
SendMsgEx
GetCreationParam
MessageDispatch
TextDecoded
KeyRegister
Dispatcher
PostMessage
```

These names are normalized documentation labels, not claims about original ArenaNet symbol names.

Do not add callable contracts to `Gw2.Contracts` until each target has a current-image identity, recovered
Windows x64 ABI, pointer/lifetime rules, and a valid game/UI thread boundary.

# Relationship to the widget table

The imported `GetChildFrame` path independently follows the same broad model already recovered for the
generic widget framework:

```text
frame/widget id
    -> bounds check
    -> global id-to-object table
    -> frame object
    -> child/frame operation
```

This corroborates the direct-index object-table model. It does not prove that every FrApi operation uses
the exact same table or object subtype.

# Native UI renderer relevance

Keep frame semantics and rendering submission separate:

```text
frame semantics/lifecycle
    FrApi / FrFrame

render submission
    FrameContentParams / GrModel / material path
```

The long-term native UI backend can use the frame surface where useful without exposing it to modules.

# Next validation steps

- derive and validate stable locators for the recovered lifecycle functions;
- recover the minimal callback/base-procedure contract, creation parameter access, and reentrancy rules;
- recover layout, focus, and resource-retention semantics;
- live-validate isolated create/show/hide/redraw/destroy behavior at the proven UI-thread boundary
  before exposing native frame construction to client code;
- recover text creation/measurement below full widget construction if possible.
