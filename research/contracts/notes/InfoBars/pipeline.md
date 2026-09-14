# InfoBar pipeline

**Confirmed build(s):** `205.655` and `205.780`, scoped by the sections below.<br>
**Status:** build-local lifecycle and update architecture reconstructed from static and live evidence.<br>
**Unresolved:** Stage-3 leads and meanings explicitly marked unresolved below, plus cross-build validity.

GW2 internally calls overhead nameplates **InfoBars**. The owning manager is
`AsInfoBarFloatingManager`.

The [2026-09-13 native-layout audit](native-layout.md) records the exact analyzed image,
corrects the replacement-versus-tick distinction and smoother offsets, and inventories
the remaining struct gaps. Its instruction-backed corrections take precedence over
older function aliases below.

This document defines the confirmed agent → eligibility → creation → update architecture and the
boundaries between the InfoBar object, its sub-widgets, the transient agent input, and unrelated
persistent character state.

The widget framework is documented in [`framework.md`](../UI/Widgets/framework.md). Name rendering,
healthbar rendering, occlusion, and character-side health storage are documented separately in
[`name-rendering.md`](name-rendering.md),
[`healthbar-rendering.md`](healthbar-rendering.md),
[`occlusion.md`](occlusion.md), and
[`../Context/context-and-health.md`](../Context/context-and-health.md).

Symbols such as `AgentShouldHaveInfoBar`, `TryCreateInfoBarForAgent`, and `InfoBarUpdate` are defined in
the active Ghidra project; see [`re/methodology/ghidra.md`](../../methodology/ghidra.md). The authoritative native layout is
[`InfoBar.cs`](../../../src/Gw2.Native/InfoBars/InfoBar.cs); unknown members should retain offset-derived names
rather than speculative semantics.

# Overview

The confirmed high-level flow is:

```text
per-frame transient tag-0x13 agent/event queue
    ↓
AgentShouldHaveInfoBar(agent)
    ↓ eligibility true
TryCreateInfoBarForAgent(manager, agent)
    ↓
InfoBar stored / registered
    ↓
InfoBarUpdate(this, agent)
    ↓
sub-widget maintenance / rendering state
```

Two architectural facts are fundamental:

- There is **not** a persistent "all agents" registry directly feeding InfoBar creation. The creation
  path drains a transient per-frame queue containing tag-`0x13` items.
- `ChCliContext` is a separate persistent **character-by-id** registry. It is used by other systems,
  including network-driven character state, and must not be treated as the source collection for
  InfoBar creation.

Widget type registration uses tag `0x4E` (`'N'`).

# Replacement readiness

The current reconstruction is suitable for continued observation, not yet for suppressing the
native InfoBar renderer. A perfect first replacement must preserve all of the following as one
behavioral unit:

```text
agent eligibility and 5000-unit creation range
    -> frame/control allocation and recycling
    -> InfoBar identity/reference maintenance
    -> child message updates and visibility/fade policy
    -> world projection and native CtlText submission
    -> health/resource/selection auxiliary submission
    -> native render ordering and depth/obstruction behavior
```

Creation, the Stage-3 update skeleton, health geometry/color inputs, and the two native submission
families are now build-locally identified. Teardown/reuse invalidation, the complete name-child
mapping, and the final name depth/obstruction handoff remain open. Until those are correlated in
live captures, a replacement hook would risk duplicate bars, stale recycled frame ids, incorrect
fade/occlusion behavior, or names that only look right in screen space. No native InfoBar draw is
suppressed by the current work.

# Agent input

The InfoBar subsystem uses these confirmed fields from the incoming agent:

| offset | field |
|---|---|
| `+0x08` | agent type |
| `+0x0C` | agentId |
| `+0x10` | type-specific pointer |

The pointer received by a particular stage must be treated according to that stage's contract. Several
related subsystems retain their own references; they are not interchangeable merely because they may
refer to the same logical unit in common cases.

# Stage 1 — eligibility

