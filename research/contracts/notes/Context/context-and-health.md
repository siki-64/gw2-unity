# ChCliContext and health state

**Confirmed build(s):** `205.780` for the allocation/layout paths described here; reflected and
InfoBar evidence is scoped in the linked subsystem notes.  
**Status:** build-local reconstruction with cross-subsystem relationships confirmed on the stated build.  
**Unresolved:** the complete network writer/bridge for every health value and ownership of unrelated
larger-offset health-like state.

`ChCliContext` is the confirmed persistent character/player registry used by character-side systems,
including network-driven health updates.

It is a separate subsystem from the InfoBar creation pipeline.

This document defines:

- how `ChCliContext` is reached;
- how characters are resolved by id;
- where authoritative character-side health lives;
- the reflected `AgentOrderHealth*` display-health model;
- how these differ from the draw-time `CombatTracker CtRecord` consumed by `AsHealth`.

InfoBar architecture is documented in
[`../InfoBars/pipeline.md`](../InfoBars/pipeline.md). Healthbar rendering and CombatTracker consumption
are documented in
[`../InfoBars/healthbar-rendering.md`](../InfoBars/healthbar-rendering.md).

Symbols are maintained in the active Ghidra project; see [`methodology/ghidra.md`](../../methodology/ghidra.md). Reconstructed
layouts live under `Gw2.Contracts/Context/` and `Gw2.Contracts/Character/`.

# Overview

The confirmed character-side lookup chain is:

```text
TLS
    ↓
ContextCollection
    ↓ pointer lookup
ChCliContext
    ├─ Characters (+0x60) → ChCliCharacter
    └─ Players (+0x80) → ChCliPlayer → ChCliCharacter / name
        ↓
    ChCliHealth
```

Separately, the client also has a reflected animated display-health model:

```text
AgentOrderHealth*
```

and the InfoBar healthbar renders from:

```text
CombatTracker CtRecord
```

These three health-related representations must remain distinct.

# Upstream native access root

`ChCliContext` is reached through `ContextCollection` payload slot `0x13` at `+0x98`:

```text
TLS
    ↓
ContextCollection
    ↓ slot 0x13 / +0x98
ChCliContext*
```

`ContextCollection` is an ordered payload registry/lifetime coordinator, not merely a passive pointer
table. Registered factories construct root payloads in ascending slot order, paired destructors tear them
down in descending order, and the active lifecycle collection is published through TLS while callbacks
run.

The complete `0x318` layout, factory/destructor tables, construction/destruction order, replacement
semantics, and known pointer destinations are documented in
[`context-collection.md`](context-collection.md).

This document starts after that subsystem-root lookup and focuses on `ChCliContext`, its nested
character/player registries, and health-related state.

# `ChCliContext`

`ChCliContext` is a separately allocated world-state payload object. The native factory allocates exactly `0x530` bytes and reaches `ChCliContext::Ctor`; the matching deleting
destructor frees the same `0x530` bytes. Build-specific symbols and addresses belong in the active Ghidra project.

The constructor shows that the character list, player list, and two attitude
caches use the same allocator-backed native-array header shape
(`allocator id/generation + data pointer + capacity/count + growth`).

Its two primary sparse registries are:

| offset | role |
|---|---|
| `+0x58` | character-list allocator metadata |
| `+0x60` | `ChCliCharacter**` character list |
| `+0x68` / `+0x6C` | character-list capacity / count |
| `+0x70` | character-list growth field (constructor value `0x20`) |
| `+0x78` | player-list allocator metadata |
| `+0x80` | `ChCliPlayer**` player-wrapper list |
| `+0x88` / `+0x8C` | player-list capacity / count |
| `+0x90` | player-list growth field (constructor value `0x20`) |
| `+0x98` | local `ChCliCharacter*` |

The vtable `+0x110` lookup is a **player-list-index lookup**, not a generic agent-id
resolver. It bounds-checks `PlayerListIndex` against `PlayerCount +0x8C` and returns
`Players[PlayerListIndex]` from `+0x80`. Each `ChCliPlayer` retains the same registry index at
`+0x74`, links to its character at `+0x18`, and exposes a UTF-16 name pointer at `+0x68`.

