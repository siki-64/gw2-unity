# Item client context (`ItCliContext`)

**Confirmed build(s):** unknown; the source build was not recorded.<br>
**Status:** provisional durable layout and accessor reconstruction with unknown build provenance.<br>
**Unresolved:** source build, cross-build validity, and the opaque fields identified below.

The dump contains a recoverable `Game/Item/Cli/ItCliContext.cpp` region. The object reached from
`ContextCollection +0x178` is allocated with `0xA0` bytes by `sub_1413c4e50` and initialized by
`sub_1413c4a60`. The native layout is represented conservatively in
[`ItCliContext.cs`](../../../src/Gw2.Native/Context/ItCliContext.cs).

## Context access and lifetime

`sub_1413c5450` is the direct accessor:

```text
contexts = GetContextCollection()
return contexts->ItCliContext       // ContextCollection +0x178
```

The constructor installs the primary vtable, initializes the item array and item-definition cache,
and registers an `.updateskinkillswitches` listener. The destructor destroys the array entries,
releases the item-definition cache and notifier, and unregisters that listener. The framework-owned
array/notifier representations are not yet identified, so only their starting offsets are modeled.

## Recovered object fields

| offset | observed role | evidence |
|---|---|---|
| `+0x00` | primary vtable | `sub_1413c4a60` |
| `+0x30` | item-pointer array | indexed by item id in `sub_1413c4ec0`, `sub_1413c5180` |
| `+0x38` | array storage/allocator state | reset with the array during destruction |
| `+0x3C` | item-array count | bounds checks and resize path |
| `+0x48` | opaque framework object | initialized by `sub_14101ed60` |
| `+0x50..+0x7F` | opaque notifier | passed to notifier dispatch and destroyed as one object |
| `+0x80` | duplicate-report item id | compared and assigned by `sub_1413c4ec0` |
| `+0x84` | duplicate-report timestamp/state | assigned with `sub_1409cdda0()` |
| `+0x88` | item-definition map bucket/capacity state | passed to dictionary hash/resize helpers |
| `+0x8C` | item-definition map entry count | tested and incremented by `sub_1413c5230` |
| `+0x90` | item-definition map entries | entries are 12 bytes each: key, value, hash |
| `+0x98..+0x9C` | opaque cache/config state | initialized by the constructor; semantics unresolved |

`sub_1413c5180(context, itemId)` is the confirmed direct lookup. It rejects an out-of-range id and
returns `context->ItemArray[itemId]`; it does not establish that a missing slot is non-null.

`sub_1413c4ec0(context, item)` removes an item by its `GetId()` value. It verifies that the indexed
entry is the same object, notifies listeners, clears the slot, and releases the item through its
secondary interface. Duplicate ids are logged and rate/state-limited using `+0x80/+0x84`.

## Item creation path

`sub_1413c59b0` is the central item factory. It first obtains the item definition, checks whether an
existing item with the same id must be reported, grows the pointer array to `itemId + 1`, then
dispatches on `itemDef +0x2C`. The observed item-kind allocations are:

| item-definition kind | allocation size | constructor helper |
|---:|---:|---|
| `0` | `0x108` | `sub_1413c6a70` |
| `1` | `0xA8` | `sub_1413c6970` |
| `2` | `0xF8` | `sub_1413c75c0` |
| `3` | `0x98` | `sub_1413c7db0` |
| `4` | `0xA8` | `sub_1413c7e90` |
| `5` | `0xB0` | `sub_1413c8d90` |
| `6..8` | `0xA8` | `sub_1413c6970` |
| `9` | `0x108` | `sub_1413c8f70` |
| `10` | `0xB0` | `sub_1413c9560` |
| `11` | `0x98` | `sub_1413c9770` |
| `12` | `0xA8` | `sub_1413c6970` |
| `15` | `0xA8` | `sub_1413c9870` |
| `16..17` | `0x98` | `sub_1413c9770` |
| `18` | `0xD0` | `sub_1413c97a0` |
| `19` | `0xA8` | `sub_1413c98b0` |
| `20` | `0x98` | `sub_1413c9770` |
| `21` | `0xD0` | `sub_1413c9990` |
| `22` | `0xA8` | `sub_1413c6970` |
| `23` | `0xE0` | `sub_1413c9a30` |
| `24` | `0x100` | `sub_1413c9b70` |

The factory stores the returned item interface at `ItemArray[itemId]`. The agent-backed path is
separate: `sub_1413c54a0` allocates a `0x58`-byte `ItCliAgent`, constructs it from a keyframed agent,
and returns the item interface at allocation base `+0x08`.

## Base item observations

`sub_1413c2720` is the common `ItCliItem` constructor. The following offsets are stable within the
base item subobject used by the context:

| offset | observed role |
|---|---|
| `+0x00` | primary vtable |
| `+0x38` | item id |
| `+0x40` | `ItemDefinition*` |
| `+0x48` | packed location type (low nibble) and location slot (`>> 4`) |
| `+0x50` | item flags/state word used by item predicates |
| `+0x58` | location-specific owner (`inventory`, `lootable`, or `vendor`) |
| `+0x60` | item notifier/list storage |

The location accessors assert the expected `Content::ITEM_LOCATION_*` value before returning the
location-specific pointer and, for inventory-like locations, the packed slot. The base item is a
secondary interface inside the item allocation; this is why `ItCliAgent` returns `allocation + 8`
to the context rather than its allocation base.

## Remaining targets

The next useful decomp step is to identify the `ItCliItem` vtable slots around `+0x68..+0x310` and
the concrete item subtype constructors. That will allow the `ItemDefinition` fields used by the
factory (`+0x2C`, `+0x30`, `+0x38`, `+0x60`, `+0x74`) to be recovered without guessing from API data.
