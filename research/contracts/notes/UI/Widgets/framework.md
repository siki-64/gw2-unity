# Widget/component framework

**Confirmed build(s):** unknown; provenance was not recorded consistently.<br>
**Status:** provisional durable framework reconstruction with unknown build provenance.<br>
**Unresolved:** source build for individual observations and cross-build validity where not explicitly established.

Build 205780 frame-specific corrections are recorded in [FrApi](../Frames/frapi.md): the frame's
leading self-pointer is an intrusive pending-list link, not a vtable, and its callback registry is
at `+0x220`. Do not transfer the older `+0x200` component layout below to `FrFrame`. Its state
`+0x29C` bit `0x200` means Hidden; this does not establish semantics for other widget object types.

InfoBars are built on a broader data-driven UI widget/component framework rather than a nameplate-specific
object model.

This document owns the confirmed generic mechanics used by the InfoBar subsystem:

- the global widget id→object table;
- per-object component callback registration;
- component creation and message dispatch;
- child widget/value-object relationships;
- generic widget property plumbing;
- the shared settings object;
- the runtime discovery model used to correlate live objects.

InfoBar creation/update behavior is documented in
[`../../InfoBars/pipeline.md`](../../InfoBars/pipeline.md).
Healthbar-specific rendering is documented in
[`../../InfoBars/healthbar-rendering.md`](../../InfoBars/healthbar-rendering.md).

Symbols are defined in
the active Ghidra project; see [`re/methodology/ghidra.md`](../../../methodology/ghidra.md).
Widget flags and stable masks are canonical in
[`WidgetStateFlag.cs`](../../../../src/Gw2.Native/UI/Widgets/WidgetStateFlag.cs).

# Global widget table

The framework maintains a global id→object table currently symbolized as:

```text
g_InfoBarTable
```

Despite the current symbol name, the table contains generic widget/frame objects used by the broader UI
framework, not only parent InfoBars.

The table layout is:

| offset | field |
|---|---|
| `+0x08` | widget-object pointer array |
| `+0x14` | bounds/count |

Lookup reduces to a plain bounds-checked array access:

```text
WidgetArrayIndex(id):
    if id >= count:
        return null
    return array[id]
```

No hidden id transform has been observed.

Known access chain:

```text
GetOrCreateComponent
    ↓
ResolveWidgetById
    ↓
WidgetArrayIndex
```

Live enumeration confirmed that populated entries dereference to valid widget/frame objects.

The first field of observed objects is a self-pointer, consistent with this UI object's native layout
convention.

# Per-object component registry

Each widget object contains a component/callback list beginning at:

```text
widget + 0x200
```

with count at:

```text
widget + 0x248
```

Each entry is 24 bytes:

```text
struct ComponentEntry
{
    fn      @ +0x00
    data    @ +0x08
    flags   @ +0x10
}
```

The final field is an `int32` status/dispatch field.

`ComponentDispatch` scans the list and invokes registered function pointers for entries matching the
required status condition.

This is a generic callback/message mechanism. It is not a hardcoded switch over InfoBar sub-widget
types.

# Component creation

`GetOrCreateComponent` is the generic entry point used to create or fetch a component by tag.

Observed tags in the InfoBar subsystem include:

```text
0x4E       InfoBar widget type/tag ('N')
0x2430     child widget tag
0x10630    child widget tag
0x30       name-related child widget tag
```

`AsHealth` is also maintained through the same framework, using component slot 2 and config tag
`0x10030` or `0x10830`.

The creation path resolves the owning widget through the global id table and then operates on its
component registry.

# Message dispatch

Registered component callbacks are widget-behavior message entry points.

Confirmed examples include:

```text
Tag2430_OnMessage
sub_55D270
AsHealthMsgThunk
Tag30_Callback
sub_558340
```

`AsHealthMsgThunk` forwards to the healthbar-specific message dispatch documented in
[`../../InfoBars/healthbar-rendering.md`](../../InfoBars/healthbar-rendering.md).

Other widget callbacks use the same general pattern: handle selected message ids locally and fall back
to shared lifecycle behavior.

`WidgetLifecycleDispatch` is a generic 75-case lifecycle/message dispatcher used across the framework.

Do not treat it as healthbar-specific.

# InfoBar callback fingerprint

Live enumeration of active InfoBar-related objects found the following callback set together:

```text
Tag2430_OnMessage
sub_55D270
AsHealthMsgThunk
Tag30_Callback
sub_558340
```

This is useful as a runtime fingerprint for identifying the InfoBar widget family.

The fingerprint does not imply that every callback belongs to the same native class.

# Child widgets are separate objects

The Stage-3 InfoBar child tags:

```text
0x2430
0x10630
0x30
```

resolve to separate objects in the global widget table.

They are **not** additional inline entries embedded directly in the parent InfoBar object.

Each observed child object has its own component list.

For the relevant single-component children:

```text
component entry @ child + 0x200
entry.data      @ child + 0x208
```

The `data` field points to a separate heap object used by the widget behavior.

# Value/display objects

The component `data` pointer for these child widgets dereferences to a small heap object whose first
field is an in-module vtable pointer.

Three related vtables have been observed in this object family:

