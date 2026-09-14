# Monthly Champion novelty effects

**Confirmed build:** 205.780  
**Runtime state:** direct Chalice replay is disabled. The live-captured factory operands return an effect, but the client later crashes during native processing, proving that the direct factory call is not a complete activation contract.
**Important:** preview-panel effect data and in-world effect data are separate contracts. Never substitute one for the other.

## Status summary

Recovered:

- the 12 Champion novelty item pairs;
- the equipped toy-slot activation request (`0x00F0`, 3 bytes total);
- Champion's Laurels **preview-panel** root `IEffectDef` and content key;
- Champion's Chalice **in-world** root `IEffectDef` and content key from a controlled OFF-to-ON activation;
- the receive-side `SkillDefinition -> AvCharEffect -> EfCliContext` world creation chain;
- the distinction between the skill's authored `0x20`-byte effect-application records and `AvCharEffect`'s separate `0x20`-byte runtime-effect records;
- the four statically reachable factory modes at the decoded world callsite: `0x01`, `0x03`, `0x11`, and `0x13`;
- most of the `EfCliContext` call ABI used by this world path.
- a direct local Chalice probe using the captured world definition, corrected live-captured operands, and game-thread dispatch; it is enabled for controlled validation after the first operand arrangement crashed.

Still unresolved:

- exact enum/type names for `SkillEffectApplicationEntry +0x08` and `+0x1C`;
- the concrete native types represented by `SkillDefinition +0x58/+0x60`;
- exact ownership semantics of the three agent/view operands passed into `EfCliContext`;
- world root definitions for the other eleven Champion novelties;
- complete authored effect lifetime/removal/replacement semantics and map-transition behavior.

All Champion novelty activation remains unavailable. The corrected Chalice factory call progressed
beyond creation and then crashed during later native processing; the missing authored initialization
or ownership step must be captured before another deployment.

---

# 1. Preview-panel contract — separate by design

Code: `Gw2.Contracts/Combat/NoveltyPreviewEffectContract.cs`

A filtered **Laurels preview** capture recovered:

```text
PREVIEW gizmo:              Champion's Laurels
PREVIEW root IEffectDef:    0xB2D1 / 45777
content type:               0xB7
content key:                49 B7 88 FF 6B CF 00 40
                            8A 98 99 21 0E EB D3 36
child effects:              1347, 25201, 14854
```

The preview resolves the key through:

```text
ContextCollection.CnContext
    -> virtual +0x68
    -> IEffectDef*
```

This data is **preview-only**. The public gameplay/status effect id `46788` is not a client `IEffectDef` lookup key.

Known data is intentionally asymmetric:

```text
Laurels preview definition: known
Laurels world definition:   unknown

Chalice preview definition: unknown
Chalice world definition:   known
```

Preview and world both reaching `EfCliContext` only proves that they share low-level effect machinery. It does not make their definitions, callers, target state, or lifetime interchangeable.

---

# 2. Equipped activation request

Normal `SbToySlot` activation asks `ChCliWardrobe` virtual `+0x250` for the equipped toy and then calls virtual `+0x2B8` with the toy-slot index.

The complete outbound request is:

```text
uint16 opcode = 0x00F0
uint8  toySlot
```

It contains no item id, `IEffectDef`, content key, position, target, or effect-manager pointer. The server resolves/authorizes the equipped novelty; the visual is realized later through a separate receive-side character path.

---

# 3. Confirmed in-world Chalice sample

Code: `Gw2.Contracts/Combat/NoveltyWorldEffectContract.cs`

The controlled OFF-to-ON test on **2026-09-09 used Champion's Chalice** and reached the real `EfCliContext` creation implementation exactly once:

```text
WORLD gizmo:                Champion's Chalice
WORLD root IEffectDef:      0x5B49 / 23369
content type:               0xB7
content key:                29 6B D1 82 28 58 1A 48
                            98 B9 D1 72 FC 69 3D 78
```

Do not label `23369` as Laurels. No Laurels world root is known yet.

---

# 4. World definition selection happens before AvCharEffect

The receive-side dispatch implementation is:

```text
AvCharEffect dispatch RVA 0x1126680
```

The exact uploaded executable contains two direct calls to it:

```text
callsite RVA 0x1121FF8
callsite RVA 0x1133788
```

Both callers iterate a `SkillDefinition`-owned authored-effect array:

```text
SkillDefinition +0x40 -> SkillEffectApplicationEntry* array
SkillDefinition +0x48 -> uint32 entry count
entry stride          -> 0x20
```

Each accepted entry supplies its authored definition from:

```text
SkillEffectApplicationEntry +0x00 -> IEffectDef*
```

That pointer is passed to `AvCharEffect` in `r9`. Therefore the authored definition does **not** come from `AvCharEffect`'s runtime table.

The `SkillDefinition` identification is reinforced by client assertion/source strings in this chain, including `skillDef`, `buffData.skillDef`, and `AsBuff.cpp`.

## SkillDefinition fields used by the effect path

```text
+0x28 uint32 effectRuntimeSelector
+0x40 SkillEffectApplicationEntry* effectApplications
+0x48 uint32 effectApplicationCount
+0x58 uint32 optionalPayloadKind
+0x60 void*  optionalPayload
```

The pre-existing code field name `ContentKey` at `+0x28` is retained as an alias for compatibility, but this world path uses the dword as the selector for the per-character `AvCharEffect` runtime entry. It is not a 128-bit `IEffectDef` content key.

---

# 5. Authored effect-application entry

Recovered partial layout:

```text
struct SkillEffectApplicationEntry // size 0x20
{
    +0x00 IEffectDef* EffectDefinition;
    +0x08 uint32      TargetSelectionKind; // semantic name provisional
    +0x0C uint32      Unknown00C;
    +0x10 ...         opaque
    +0x18 uint32      ApplicationFlags;
    +0x1C uint32      ApplicationType;      // semantic name provisional
}
```

## `+0x08`: target-selection discriminator

`AvCharEffect` reads this as a **32-bit** value:

```text
0 -> no selected secondary operand
1 -> use the caller-supplied secondary candidate
2 -> resolve another object through AvCharEffect virtual +0x58
```

The exact enum name is unresolved, so `TargetSelectionKind` remains provisional.

## `+0x18`: application flags

This field is consumed as a bitfield after effect creation. The decoded path:

- masks the low three bits with `& 0x7`;
- separately tests mask `0x10C`;
- combines those results with `IEffectDef` flags before further runtime setup.

That is enough to call the field `ApplicationFlags`, but not enough to name the individual bits.

## `+0x1C`: application type/category

Both callers and `AvCharEffect` branch specially on:

```text
1
0x200000
0x400000
```

The exact enum/type name remains unresolved.

---

# 6. AvCharEffect owns a different 0x20-byte table

`AvCharEffect` has its own sorted runtime-effect array:

```text
AvCharEffect +0x80 -> runtime entry array
AvCharEffect +0x88 -> capacity/related bound
AvCharEffect +0x8C -> runtime entry count
entry stride       -> 0x20
```

The lookup selector comes from `SkillDefinition +0x28`.

Recovered partial layout:

```text
struct AvCharEffectEntry // size 0x20
{
    +0x00 uint32 selector;
    +0x04 uint32 runtimeHandle;
    +0x08 void*  EffectObject;
    +0x10 ... opaque ...
}
```

The central readability rule is:

```text
AUTHORED INPUT
SkillEffectApplicationEntry +0x00 = IEffectDef*

CREATED RUNTIME STATE
AvCharEffectEntry +0x08            = runtime effect object
```

These two arrays happen to use the same stride but have different owners and purposes.

---

# 7. SkillDefinition +0x58/+0x60 predicate

The previous notes called these fields an `AvCharEffectEvent` kind/object. That was incorrect. They belong to `SkillDefinition`.

The helper at RVA `0x12BCFA0` receives a pointer to the `SkillDefinition*` and computes a boolean used later in mode construction.

Common/base condition:

```text
SkillDefinition +0x2C <= 0x0A
```

Then behavior depends on the 32-bit tag at `+0x58`:

```text
tag 0:
    if payload != null:
        include payload +0x70 bit 30 in the predicate

tag 1:
    if payload != null:
        predicate becomes payload +0x04 bit 19

other tags / missing payload:
    fall back to the base condition as decoded
```

So the relevant tested masks are:

```text
payload +0x70 : bit 30 / 0x40000000
payload +0x04 : bit 19 / 0x00080000
```

This tagged payload also participates in an earlier lifetime branch. When tag `1` is present, the payload's `+0x04` bit `0x800`, a nonzero application target discriminator, and an existing runtime effect can combine to destroy the runtime effect and erase the matching `AvCharEffect` entry.