`AgentShouldHaveInfoBar(agent)` is a free function. It dispatches on `[agent+0x08]`.

For builds `205.655` and `205.780`, every recovered direct creation caller prepares the eligibility call
with the Windows x64 register state `RCX = manager`, `RDX = agent`. The same manager/agent pair is restored
immediately before `TryCreateInfoBarForAgent` when eligibility returns nonzero. The recovered call-site
ABI is therefore confirmed; whether `AgentShouldHaveInfoBar` semantically consumes `RCX` is not yet
established, so its conceptual signature remains agent-centric here.

Known agent-type behavior:

| type | behavior |
|---|---|
| `0x00` | type-0 wrapper path |
| `0x0A` | richer wrapper; eligible when any of three virtual predicates is true |
| `0x0B` | never eligible |
| `0x0F` | item/gadget content-definition flag path |
| other | not known to be eligible |

## Type `0x00`

The wrapper resolution is:

```text
require agent.type == 0
ptr = [agent+0x10]
require ptr != null
wrapper = ptr - 0x48
require wrapper.flags_0x178 & 0x10
```

After resolution, eligibility is gated by two wrapper virtual predicates at:

- vtable `+0x3E8`
- vtable `+0x408`

Do not assign stronger semantics to those predicates than the reconstructed code supports.

## Type `0x0A`

The wrapper resolution is:

```text
require agent.type == 0x0A
ptr = [agent+0x10]
require ptr != null
wrapper = ptr - 0x30
```

Eligibility is the OR of three virtual calls:

- vtable `+0x258`
- vtable `+0x200`
- vtable `+0x210`

These predicates reduce to direct bit tests:

```text
(wrapper.flags_0x1F8 >> 9) & 1
(wrapper.flags_0x1F8 >> 5) & 1
(wrapper.flags_0x200 >> 13) & 1
```

The resolver itself is a general type-`0x0A` wrapper utility rather than an InfoBar-specific primitive.

## Type `0x0B`

Type `0x0B` is never eligible in `AgentShouldHaveInfoBar`.

## Type `0x0F`

The type-`0x0F` path resolves an item/gadget content definition from the agent id and tests a content
flag:

```text
itemDef = <singleton>.vtbl[+0x8]([agent+0x0C])
tmp = itemDef->vtbl[+0x18]()->vtbl[+0x8]()
eligible = (tmp->flags & 0x400) == 0
```

This is a content-definition flag path, not a live global-settings boolean.

# Stage 2 — creation

`TryCreateInfoBarForAgent(manager, agent)` runs after eligibility succeeds.

Confirmed behavior:

1. Validate `agentId` from `[agent+0x0C]` against the manager's allocated child/slot range.
2. Resolve an `avAgent` view with `ResolveAvAgent(agent)`.
3. Obtain the required world/reference positions through the resolved object.
4. Compute squared creation distance.
5. Reject creation if the squared distance exceeds `25,000,000`.
6. Allocate/register the InfoBar in the resolved slot using widget type/tag `0x4E` (`'N'`).

The maximum creation distance is therefore:

```text
sqrt(25,000,000) = 5000 units
```

This is a **creation-time range check**. It must not be conflated with the later name visibility fades,
brightness attenuation, model unload range, or world-depth occlusion.

## Runtime validation of the Stage-1 → Stage-2 boundary

Observer captures on build `205.655` validate the recovered control-flow relationship without modifying
native eligibility or creation behavior. Across paired reporting windows, the number of nonzero
`AgentShouldHaveInfoBar` results matched the number of `TryCreateInfoBarForAgent` entries exactly.
Observed examples include `97 -> 97`, `98 -> 98`, `93 -> 93`, `79 -> 79`, `86 -> 86`, `88 -> 88`,
`90 -> 90`, and `89 -> 89`. Rejected Stage-1 candidates did not enter Stage 2.

The runtime model is therefore:

