# ContextCollection native access root

**Confirmed build(s):** `205.780`  
**Status:** build-local reconstruction; promote individual facts to cross-build only after independent validation.  
**Unresolved:** unnamed payload slots and whether additional client subsystems exist outside this registry.

`ContextCollection` is the primary reconstructed native access root used by this repository. More
precisely, it is a **TLS-scoped, fixed-index registry and lifetime coordinator for independently
allocated world-state payload roots**.

It is not a monolithic game-state object. The collection coordinates the lifetime of registered root
payload objects, while those payloads own and update their subsystem-internal state.

## Physical contract

The native allocation is exactly `0x318` bytes:

```text
+0x000 .. +0x308   98 × 8-byte payload-pointer slots
+0x310             uint state/control field
+0x314             alignment/tail
+0x318             native end
```

The TLS accessor returns this table base directly. There is no second payload-pointer table behind it.

Conceptually:

```cpp
struct ContextCollection
{
    void* Payloads[98];
    uint32_t State;
};
```

The numeric slot is part of the recovered native contract:

```text
offset = slotIndex * 8
slotIndex = offset / 8
```

For example, `ChCliContext` is payload slot `0x13` (19):

```text
0x13 * 8 = 0x98
ContextCollection +0x98 -> ChCliContext*
```

## TLS publication

The current collection is reached through thread-local storage:

```text
tlsArray = gs:0x58
tlsBlock = tlsArray[g_tlsIndex]
ContextCollection* = *(tlsBlock + 0x10)
```

The canonical accessors are `ContextCollection::GetTls` and `ContextCollection::SetTls`; their
build-specific symbols and addresses belong in the active Ghidra project.

The TLS value is the current access root for native code in that scope, not a second representation of
the table.

## Static payload registration

Two parallel static arrays contain one factory and destructor callback per payload slot. Their canonical
symbol identities and build-specific addresses belong in the active Ghidra project, not in this behavioral note.

Subsystem registration stores paired callbacks by stable slot index:

```text
RegisterPayloadFactory(index, factory)
RegisterPayloadDestructor(index, destructor)
```

The ChCli subsystem registers slot `0x13`:

```text
RegisterPayloadFactory(0x13, ChCliContextFactoryThunk)
RegisterPayloadDestructor(0x13, ChCliContextDestructorThunk)
```

The factory reaches the exact `0x530`-byte `ChCliContext` allocation/constructor path; its paired
destructor thunk reaches the deleting-destructor path.

## Construction

`ContextCollection::Create` allocates exactly `0x318` bytes and invokes registered factories in ascending
slot order.

The collection under construction is temporarily published through TLS while the factories execute:

```text
old = GetTls()

collection = allocate(0x318)
SetTls(collection)

for index = 0 .. 97:
    factory = payloadFactory[index]

    if factory != null:
        collection[index] = factory()

SetTls(old)
return collection
```

This lets payload constructors use the normal `GetContextCollection()` path. A higher-index payload can
therefore resolve already-created lower-index payloads during construction without receiving the
collection explicitly as a constructor argument.

Construction order is consequently meaningful.

## Destruction

The matching `ContextCollection::Destroy` path performs the inverse lifetime operation.

It temporarily publishes the collection being destroyed through TLS, invokes registered payload
destructors in descending slot order, restores or clears the previous TLS value, and frees the
`0x318` table:

```text
old = GetTls()
SetTls(collection)

for index = 97 .. 0:
    payload = collection[index]
    destructor = payloadDestructor[index]

    if payload != null && destructor != null:
        destructor(payload)

if old == collection:
    old = null

SetTls(old)
free(collection, 0x318)
```

The ordering is dependency-friendly:

```text
construction:  low index -> high index
destruction:   high index -> low index
```

A payload destructor can still use the ordinary TLS accessor while lower-index dependencies remain alive.

This establishes that `ContextCollection` **coordinates the lifetime of registered root payloads**.
It does not establish that the collection itself performs the gameplay/network mutations inside those
payloads; those responsibilities remain subsystem-specific.

## Replacement and pointer stability

`ContextCollection::SetPayload` performs:

```text
GetContextCollection()[index] = payload
```

The durable identity is therefore the **slot**, not necessarily one immutable pointer value for the
entire collection lifetime:

```text
slot N = subsystem/payload identity
current slot value = current root pointer for that subsystem
```

Consumers should not infer permanent pointer identity merely because the slot number is stable.

## Known payload destinations

The exhaustive slot/offset layout is owned by
[`ContextCollection.cs`](../../Gw2.Contracts/Context/ContextCollection.cs). This note records only
relationships needed to explain behavior.

The runtime resolves the TLS-scoped `ContextCollection` anchor once during process initialization. It
validates the `ContextCollection.ChCliContext` field at `+0x98` and publishes only the root to
the shared runtime features. The startup resolver may retry while the game is constructing its world
payloads, but consumers do not rediscover the collection while reading state. The runtime never scans
payload slots and never chooses a relationship from a shallow runtime shape; if the `+0x98` contract is
not valid after startup resolution, context-backed features publish no data. All downstream access
walks from that anchor through Native intra-object offsets; no image-relative function or vtable
address is required.

Important recovered examples are `ChCliContext` at slot `0x13`, `GdCliContext` at `0x27`,
`ItCliContext` at `0x2F`, and `PvpCliContext` at `0x42`. The destination set is heterogeneous and
also includes non-`Context` roots; therefore **world-state payload** is the preferred generic term.
Unnamed entries remain valid slots and retain provisional names in the compiled layout until independently
identified.

## ChCliContext as a nested registry

`ContextCollection` resolves **which subsystem root** to use. A subsystem root can then expose its own,
independent index domains.

For the character side:

```text
TLS
 ↓
ContextCollection
 ↓ slot 0x13 / +0x98
ChCliContext
 ├─ Characters[character-list index] -> ChCliCharacter*
 ├─ Players[PlayerListIndex]         -> ChCliPlayer*
 ├─ LocalCharacter                   -> ChCliCharacter*
 ├─ m_professionLookup[type]         -> profession definition
 └─ m_raceLookup[race]               -> race definition
```

These nested indices are not `ContextCollection` indices.

For the recovered PvP/world-entry path:

```text
ContextCollection slot 0x13
    ↓
ChCliContext*
    ↓
payload +0x02 PlayerListIndex
    ↓
ChCliContext::GetPlayerByListIndex
    ↓
Players[PlayerListIndex]
    ↓
ChCliPlayer*
```

`PlayerListIndex`, the separate character-list index, and `Agent.agentId` are distinct identifier
domains unless a particular native path proves a bridge.

Detailed `ChCliContext`, character, and health behavior is documented in
[`context-and-health.md`](context-and-health.md).

## Architectural boundaries

Established:

- `ContextCollection` is a fixed 98-slot world-state payload registry;
- the TLS accessor returns the table itself;
- payload types register paired factory/destructor callbacks by stable numeric slot;
- creation constructs registered roots in ascending slot order;
- destruction tears them down in descending slot order;
- the lifecycle collection is published through TLS while callbacks execute;
- the collection therefore coordinates registered root-payload lifetime;
- individual slots can be replaced through `SetPayload`;
- known slots point directly to independently allocated subsystem roots such as `ChCliContext`.

Not established merely by membership:

- that every client subsystem appears in this table;
- that every payload follows the same internal object model;
- that `ContextCollection` populates or updates the gameplay state inside each payload;
- that a cached slot pointer survives replacement or collection teardown;
- that nested indices inside a payload share an identifier domain with another payload or with agent ids.

The useful short description is: **a TLS-scoped world-state payload registry with ordered lifetime
management**.
