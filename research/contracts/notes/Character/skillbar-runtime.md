# Skillbar runtime state

**Confirmed build(s):** unknown; evidence is from independent July 2026 native consumers plus the existing local character/skill reconstruction.<br>
**Status:** runtime slot table and registration links are structurally recovered; recharge containers and secondary skill-definition data remain partial.<br>
**Unresolved:** exact meanings of several slot-state fields, recharge container key/value types, and complete `SkillDefinition +0x60` layout.

## `ChCliSkillbar`

The character-owned skillbar allocation remains `0x298` bytes. Independently supported fields:

| Offset | Field |
|---:|---|
| `+0x74` | active/current skill-slot code |
| `+0x80` | `AsContext*` |
| `+0xB0` | using-skill state/code |
| `+0xF0` | owner `ChCliCharacter*` |
| `+0x100` | notifier/registration object |
| `+0x150` | runtime recharge/state object |
| `+0x158` | registered combatant |
| `+0x160` | registered `ChCliCoreStats*` |
| `+0x168` | registered `ChCliInventory*` |
| `+0x178` | registered transformation manager |
| `+0x180` | held instant-skill slot |
| `+0x1D0` | 23 runtime `SkillDefinition*` slot entries |

The slot domain contains 23 entries (`0..22`). `0x17` is the observed inactive/no-slot sentinel.

A separate native consumer directly performs `slot < 23 -> ChCliSkillbar + 0x1D0 + slot * 8 -> SkillDefinition* -> SkillDefinition + 0x28 content key`. This independently confirms that `+0x1D0` is the runtime skill-definition table rather than a UI-only cache.

## Recharge object

`ChCliSkillbar +0x150` points to the runtime recharge/state object already modeled as `ChCliSkillRecharge`. Observed consumers use `+0x30` float, `+0x38` dword, `+0x48` pointer/container, `+0x58` dword, and `+0x68` pointer/container. The `+0x48` and `+0x68` collections are distinct.

Previous probes associate one collection with skill-keyed recharge state and the other with slot-keyed state, but their concrete native container and entry layouts remain unresolved. Do not collapse this object into a single cooldown timer.

## `SkillDefinition`

The stable fields remain `+0x28` content/native key, `+0x38` flags, and `+0x60` secondary data pointer. Runtime ability-state logic directly tests masks `0x1000`, `0x80000`, `0x1000000`, and `0x20`. Only `0x1000` currently has a promoted semantic (`NoRange`); keep the others unnamed until their branches are isolated.

### Correction to the secondary data interpretation

`SkillDefinition +0x60` leads to a substantial secondary native object. Runtime consumers read pointer/container state from it, including a qword at `+0x90`. Therefore the earlier interpretation of `+0x90` as base recharge milliseconds is not supported and has been removed from `Gw2.Contracts`.

Observed secondary-object anchors include `+0x58`, `+0x60`, `+0x68`, `+0x70`, `+0x71`, `+0x90`, `+0xA0`, `+0xF0`, `+0x150`, `+0x158`, `+0x160`, `+0x168`, `+0x190`, and `+0x198`. These are search anchors, not yet a canonical layout.

## Next steps

- recover the constructor/allocation boundary of the `SkillDefinition +0x60` pointee;
- identify the native containers at recharge `+0x48/+0x68`;
- correlate `ActiveSkillSlot`, `HeldInstantSkillSlot`, and runtime slot messages;
- isolate native ammo count/recharge records;
- recover exact state bits behind range, ground-target, resource, queued/casting, and autoattack behavior.