```text
candidate reaches recovered Stage-1 call site
    ↓
AgentShouldHaveInfoBar(...)
    ├─ 0       -> stop on this path
    └─ nonzero -> exactly one observed TryCreateInfoBarForAgent(manager, agent) attempt
```

`TryCreateInfoBarForAgent` must be described as a **creation attempt boundary**, not as proof that a new
InfoBar was allocated. The same accepted agents can reach Stage 2 repeatedly, so native slot lookup,
existing-object handling, and the internal creation-range cull remain semantically significant.

Both type `0x00` and type `0x0A` candidates have been observed failing Stage 1. For type `0x0A`, stable
accepted and rejected candidates recur across successive batches, confirming that eligibility is filtering
state within the type rather than merely accepting or rejecting the type wholesale.

In the validated captures, Stage 1 and Stage 2 executed on one observed game thread and the observer
reported no dropped records or failed agent snapshots. This is runtime evidence for those captures, not a
thread-affinity contract; instrumentation and feature code must remain safe if execution moves to another
thread.

## Runtime manager lifetime

The manager argument is not process-lifetime stable. A public-area capture observed the manager pointer
change while the game process and observed calling thread remained the same; several agent objects
continued across that transition. A separate private-instance capture kept one manager pointer throughout.

Consequences:

- use the manager supplied by the current native call;
- do not cache an `AsInfoBarFloatingManager*` as process-global durable state;
- do not infer agent lifetime from manager lifetime, or vice versa;
- raw agent pointers remain transient even when the same object is observed repeatedly.

## Name-display settings are downstream of Stage 2

Controlled runtime captures were repeated with these in-game name-display states:

- normal/baseline name settings with all relevant names enabled;
- enemy names disabled;
- player names and enemy names disabled;
- only the local player's name enabled.

None of those setting changes materially altered the recovered Stage-1 eligibility flow or the Stage-2
creation-attempt flow. In every tested state, accepted Stage-1 counts continued to match Stage-2 entries,
and the same recurring type-`0x0A` accepted/rejected pattern remained visible.

The tested name-display settings therefore act **downstream of `AgentShouldHaveInfoBar` and
`TryCreateInfoBarForAgent`**. Their exact suppression point is not yet localized; Stage 3 and later
presentation/render state are the appropriate next search area. Do not encode these UI settings into a
Stage-1 or Stage-2 reimplementation without new evidence.

## Builds `205.655` and `205.780` recovery evidence

The active Ghidra analyses resolve the same Stage-2 identity and caller topology in both images.
Its prologue immediately preserves the Windows x64 arguments as:

```text
RCX = AsInfoBarFloatingManager*  -> RDI
RDX = agent*                     -> RBX
```

The function directly reads `agentId` from `[agent+0x0C]`. Its creation-distance limit is referenced
through the unique `25,000,000.0f` datum (little-endian bytes `20 BC BE 4B`). The unique decoded
RIP-relative `comiss` reference is followed by `ja`, confirming rejection when the squared distance is
greater than the limit.

On build `205.780`, `ResolveAvAgent` is a thin adapter around the shared wrapper table: it preserves the
incoming agent, obtains the table, and tail-calls a lookup that bounds-checks `[agent+0x0C]` before reading
the corresponding pointer slot. This static path does not consume `[agent+0x10]`; live entry evidence for
that field is recorded below.

Four direct callers of `TryCreateInfoBarForAgent` are recovered in the same manager code region.

All four use the same semantic sequence: call `AgentShouldHaveInfoBar`, test the integer result, skip
creation on false, then prepare the same manager/agent pair and call `TryCreateInfoBarForAgent`. This
makes the Stage-2 function entry the centralized post-eligibility creation boundary; patching any one
caller would miss the other creation paths. The internal 5000-unit cull remains downstream of that
boundary and should not be duplicated by an interception.

## Build `205.780` entry ABI and interception boundary

The complete build-`205.780` function range is present in the image's runtime-function metadata. The
entry begins with these complete instructions:

