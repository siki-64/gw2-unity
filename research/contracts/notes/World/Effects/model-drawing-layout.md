# Model effect drawing inputs and ownership

Static continuation, 2026-09-13. Analyzed Ghidra program: `/Gw2-64.exe`, base
`0x140000000`; the imported image's SHA-256 is recorded in protocol/catalog.json.
These addresses are coordinates in that imported image. No live effect was activated,
and this pass does not establish a complete Champion novelty replay contract.

## Relationship to Champion novelties

The known Chalice world definition enters the shared EfCli factory. Its authored
definition tree and application metadata are still needed to establish which runtime
families realize that specific visual. The following structures describe the shared
model family (applicationTargetMode 4/6/10); they are not a claim that the captured
Chalice root itself has one of those modes. Compound definitions may instantiate
children from other families. The external-content boundary is described by the client-novelty notes in
this directory rather than by a separate static-closure note.

## Created model, host, listener, and attachment

[`EfCliModelEffect.cs`](../../../Gw2.Contracts/Effect/EfCliModelEffect.cs) remains
0x210 bytes. Constructor `0x14134FD40`, setup `0x141352840`, model creation
`0x1413512C0`, and host cleanup `0x1413515B0` distinguish these fields:

| Offset | Field | Producer / ownership |
|---|---|---|
| `78` | placement listener vtable | embedded interface passed to placement source |
| `D0` | authored model binding | mode 4 payload+10, mode 6 payload+00, or first mode-10 payload+18 entry |
| `D8` | Mode6Payload68 | copied from mode-6 payload+68; semantics unresolved |
| `E0` | PlacementValue10 | setup descriptor+10 |
| `E8` | ModelScale | starts at 1; multiplies scale extracted from common transform |
| `F8` | ModelObject | result of model factory `0x140CF04F0` |
| `100` | ModelCreationPayload | mode-6 payload+78, passed to model factory |
| `108` | SetupParameter | fifth setup argument, semantic domain unresolved |
| `10C` | ModelStateFlags | separate from common runtime flags at +20 |
| `110` | SetupScale | sixth setup argument when positive; starts at 1 |
| `118` | PlacementValue08 | setup descriptor+08 |
| `120..14F` | placement matrix | existing row-major 3x4 overlay |
| `150` | PlacementMode | descriptor+00; values 0..4 accepted |
| `154` | ModelBindingId | model virtual+618 result; starts/reset to FFFFFFFF |
| `158` | PlacementValue18 | descriptor+18 in mode 3 |
| `160` | PlacementValue38 | descriptor+38 in mode 3 |
| `164..173` | PlacementValue3C/40/44/48 | four mode-3 values; constructor defaults -pi,+pi,-pi,+pi |
| `178` | HostObject | retained setup input; distinct from created model |
| `180` | PlacementSource | mode-0 input supplying listener notifications |
| `18C` | TimeScale | constructor float; model virtual+40 consumes it in compatibility branch |
| `190` | EffectId | constructor id, distinct from tracked slot at +30 |
| `198/1A0/1A8` | AttachmentObject/Next/Previous | intrusive tracked reference |

Factory call/store instructions `0x1413513FE/0x141351403` establish the created model
at F8. Setup `0x1413528A9..0x1413528B6` stores the host and invokes virtual+08.
Cleanup invokes host virtual+00 and clears +178. These paired operations establish
retention/release behavior without proving a specific C++ reference-count class.
Mode-0 setup passes effect+78 to source virtual+20; cleanup unregisters via virtual+28
and clears +180. Constructor links +198 into the attachment object's reference list;
that is a different protocol from host retention.

## Setup descriptor includes rotation

[`EfCliModelPlacement.cs`](../../../Gw2.Contracts/Effect/EfCliModelPlacement.cs)
is a partial descriptor overlay with an aligned covered span of 0x50. This is not a
proven native allocation size or sufficient initialization recipe.

| Source offset | Consumer |
|---|---|
| `00` uint32 | effect+150 placement mode |
| `08` qword | effect+118 |
| `10` qword | effect+E0 |
| `18` qword | mode 3: effect+158 |
| `20/24/28` floats | mode 1: effect+12C/13C/14C position |
| `2C/30/34` floats | mode 1: rotation matrix builder input |
| `38` uint32 | mode 3: effect+160 |
| `3C/40/44/48` floats | mode 3: effect+164/168/16C/170 |

At `0x141352A09` the instructions load `RDX=descriptor+2C`, then call `0x1409C92E0`
with `RCX=effect+120`. The decompiler incorrectly omits RDX from that helper's
signature. Helper instructions `0x1409C92F3..0x1409C9346` read three floats through
RDX and feed trigonometric functions to build a matrix. Do not replace this path with
an identity matrix. Rotation axis order remains unnamed.

Mode 0 registers the supplied listener source. Mode 1 builds rotation and then copies
position. Mode 2 obtains a context-derived position and writes NaNs when its provider
reports no valid position. Mode 3 copies the extra binding/range fields. Mode 4 performs
no mode-specific copy in setup. Every accepted branch then stores SetupParameter,
clears model-state bits 0x20/0x40000, and enters readiness/setup `0x141351B30`.
Descriptor-to-runtime copies are instruction-confirmed at
`0x141352855..0x14135285C`, `0x1413528B9..0x1413528CA`,
`0x14135290F..0x141352935`, and `0x141352A12..0x141352A27`.

## Scale bounds and rendering transform

`0x1413508B0` prepares the model-creation transform from the common effect matrix
at +34/+44/+54 and its section id +64, including native world-section conversion.
It is distinct from simply reading the model placement matrix at +120.

The routine extracts component scales, multiplies by model+E8 and mode-6 authored
factors, then clamps each component using `IEffectDef+40/+44`. Instructions
`0x1413509E8` and `0x141350A06` load the upper/lower bounds. Zero selects defaults;
the effective lower bound is at least 0.001 and upper bound at most 100. This supports
`MinimumModelScale` and `MaximumModelScale` in the definition overlay. It does not
establish that those fields have the same role in every non-model effect family.

`TimeScale` at +18C is separate: the `EffectsEnableTimescaleFix` compatibility branch
multiplies it by mode-6 payload+44 before model virtual+40. `SetupScale` at +110 is
also separate; its full downstream meaning is not assigned to the model+E8 multiplier.

## Validation and remaining work

Native layout tests cover descriptor copies, three separate ownership pointers,
attachment links, matrix translation offsets, and distinct scale/time fields.
They validate compiled offsets, not live effect drawing or lifetime behavior.

Remaining: exact host/model interface layouts, compound child ownership, authored
payload types, the remaining model-state bits, placement enum names/rotation order,
and the novelty-specific definition tree. No activation or deployment is part of this
struct-mapping change.
