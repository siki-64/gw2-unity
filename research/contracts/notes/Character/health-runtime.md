# Health, barrier, and breakbar runtime state

**Confirmed build(s):** unknown; evidence is from independent July 2026 native-consumer paths, cross-checked against the existing character-owned allocation boundaries.<br>
**Status:** health/barrier interpolation fields and character breakbar link recovered.<br>
**Unresolved:** notification semantics, several auxiliary health fields, and full breakbar object layout.

## Character links

`ChCliCharacter` exposes `+0xC8 -> CmbtCliBreakBar*`, `+0xD8 -> CharacterWaterState`, and `+0x3E8 -> ChCliHealth*`.

Observed water-state codes are `0 = Dry`, `1 = Diving`, and `2 = Surface`.

The candidate life-state field at `ChCliCharacter +0x188` is intentionally not promoted because it overlaps a region whose local object interpretation still needs reconciliation.

## Health interpolation

The native consumer computes a time-adjusted health value rather than simply displaying the raw `CurrentHealth` field:

`elapsed = max(now - HealthUpdateTimestamp, 0)`

`health = min(CurrentHealth + (elapsed / 1000.0) * HealthRegenRate, MaximumHealth)`

| Offset | Field |
|---:|---|
| `+0x08` | health update timestamp |
| `+0x0C` | current/base health at update |
| `+0x10` | maximum health |
| `+0x14` | health change/regen rate |

This explains why direct reads of `+0x0C` can lag the presentation value while regeneration is occurring.

## Barrier interpolation

Barrier uses the same pattern with an independent clock and rate:

`elapsed = max(now - BarrierUpdateTimestamp, 0)`

`barrier = min(Barrier + (elapsed / 1000.0) * BarrierChangeRate, MaximumBarrier)`

| Offset | Field |
|---:|---|
| `+0x24` | barrier update timestamp |
| `+0x28` | current/base barrier |
| `+0x2C` | maximum/interpolation cap |
| `+0x30` | barrier change rate |

These names are now promoted in `Gw2.Native.ChCliHealth` because the arithmetic is directly observed.

## Breakbar

`ChCliCharacter +0xC8` points to the character breakbar subsystem. The partial object has `+0x40 BreakbarState` and `+0x44` normalized value. For active states, a native consumer multiplies `+0x44` by `100.0` for presentation.

Existing `BreakbarState` values are compatible with the observed branches: `0 Ready`, `1 Recover`, `2 Immune`, `3 None`.

Keep the object partial until its ownership/allocation size and notification fields are recovered.

## Next steps

- identify the native functions that update the two timestamp/rate pairs;
- determine whether negative change rates are possible for health/barrier;
- recover breakbar maximum/current semantics and update notifications;
- resolve the candidate character life-state location without weakening the constructor-backed `ChCliCharacter` layout.