```text
TryCreateInfoBarForAgent +0x00  mov [rsp+0x08], rbx  ; 48 89 5C 24 08
TryCreateInfoBarForAgent +0x05  mov [rsp+0x10], rsi
TryCreateInfoBarForAgent +0x0A  mov [rsp+0x18], rdi
TryCreateInfoBarForAgent +0x0F  mov [rsp+0x20], r14
TryCreateInfoBarForAgent +0x14  push rbp
TryCreateInfoBarForAgent +0x15  mov rbp, rsp
TryCreateInfoBarForAgent +0x18  sub rsp, 0x70
TryCreateInfoBarForAgent +0x1C  mov rbx, rdx
TryCreateInfoBarForAgent +0x1F  mov rdi, rcx
```

The recovered native contract is:

- `RCX` supplies the current `AsInfoBarFloatingManager*`;
- `RDX` supplies the transient creation-time `agent*`;
- no incoming `R8`, `R9`, XMM, or stack argument is consumed by the recovered function body;
- `RBX`, `RSI`, `RDI`, `R14`, and `RBP` are preserved by the native prologue/epilogue;
- `EAX = 0` on the recovered rejection paths;
- after the tag-`0x4E` registration helper is invoked, the function forces `EAX = 1` before returning.

The four recovered callers do not consume `EAX`; each immediately continues its own loop, epilogue, or
control-flow branch. The status remains part of the recovered callee contract and an interception must
return the native result unchanged rather than relying on those callers continuing to ignore it.

### Build `205.780` live entry observation

A bounded software tracepoint at `TryCreateInfoBarForAgent` captured ten unfiltered register hits followed
by five field-aware hits. Both captures reported one breakpoint event per accepted hit and completed their
normal restore/detach path.

Across all fifteen observations:

- execution used one observed game thread;
- `RCX` held one manager pointer throughout the capture;
- `RDX` held the current agent and varied between observed calls.

The five field-aware samples were type-`0x0A` agents. Each had readable `[agent+0x08]`,
`[agent+0x0C]`, and `[agent+0x10]`; the observed agent ids were distinct and every type-specific link at
`+0x10` was nonzero. This confirms that those fields were readable at the creation entry for the sampled
type-`0x0A` agents. It does not establish the same state for every agent type, thread affinity, or pointer
lifetime beyond the current native call.

After each capture, a direct memory read confirmed that the entry again began with the original
`48 89 5C 24 08` instruction bytes.

The function entry is the preferred centralized observation boundary. Its first instruction is exactly
five bytes, has no relative branch or RIP-relative operand, and performs the first native nonvolatile
save into the caller-provided home area. A five-byte near jump may therefore steal exactly one complete
instruction when all of the following are validated at runtime:

- the locator resolves one `TryCreateInfoBarForAgent` identity;
- the entry bytes are exactly `48 89 5C 24 08`;
- the trampoline is reachable by the installed branch;
- the stolen instruction executes against the original entry `RSP` before returning to function
  offset `+0x05`.

An observation callback at this boundary must preserve the original `RCX`/`RDX` pair across the callback,
provide aligned Windows x64 shadow space for its own call, preserve nonvolatile registers, and contain all
failures before resuming native execution. It must not cache either native pointer beyond the observed
call. These are addon hook requirements derived from the native entry contract, not claims about a native
ArenaNet callback mechanism.

# Stage 3 — `InfoBarUpdate`

The legacy `InfoBarUpdate(this, agent)` alias at RVA `0x003AF840` describes the
unit-replacement path, with `RCX = InfoBar*`, `RDX = agent*`. In the audited image it
returns without work when the unit is unchanged. Child setup is a separate callee
at `0x1403AD510`; presentation ticking is at `0x1403AEBE0`, dispatched by message
`0x41`. See [native-layout.md](native-layout.md) for the exact-image evidence.