The separate `+0x60` character list is the list to iterate when extracting all character objects;
the player list supplies the character-to-name association.

## World-entry payload → player registry

The recovered `ChCliPvp` world-entry/update handlers resolve their target player through this
registry. The packet payload does **not** index `ContextCollection`: that table has already supplied
the `ChCliContext*`. The message then carries its own player-list index.

```text
ContextCollection +0x98
    ↓
ChCliContext*
    ↓
payload +0x02 : PlayerListIndex
    ↓
vtable +0x110 GetPlayerByListIndex
    ↓ bounds check against PlayerCount +0x8C
Players[PlayerListIndex] at +0x80
    ↓
ChCliPlayer*
```

Player creation grows the `Players` registry as needed, constructs the full `ChCliPlayer` with the
same index, stores that index at `ChCliPlayer +0x74`, and publishes the object in the matching
`Players[index]` slot. The PvP equipment/rank message family `0x200..0x207` uses the payload field
at `+0x02` through this path before mutating the resolved player's state.

The character registry at `+0x60` is separate. Character-list indices, player-list indices, and
`Agent.agentId` are distinct recovered domains unless a specific native path proves a bridge.

## Definition lookup arrays

The constructor-zeroed middle of `ChCliContext` also contains two source-named definition caches.
These are unrelated to the world-entry `Players` lookup:

| offset | native source name | entries | population key |
|---:|---|---:|---|
| `+0x310` | `m_professionLookup` | 11 pointers | `professionDef->GetType()` |
| `+0x368` | `m_raceLookup` | 5 pointers | `raceDef->GetRace()` |

Native assertions bound the compact definition index against `arrsize(...)`. The profession cache is
populated from content request family `0x13B`; the race cache from family `0x158`.

`ChCliPlayer` is a large native object: build `205.780` allocates `0xA178` bytes for each instance.
The player-list entry is therefore a pointer to the same full object that owns the recovered inline
character/progress/specialization state, not a separate compact name wrapper. Its canonical character
link is `+0x18`; the adjacent `+0x20` link currently aliases it in live captures and is retained as
an unresolved field in the overlay. The PvP loadout object used by spectator gear inspection is a
separate `ChCliCharacterContext` embedded at `ChCliPlayer +0x4F18`.

It does **not** establish `ChCliContext` as a universal registry for every entity/agent type in the
client.

## Agent attitude cache

Build-`205.780` disassembly identifies `ChCliContext_GetAttitude` at `g_ChCliContextVtbl+0x50`. The
function accepts an agent, indexes it by `agentId` at agent `+0x0C`, and first checks an explicit
presence bitset before reading the cached `Attitude` byte:

| `ChCliContext` offset | role |
|---|---|
| `+0x4F0` | presence-bitset allocator metadata |
| `+0x4F8` | presence-bitset word pointer |
| `+0x500` / `+0x504` | presence-bitset capacity / word count |
| `+0x508` | presence-bitset growth field (constructor value `0x40`) |
| `+0x510` | attitude-byte-array allocator metadata |
| `+0x518` | agent-id-indexed `Attitude` byte pointer |
| `+0x520` / `+0x524` | attitude-byte capacity / count |
| `+0x528` | attitude-byte-array growth field (constructor value `0x100`) |

On a cache miss, the function dispatches on agent type. Type `0` resolves and validates the Character
wrapper and calls its virtual `+0x1A8`. Types `0x0A` and `0x0B` use their respective wrapper paths and
ultimately call a virtual `+0x28`. Other types default to `Attitude.Neutral` (`3`). The result byte is
then stored at the agent-id index and its presence bit is set. This is a cached relationship value, not
the InfoBar name category itself; the Character name-category classifier consumes it as one input.

# Character → combatant path

The confirmed health-update path resolves a character and then obtains another sub-object through:

```text
vtable_call(character + 8, +0xB0)
```

This resolves the object used by the health setter path.

Do not assign a stronger semantic class name to this sub-object unless independently recovered.

# `ChCliCharacter.m_health`

`ChCliCharacter.m_health` is a pointer at:

```text
ChCliCharacter + 0x3E8
```

It points to a separate `ChCliHealth` object.

This is assert-confirmed from `ChCliCharacter` destruction logic.

Therefore health is not an inline float or inline health struct embedded directly at `+0x3E8`.

# `ChCliHealth`

`ChCliCharacter.m_health` points to a separately allocated native
`ChCliHealth` object. The attached executable gives a hard ownership/size boundary: `ChCliCharacter::~ChCliCharacter` destroys
`ChCliHealth` and frees exactly `0x88` bytes.

The constructor establishes this physical layout:

| offset | role |
|---:|---|
| `+0x00` | `ChCliHealth` vtable |
| `+0x08` | first 0x1C-byte value helper |
| `+0x24` | second 0x1C-byte value helper |
| `+0x40` | independently stored float/getter value |
| `+0x48` | 0x30-byte notification list |
| `+0x78` | owning `ChCliCharacter*` |
| `+0x80` | unresolved 8-byte tail |
| `+0x88` | native end |

The previously useful low-offset observations remain inside this real object:
`+0x0C`, `+0x10`, `+0x14`, `+0x28`, and `+0x40` are all accessed as
float state by native health helpers.

## Object boundary

`ChCliHealth` ends at `+0x88`. Health-like fields at larger offsets belong to a different object/state
path and remain unassigned until that owner is independently recovered.

The pointer relationship is:

```text
ContextCollection
    -> ChCliContext
    -> ChCliCharacter
    -> ChCliCharacter +0x3E8
    -> ChCliHealth* [0x88]
```

The exact network writer/bridge for every health value remains unresolved.

# Display-health model: `AgentOrderHealth*`

A separate server→client representation describes animated **display health**.

This family is recovered from the binary's reflection metadata.

The reflection block contains:

```text
g_AgentOrderReflectionStringPool
g_AgentOrderReflectionDescriptorTable
```

Descriptor entries contain type information and field-name pointers. The reflected field names below
are read directly from that metadata.

# `AgentOrderHealthCurr`

Confirmed fields:

```text
order
healthCurr
healthShieldCurr
healthShieldDecayElapsedTime
healthShieldDecayStartValue
healthMax
healthShieldMax
```

This describes a display-oriented health snapshot containing:

- current health;
- current shield/barrier value;
- shield-decay animation state;
- max health;
- max shield.

# `AgentOrderHealthMax`

Confirmed fields:

```text
order
valueCurr
valueRate
```

# `AgentOrderHealthRate`

Confirmed fields:

```text
order
isLooping
```

# Display-model implications

The reflected field set strongly indicates an animated presentation model rather than a raw static
health fraction.

The evidence includes:

- current values;
- a rate/interpolation field;
- looping/state behavior;
- shield/barrier decay elapsed time;
- shield/barrier decay start value;
- separate health and shield maxima.

This is consistent with client-side animated presentation such as interpolated values and
shield/barrier decay.

Do not treat `AgentOrderHealth*` as the same object as `ChCliHealth`.

# CombatTracker draw-time state

`AsHealth` does not draw directly from `ChCliHealth`.

Its draw path reads per-agent display values from `g_CombatTracker`.

The relevant record is:

```text
CombatTracker CtRecord
```

indexed by:

```text
agentId = [agent+0x0C]
```

Its canonical layout is [`CtRecord.cs`](../../Gw2.Contracts/InfoBars/CtRecord.cs). Field evidence and
rendering semantics belong in
[`../InfoBars/healthbar-rendering.md`](../InfoBars/healthbar-rendering.md).

# The three health layers

The current subsystem separation is:

```text
ChCliHealth
    authoritative/server-updated character-side health

AgentOrderHealth*
    reflected animated display-health model/messages

CombatTracker CtRecord
    draw-time display values consumed by AsHealth
```