The exact native payload types are still unknown, so the code intentionally keeps neutral names.

---

# 8. AvCharEffect mode construction

The factory mode is built from two independent predicates.

First, a character-state virtual at `+0x350` chooses the base:

```text
virtual result == 0 -> 0x01
virtual result != 0 -> 0x03
```

Then the skill predicate described above controls whether bit `0x10` is added.

The complete statically reachable set at this callsite is therefore:

```text
0x01
0x03
0x11
0x13
```

The controlled Chalice activation used `0x11`.

`0x13` is a statically reachable alternate, not a mode established by that Chalice capture.

---

# 9. EfCliContext world creation ABI

The shared creation implementation is:

```text
EfCliContext create wrapper RVA 0x1344B70
EfCliContext create core    RVA 0x1344C50
```

At the decoded `AvCharEffect` site the virtual `+0x10` wrapper call is:

```text
EfCliContext.Create(
    context,
    IEffectDef* definition,
    uint32 effectId,
    uint32 mode,
    Agent/View* primaryAgent,
    Agent/View* selectedSecondary,
    Agent/View* auxiliaryAgent,
    float scale,
    uint32 trailingFlags)
```

The world call supplies:

```text
definition        = SkillEffectApplicationEntry.EffectDefinition
effectId          = 0xFFFFFFFF
mode              = 0x01 / 0x03 / 0x11 / 0x13
primaryAgent      = character virtual +0x140 result for captured Chalice call
selectedSecondary = null for captured Chalice call
auxiliaryAgent    = same character virtual +0x140 result for captured Chalice call
scale             = 1.0
trailingFlags     = 0
```

The wrapper derives one additional primary-agent classification flag and forwards
to the core creator through virtual `+0x08`. Calling the core RVA directly would
skip that normalization and is not equivalent to the captured world path.

The three agent/view operands remain neutrally named until their exact ownership semantics are proven across target modes.

## EfCliContext is not a thin allocator

Before creating a runtime effect, RVA `0x1344C50` performs substantial normalization and validation. Static decode shows that it can:

- reject/assert a null definition;
- replace the submitted definition through definition-dependent logic;
- ask an `EfCliContext` virtual for an alternate definition;
- inspect effect-definition flags and character/player state;
- select among multiple runtime implementation classes through a switch-like factory.

The binary contains the assertion text:

```text
No valid case for switch variable 'effectDef->applicationTargetMode'
```

That recovers the native field name. Operationally, `IEffectDef +0x28` is also the
`0..11` runtime subclass factory discriminator; it is distinct from the upstream
`SkillEffectApplicationEntry +0x08` target-selection field. The generic context and
lifecycle reconstruction is recorded in [`efcli-context.md`](efcli-context.md).

## Static feasibility of direct local Chalice creation

The captured Chalice arguments are sufficient to reach the generic wrapper without
manufacturing a `SkillDefinition` or `AvCharEffectEntry` first. Static analysis of
the wrapper/core pair establishes this narrower path:

```text
RVA 0x1345BD0 -> global EfCliContext*
EfCliContext virtual +0x10 -> RVA 0x1344B70 wrapper
    -> derive primary-agent classification flag
    -> EfCliContext virtual +0x08 -> RVA 0x1344C50 core
    -> validate/normalize IEffectDef and agent operands
    -> allocate the implementation selected by IEffectDef +0x28
    -> insert the returned object into EfCliContext's owned runtime list
    -> finalize/register the runtime object

EfCliContext virtual +0x30 -> RVA 0x1345AD0(effectObject, 1) removal request
```

The relevant `IEffectDef` fields consumed by the creator or its immediate
`AvCharEffect` guards are:

```text
+0x28 uint32  applicationTargetMode (native name; runtime factory discriminator)
+0x38 uint32  flags
+0x48 pointer alternate/replacement definition
+0x50 uint32  character-class discriminator (0, 1, or unrestricted 2)
```

The creator may replace the submitted definition through `+0x48`, apply additional
agent-specific replacement tables, reject definitions restricted to the local-player
operands, and select one of several runtime implementation classes from `+0x28`.
The direct route must therefore submit the captured world definition and the same
local character/view operand roles; substituting the preview definition remains invalid.

The available local lookup sequence is:

```text
ContextCollection.CnContext
    -> virtual +0x68(ContentKey*)
    -> Chalice WORLD key 29 6B D1 82 28 58 1A 48
                         98 B9 D1 72 FC 69 3D 78
    -> require returned definition +0x10 == content type 0xB7
```