The caller passes `agent` fresh at update time. The InfoBar object itself must not be treated as the
authoritative owner of that agent pointer. Tag `0x4E` dispatch has been observed directly preparing
that pair and calling `InfoBarUpdate`, independently tying the widget tag to the Stage-3 callback path.

`InfoBarFinalize` is recovered at build-specific RVA `0x003AFB40`. Its durable AOB
`48 83 B9 A0 00 00 00 00 48 8B F9 74` begins at function offset `+0x0A`.

For type-0 agents, the update begins with the same wrapper-resolution family used by eligibility. A
wrapper predicate at vtable `+0x498` produces a gate that controls the remainder of sub-widget
maintenance.

The known shape is:

```text
if agent.type == 0:
    wrapper = ResolveType0Wrapper(agent)
    if wrapper != null:
        gate = wrapper.vtbl[+0x498](wrapper, <context arg>)

InfoBar.flags_0x10C.bit5 = gate ? set : clear
maintain AsHealth

if gate:
    return

maintain tag 0x2430 when its settings gate is enabled
maintain tag 0x10630
maintain tag 0x30
InfoBarFinalize(this)
```

The gate skips the non-health child setup below it. The health helper is called
before the early return and separately applies the predicate to its visibility.


## Build `205.655` presentation-policy recovery

The Stage-3 path contains a live UI policy object and a downstream presentation-policy evaluator.
`InfoBarPolicyObjectAccessor` returns the policy object; `InfoBarPolicyQuery` evaluates a numeric policy
id against three layered bitsets:

```text
A = object + 0x1E8
B = object + 0x1F3
C = object + 0x1FE

byteIndex = id >> 3
mask      = 1 << (id & 7)

if A[byteIndex] & mask: return true
if !(B[byteIndex] & mask): return false
return !(C[byteIndex] & mask)
```

Runtime `debug memory-diff` tests on build `205.655` mapped these ids directly to UI settings:

| policy id | setting |
|---:|---|
| `0x16` | Show All Player Names |
| `0x17` | Show All Enemy Names |
| `0x44` | Thick Party Healthbars |
| `0x45` | Thick Squad Healthbars |
| `0x46` | Always Show Party Healthbars |
| `0x47` | Always Show Squad Healthbars |
| `0x4E` | Show All Party Names |
| `0x4F` | Show All Squad Names |

The policy id `0x4E` and the InfoBar widget/component tag `0x4E` are separate numeric namespaces.
Policy `0x43` participates in the same healthbar decision family but remains semantically unnamed.

Two early Stage-3 helpers update the packed **byte-sized** `InfoBar.flags_0x10C` field:

```text
InfoBarType0Predicate      -> bit 3 (0x08)
InfoBarStage3StateHelper   -> bit 5 (0x20)
```

`InfoBarStage3StateHelper` calls `InfoBarPresentationStateBuilder`, which queries Thick Party/Squad
policies `0x44` and `0x45` and builds config tag `0x10030` or `0x10830`. The `+0x800` difference is the
thick-healthbar geometry/style bit, not a runtime occlusion flag.

### Native healthbar show-reason convergence

The later Stage-3 evaluator combines multiple independent reasons rather than one visibility boolean.
Confirmed build-`205.655` inputs include:

- `IsCurrentTargetIdentity(InfoBar.unit)`: runtime-proven current/normal-target identity;
- the `GetAsContext` call/personal-target slot (`vtbl +0x70`), compared directly with `InfoBar.unit`;
- the `GetAsContext` effective-target/mouseover comparison local (`vtbl +0x40` / `+0x88`);
- Always Show Party/Squad Healthbars (`0x46` / `0x47`), which converge into one forced-show local;
- the still-unnamed policy `0x43`, whose false result under its classification path also sets that
  forced-show local;
- a damaged/resource-deficit proximity reason described below;
- additional classification/reason locals that remain unnamed.

The current-target predicate was validated live: targeting the tracked agent toggled its result and the
stored Stage-3 local `0 -> 1`, and moving target away toggled both `1 -> 0` while other watched locals
remained stable.