| symbol | observed use |
|---|---|
| `g_ValueVtbl_2430` | value object behind `Tag2430_OnMessage` objects |
| `g_ValueVtbl_55D270` | value object behind `sub_55D270` objects |
| `g_ValueVtbl_shared` | observed behind `Tag30_Callback` and `AsHealthMsgThunk` objects |

These vtables cross-reference within the same construction path.

`BuildValueDescriptor`, reached from the generic lifecycle dispatcher, writes a three-vtable bundle into
fresh value/display objects.

Confirmed leading layout:

| offset | field |
|---|---|
| `+0x00` | primary vtable pointer |
| `+0x08` | related vtable/reference |
| `+0x10` | related vtable/reference |
| `+0x18..+0x38` | per-instance data |

Some qwords in the per-instance region decode as a low dword plus a high float.

Observed values include:

- one low dword matching a creation/order-like id;
- floats whose values are compatible with screen-coordinate ranges.

Those observations do **not** establish canonical field semantics. Keep these members provisional until
their role is confirmed.

# Generic widget property plumbing

The framework exposes generic property setters used by the InfoBar Stage-3 path.

Known helpers include:

```text
Widget_SetIntProp
Widget_SetFloatProp
```

`Widget_SetIntProp` resolves the target widget by id and writes through framework-managed property
storage.

`Widget_SetFloatProp`:

- resolves the widget by id;
- clamps the incoming value to `[0, 1.0]`;
- stores the value only when it changes;
- triggers the framework notification path.

Known InfoBar uses include:

```text
tag 0x2430
    receives a presentation float sourced from InfoBar.PresentationC8.Current (+0xCC)

tag 0x10630
    receives a float sourced from InfoBar.field_E0 (+0xE0)

tag 0x30
    receives a generic property used by the name-related child
```

`AsHealth` is an important exception:

```text
AsHealth
    does not receive its displayed health through this generic float-property path
```

Its draw-time values come from `g_CombatTracker`.

# Global settings object

The framework uses a static settings object:

```text
g_Settings
```

`GetSettings` is a trivial accessor returning its address.

The settings flags dword is at:

```text
g_Settings + 0x18
```

Known accessors include:

| bit | accessor | confirmed use |
|---|---|---|
| `0` | `Settings_GetFlagBit0` | participates in healthbar visibility routing |
| `7` | `Settings_GetBit7` | gates creation/maintenance of tag `0x2430` |
| `8` | `Settings_GetBit8` | accessor confirmed; semantic role unresolved here |
| `9` | `Settings_GetBit9` | accessor confirmed; semantic role unresolved here |
| `10` | `Settings_GetBit10` | flag read combined with an override check |
| `12` | `Settings_GetBit12` | flag read combined with the same override pattern |
| `13` | `Settings_GetBit13` | accessor confirmed; semantic role unresolved here |

Do not infer subsystem semantics for bits whose role has not been independently recovered.

# Relationship to InfoBars

The framework and InfoBar subsystem have different responsibilities:

```text
generic widget framework
    id table
    component registry
    message dispatch
    generic properties
    lifecycle

InfoBar subsystem
    agent eligibility
    InfoBar creation
    per-agent update
    child-widget maintenance
    name/health presentation state
```

`AsInfoBarFloatingManager` does not own all widget logic through its class vtable.

InfoBar behavior is distributed across:

- free functions;
- component callbacks;
- generic framework helpers;
- widget-specific message dispatchers.

# Known boundaries — do not conflate

- global widget table != persistent all-agent registry
- parent InfoBar != child sub-widget object
- component entry != child widget object
- component entry `data` pointer != widget object
- component callback registry != manager vtable
- `WidgetLifecycleDispatch` != healthbar-specific logic
- generic widget float property != `AsHealth` health data source
- `g_Settings` bit accessor != confirmed semantic setting unless independently established
- observed value-object float ranges != confirmed field meanings
- runtime heap pointer != durable symbol identity
- current symbol name `g_InfoBarTable` != proof the table is InfoBar-exclusive

# Runtime discovery

Runtime enumeration is useful when correlating framework objects with visible gameplay/UI state.

The stable structural process is:

```text
g_InfoBarTable
    ↓
live widget array
    ↓
widget object
    ↓
component entries
    ↓
callback identity
    ↓
component data/value object
```

A typical session workflow is:

1. resolve the current widget table from its symbol/locator;
2. read the live array pointer and bounds;
3. enumerate non-null widget objects;
4. inspect component counts and registered callback pointers;
5. identify known widget families by callback symbols;
6. dereference component `data` pointers only after the containing object has been validated;
7. correlate provisional fields with controlled in-game state changes.

Exact live heap addresses and population counts are session-specific and should not be treated as
durable documentation.

Use the existing tooling described in
[`../../../methodology/tooling.md`](../../../methodology/tooling.md) rather than creating one-off readers when
the tool cache already supports the required operation.

# Open leads

Useful unresolved areas include:

- better semantic naming for the global widget table if its broader ownership can be proven;
- exact native class relationships among the observed value/display vtables;
- semantic meaning of the provisional value-object fields at `+0x18..+0x38`;
- complete message-id semantics for the generic 75-case lifecycle dispatcher;
- complete settings-bit semantics outside the confirmed InfoBar uses;
- destruction/recycling behavior for child widgets and their value/display objects;
- ownership and invalidation rules for component `data` pointers.