The numeric definition id `0x5B49` is diagnostic identity only; the creator requires
the resolved `IEffectDef*`. A focused live capture at wrapper RVA `0x1344B70` recorded
the owning character's virtual `+0x140` result as both `primaryAgent` and
`auxiliaryAgent`, with `selectedSecondary = null`. The wrapper returned a non-null
runtime effect. This supersedes the earlier operand interpretation.

Direct creation deliberately bypasses the following authored `AvCharEffect` work:

- selector-indexed insertion in `AvCharEffect +0x80`;
- application-flag postprocessing;
- `0x200000` / `0x400000` paired-state binding;
- stowed/restored Buff state;
- replicated activation/deactivation ownership.

That bypass does not prevent `EfCliContext` from owning and ticking a successfully
created runtime object. It does mean the client must retain the returned object and
request removal through virtual `+0x30` before replacement, shutdown, or a
world/character transition. Losing that pointer can leave a persistent local effect
outside `AvCharEffect`'s normal removal bookkeeping.

Removal is deferred and context-owned: RVA `0x1345AD0` removes any auxiliary-agent
association, marks the runtime effect for teardown, and dispatches its virtual
teardown method. The `EfCliContext` tick later finalizes, unlinks, and frees the
object. Local code must not directly free the returned pointer.

The world content key is the first 16 bytes of Chalice's authored `IEffectDef`.
The current `CnContext` key resolver can still return null when the resident object
is outside its two active registries, so the probe first uses that resolver and then
performs a read-only background scan for the exact key. A candidate is accepted only
when its key, content type `0xB7`, root id `0x5B49`, and factory mode `0..11` all
match. The result is revalidated on the game thread before creation.

No explicit thread-id assertion exists in the decoded creator/removal routines, but
they mutate unsynchronized context-owned collections and all recovered world calls
occur in character/effect update flow. Both operations therefore belong on the
existing game-thread dispatcher, never an arbitrary GUI/render callback.

---

# 10. Preview vs in-world architecture

```text
PREVIEW PANEL
-------------
wardrobe / toybox preview state
    -> preview 128-bit content-key lookup
    -> PREVIEW IEffectDef
    -> preview Agent/model state
    -> shared EfCliContext machinery

IN WORLD
--------
server-authorized / replicated character state
    -> SkillDefinition
       +0x40 authored application array
       +0x48 count
    -> SkillEffectApplicationEntry +0x00 WORLD IEffectDef*
    -> AvCharEffect
       +0x80 runtime-effect table
    -> shared EfCliContext machinery
    -> created world runtime effect
```

Do not cross-populate the preview and world contracts.

---

# 11. Negative evidence

The old local replay implementation remains removed.

The character-owned helper at RVA `0x112E990` produced a Revenant dodge visual when called with the earlier guessed event value `0x10`; it is not a gizmo replay ABI.

Likewise, submitting the Laurels preview definition through generic effect paths did not reproduce the world gizmo. The deeper decode explains why: the real world path selects authored application state upstream, then applies additional target/mode/definition normalization.

Inbound `0x2CE` remains only a correlated secondary state message, and incidental `0x3FD` marker traffic remains ruled out as the visual creation path.

---

# 12. Runtime policy

The Companion selector remains visible, but activation is unavailable. The captured Chalice definition
and wrapper operands are retained for further instrumented investigation:

```text
Chalice -> captured EfCliContext call returns an effect -> later native processing crashes
all other novelties -> unavailable
toy/item activation callbacks -> null
```

No preview definition is replayed as a world effect. The probe retains the returned native effect
pointer and requests graceful removal before replacement; native context reset remains responsible
for teardown across a torn world transition.

---

# 13. Remaining RE work

1. Identify the concrete native types behind `SkillDefinition +0x58/+0x60` and name their relevant bits.
2. Recover a semantic enum for `SkillEffectApplicationEntry +0x08` target selection.
3. Recover a semantic enum/bit domain for `SkillEffectApplicationEntry +0x1C`.
4. Identify the upstream replicated state that installs/selects the Chalice-bearing `SkillDefinition` on activation and removes it on deactivation.
5. Decode Chalice destruction/removal ordering through the `AvCharEffect` runtime table.
6. Recover world definitions for additional Champion novelties independently from preview captures.
7. Establish replacement and map/character-transition behavior before implementing local activation.