The current-target and call/personal-target paths fast-path to a presentation target of `1.0`; they do
not execute the damaged-health `1800`-unit proximity test. This matches live behavior where a targeted
unit's nameplate/health presentation can persist as far as the underlying unit/model remains loaded,
rather than being constrained by the damaged-health range.

### Damaged/resource-deficit proximity reason

For the damage-side branch, Stage 3 first requires an integer classification of `1` or `2`. It then
queries two float-returning virtuals from a type-0 character-side object and keeps the reason only when:

```text
valueB > valueA
```

This comparison is confirmed to be the native damaged/resource-deficit show reason by its control-flow
position and live damage behavior, but the two virtuals are not yet promoted to `CurrentHealth` and
`MaxHealth` names.

If the deficit comparison succeeds, `InfoBarWorldDistance(InfoBar.unit, &distance)` computes a true
world-space Euclidean distance and the reason remains active only while:

```text
distance <= 1800.0
```

This `1800` presentation rule is separate from the `5000`-unit InfoBar creation-time cull.

At the final convergence, the forced-show local, effective-target/mouseover local, and the surviving
deficit-proximity reason feed the native presentation/fade calculation. Forced-show paths preserve the
engine's own distance fade, including the `3500 -> 3200` band, rather than simply assigning alpha `1`.

### Presentation interpolation

`SetPresentationTarget` writes the target float at `object +0x08`. Consequently:

```text
InfoBar +0xC8  = smoother base/time; current +0xCC; target +0xD0
InfoBar +0xDC  = smoother base/time; current +0xE0; target +0xE4
InfoBar +0xF0  = smoother base/time; current +0xF4; target +0xF8
```