These are related but not interchangeable.

## `ChCliHealth`

Role:

```text
authoritative character-side health state
```

Evidence:

- reached through `ChCliContext`;
- updated from network packet deserializers;
- uses buffered pending/current values;
- emits change notification only on changes.

## `AgentOrderHealth*`

Role:

```text
animated display-health description/messages
```

Evidence:

- reflection metadata;
- current/rate/looping fields;
- shield/barrier decay fields;
- separate health/shield maxima.

## `CombatTracker CtRecord`

Role:

```text
draw-time values consumed by AsHealth
```

Evidence:

- indexed by the healthbar's agentId;
- read every draw by `AsHealth`;
- contains the displayed health fraction, lag trail, barrier-related fraction, and another display
  value.

# Unresolved bridge

The exact bridge:

```text
AgentOrderHealth*
    ↓
    ?
    ↓
CombatTracker CtRecord
```

has not been recovered.

Do not document a direct writer, shared memory overlay, or one-to-one struct conversion without further
evidence.

Likewise, the exact relationship between `ChCliHealth` updates and the `AgentOrderHealth*` display model
is not yet fully reconstructed.

# Identifier domains

The recovered PvP/world-entry path uses `PlayerListIndex`, while CombatTracker and attitude caches
use agent ids in their own domains:

```text
payload.PlayerListIndex
    ↓
ChCliContext.Players[index]
    ↓
ChCliPlayer
    ↓ optional Character link
ChCliCharacter
    ↓
Agent*
    ↓
Agent.agentId
```

Do not treat `PlayerListIndex`, the separate character-list index, and `agentId` as interchangeable.
A join between them must be established by the specific entity/path being used.

# Relationship to the InfoBar pipeline

`ChCliContext` does not directly feed InfoBar creation.

The InfoBar creation path is driven by a transient per-frame tag-`0x13` agent/event queue:

```text
per-frame tag-0x13 queue
    ↓
AgentShouldHaveInfoBar
    ↓
TryCreateInfoBarForAgent
```

`ChCliContext` instead provides persistent character lookup for character-side systems.

The two systems may be correlated by id for supported entity types, but they have different ownership,
lifetime, and responsibilities.

# Known boundaries — do not conflate

- `ContextCollection` pointer lookup == native entry point for exposed downstream subsystem state
- `ContextCollection` pointer lookup != owner/populator of downstream subsystem state
- `ContextCollection` != universal root of all client state
- `ChCliContext` != universal entity registry
- `ChCliContext` != InfoBar creation source
- TLS cache != separate per-thread `ContextCollection`
- `ChCliCharacter.m_health` != inline health value
- `ChCliHealth` != `AgentOrderHealth*`
- `ChCliHealth` != `CombatTracker CtRecord`
- `AgentOrderHealth*` != `CombatTracker CtRecord`
- `CombatTracker CtRecord` != authoritative character-side health storage
- `CtRecord.alpha` != established render visibility alpha
- `PlayerListIndex`, character-list index, and `agentId` are distinct domains unless a specific native path proves a join
- reflected field names != proof of the exact writer/consumer bridge

# Open leads

Useful unresolved areas:

- exact semantic meaning of the three trivial `ChCliHealth` getter values;
- exact relationship between `ChCliHealth_Set` and `AgentOrderHealth*` generation/application;
- exact apply handlers for `AgentOrderHealthCurr`, `AgentOrderHealthMax`, and `AgentOrderHealthRate`;
- exact writer path into `CombatTracker CtRecord`;
- exact bridge from `AgentOrderHealth*` to `CombatTracker`;
- explicit bridges between player-list index, character-list index, and agent-id domains where needed;
- lifetime/invalidation behavior for `ChCliCharacter`, `ChCliHealth`, and related cached references.

Until those are resolved, integrations should preserve the subsystem boundaries above rather than
constructing a single synthetic "health object" that hides native distinctions.