The older two-record description omitted the C8 channel and mislabeled record bases
as current values. All three are 0x14-byte smoothing records; see
[native-layout.md](native-layout.md#presentation-smoothers).
The current-target fast path sets presentation targets to `1.0`. The Stage-3 settings-bit-0 route is a
separate global visibility-routing branch and must not be confused with an occlusion/depth bit; see
[`occlusion.md`](occlusion.md).

The implementation consequence remains: addon code should add or transform presentation semantics while
preserving the native OR-chain, interpolation, fading, projection, occlusion, scaling, and drawing.

## `AsHealth`

`AsHealth` is a distinct sub-widget class and is part of the normal Stage-3 InfoBar path.

Confirmed creation/registration facts:

- component slot: `2`
- config tag: `0x10030` or `0x10830`
- callback: `AsHealthMsgThunk`
- `AsHealthMsgThunk` consists of a single `jmp AsHealthOnMessage`

The two config-tag values belong to the widget's static creation/configuration state. They must not be
confused with the runtime occlusion bit carried in `AsHealthFrameView.stateFlags`.

`AsHealth` does **not** consume a generic InfoBar health float for rendering. Its draw path reads
per-agent display values from `g_CombatTracker`; see
[`healthbar-rendering.md`](healthbar-rendering.md).

## Other known Stage-3 widgets

Other known sub-widget tags maintained by the InfoBar update path include:

- `0x2430`
- `0x10630`
- `0x30` — name-related

Known property plumbing for these widgets remains distinct from `AsHealth`:

- tag `0x2430` receives a clamped float sourced from `InfoBar.field_CC` (`+0xCC`) when its settings-controlled
  path is active;
- tag `0x10630` receives state including `InfoBar.field_E0` (`+0xE0`);
- tag `0x30` is name-related.

These generic widget-property paths are not evidence that the healthbar reads its displayed health from
the corresponding InfoBar fields.

# Pointer boundaries

Several pointers/references participate in nameplate behavior. They must remain distinct in
documentation and implementation.

## `InfoBarUpdate(this, agent)`

This is the **update-time agent** supplied fresh by the caller.

It is the input used while refreshing the existing InfoBar and maintaining its sub-widgets.

## `AsHealthWidget.agent`

This is the **draw-time agent reference** used by the `AsHealth` healthbar path.

`AsHealthDraw` uses it to resolve type-specific state, fetch the world anchor, and index display-health
state through the agent id.

## `InfoBar.unit` (`+0xA0`)

This is a **presentation/visibility-side unit reference** used by InfoBar logic, including target/context
comparisons and healthbar visibility/occlusion policy.

Build-`205.780` disassembly of `sub_55D650` confirms that this is the value member of an intrusive
tracked-reference record, not an isolated raw-pointer slot:

```text
InfoBar +0xA0  value
InfoBar +0xA8  next link
InfoBar +0xB0  previous link
```

When the update-time input changes, `sub_55D650` unlinks the old record, clears all three members, stores
the new value, and registers the new record through the pointed-to object's virtual interface. The same
helper reads the distinct Character wrapper at `+0xB8` and maintains another resolved pointer at
`+0xC0`. `InfoBarPresentationStateBuilder` also consumes `+0xB8` as a Character wrapper. A build-
`205.780` live read independently confirmed this identity: the field pointed to an object whose primary
vtable was the known Character-wrapper vtable. The exact native role and lifetime contract of `+0xC0`
remain unresolved, so it must not be treated as another interchangeable agent/unit pointer.

The type of `+0xC0` is bounded more tightly than its semantics. For a newly attached type-0 unit,
`sub_55D650` calls the `+0xB8` Character's embedded-interface vtable `+0xE0`; the concrete target is
`sub_11D3C70`, which obtains the player-list lookup index through embedded vtable `+0x18` and passes it to
`ChCliContext::GetPlayerByListIndex`. Therefore `+0xC0` is either null or a player-wrapper lookup result. The
meaning of that secondary character relationship is still unresolved.

`InfoBarTeardown` calls `sub_55D650` with a null replacement and separately unlinks the Character
wrapper record. This establishes explicit teardown boundaries for both references; neither pointer is
safe to cache across InfoBar destruction.

The Character link is bidirectional. When a type-0 unit is attached, `sub_55D650` passes the embedded
InfoBar interface at `InfoBar+0x58` to `ChCliCharacter_AddTrackedClient` (Character vtable `+0x558`).
That function inserts the pointer into the Character's intrusive client list at `+0x1E8/+0x1F0` and
rejects duplicates. Replacement and teardown use `ChCliCharacter_RemoveTrackedClient` (vtable
`+0x560`). This is native lifecycle bookkeeping; it does not make either object the exclusive owner of
the other.

It must not be silently substituted for either the update-time agent or the health widget's draw-time
agent.

## `NameRenderCtx.agent`

This is the **name projection/render input**.

It gates the name's world-position/projection block and participates in native occlusion behavior. It
must not be assumed to equal `InfoBar.unit`.

Like `InfoBar.unit`, it is the value member of an intrusive tracked-reference record: the value is at
`+0xA8`, followed by links at `+0xB0/+0xB8`. `NameRenderCtx_SetAgent` maintains this record. A build-
`205.780` live read linked the personal-targeted unit's matching `NameRenderCtx` record into the same
unit-side reference graph as its InfoBar, but that relationship does not make the two value slots
interchangeable.

# Dispatch architecture

`AsInfoBarFloatingManager` has a normal class vtable, but that vtable is not a master table for the
InfoBar subsystem.

The implementation is split among three mechanisms:

- **class vtable** — lifecycle/base-class virtual hooks;
- **component callback registry** — widget update/message/render callbacks;
- **free functions** — major entry points such as eligibility and creation.

In particular:

- `AgentShouldHaveInfoBar` is a free function;
- `TryCreateInfoBarForAgent` is a free function;
- `InfoBarUpdate` is reached through widget/component callback machinery rather than being discovered as
  a manager-vtable method;
- `AsHealthMsgThunk` is a registered component callback that tail-jumps directly to
  `AsHealthOnMessage`.

Searching only the manager vtable will therefore miss major parts of the subsystem.

# RTTI and symbol recovery

ArenaNet class RTTI is effectively unavailable for this code.

The binary contains useful RTTI for third-party/runtime types such as Havok and some standard-library
instantiations, but ArenaNet gameplay/UI classes do not expose useful `/GR` metadata. Class recovery
therefore depends on stronger anchors such as:

- constructors that write a known vtable;
- embedded source/assert strings;
- component callback registrations;
- durable code/data signatures;
- direct call and xref relationships.

`AsInfoBarFloatingManager` is identified through constructor/vtable and embedded source-string evidence,
not C++ RTTI.

# Relationship to character and health state

The transient InfoBar creation pipeline and persistent character-health subsystem have different
ownership and lifetimes. Their tested agent-id join, the three distinct health layers, and the unresolved
bridge between them are documented once in
[`../Context/context-and-health.md`](../Context/context-and-health.md). Healthbar consumption of
`CombatTracker CtRecord` is documented in [`healthbar-rendering.md`](healthbar-rendering.md).

# Name material boundary

The name presentation vector continues beyond Stage 3 into the native material path. Its exact material,
shader inputs, effect records, and negative selector result are owned by
[`name-rendering.md`](name-rendering.md#exact-name-material-depth-shader-evidence); the remaining
occlusion question is tracked in [`occlusion.md`](occlusion.md#name-material-shader-constant-and-depth-peel-evidence).

# Native rendering boundary

The InfoBar update path owns widget creation and presentation state; final name and health drawing
eventually converges into shared native emitter/submission infrastructure.

Known render layers are:

| element | layer |
|---|---:|
| health / `AsHealth` | `0` |
| tag `0x2430` | `1` |
| name | `2` |
| tag `0x10630` | `3` |

These layer values are queue/order state. They are **not** the depth-test selector.

Detailed emitter reconstruction belongs in
[`healthbar-rendering.md`](healthbar-rendering.md), with name-side use cross-referenced from
[`name-rendering.md`](name-rendering.md).

# Known boundaries — do not conflate

- transient per-frame tag-`0x13` input queue != `ChCliContext`
- `ChCliContext` != universal all-entity registry
- InfoBar creation distance != name distance brightness dimming
- InfoBar creation distance != visibility alpha fade
- `InfoBarUpdate(..., agent)` != automatically `AsHealthWidget.agent`
- `AsHealthWidget.agent` != automatically `InfoBar.unit`
- `NameRenderCtx.agent` != automatically `InfoBar.unit`
- `AsHealthFrameView.stateFlags & 0x1000` != `AsHealthFrameView.configTag`
- `AsHealth` display health != `InfoBar.field_CC`
- render layer != depth-test selector
- manager vtable != complete InfoBar logic dispatch table

# Build `205.655` unresolved Stage-3 leads

- resolve the semantic names of the two float getters whose `valueB > valueA` comparison forms the
  damaged/resource-deficit condition;
- identify the remaining unnamed reason/classification locals at the final Stage-3 convergence;
- map still-unidentified policy ids `0x15`, `0x18`, `0x1A`, and `0x43`;
- recover a durable semantic locator for the full presentation-policy evaluator rather than relying on a
  build-specific region;
- preserve the native visibility OR-chain rather than bypassing its fallback logic.

# Lifecycle and open leads

Creation and per-frame update are substantially reconstructed. Destruction, recycling, and invalidation
remain less understood.

Useful open leads:

- InfoBar destruction path;
- child/sub-widget destruction ordering;
- slot reuse behavior;
- agentId reuse or generation behavior;
- cached-pointer invalidation rules;
- exact lifetime relationship among the update-time agent, `InfoBar.unit`,
  `AsHealthWidget.agent`, and `NameRenderCtx.agent`;
- exact bridge from `AgentOrderHealth*` display messages to `CombatTracker CtRecord`.

Until those paths are recovered, code that caches native pointers across lifecycle boundaries should
fail closed and avoid assuming persistence not established by the native contracts.
