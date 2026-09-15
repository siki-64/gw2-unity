# Champion novelty in-world receive path — CmbtCliMsg/CmbtCliBuff — build 207.032

> **IN-WORLD RECEIVE PATH ONLY.** This note describes decoded combat Buff state after messages have been received by the client. Receive registration ids below are diagnostic/static metadata; they are not a novelty activation or send protocol.

## Resolved Buff apply — receive id 0x02DF

`CmbtCliMsg.cpp` RVA `0x12C1A10` resolves the target combatant and a numeric `SkillId`, then performs the normal content lookup:

```text
message +0x0A -> SkillId

ContextCollection.CnContext
    virtual +0x70(content class 0x40, SkillId)
    -> SkillDefinition*

assert SkillDefinition.PayloadKind == Buff
assert SkillDefinition.Payload != null
```

This independently confirms `SkillDefinition +0x28` as `SkillId` and content class `0x40` as the skill-definition class used by this receive path.

Recovered apply record:

```text
CmbtCliBuffApplyMessage  // 0x18 bytes
+0x00 uint16 opaque receive header
+0x02 uint32 TargetCombatantId
+0x06 uint32 SourceAgentId
+0x0A uint32 SkillId
+0x0E uint32 BuffId
+0x12 uint32 Duration
+0x16 uint8  Unknown16
+0x17 uint8  IsActive
```

The handler begins parsing at `+0x02`; `+0x00` therefore remains an opaque receive header rather than being equated to the registration id.

### BuffId proof

RVA `0x12C0530` inserts the new object into the per-character Buff table using the value supplied from message `+0x0E`. Attempting to insert an existing key reaches the ArenaNet assertion:

```text
!m_buffs.Find(buffId)
```

The constructor copies the same value into `CmbtCliBuff +0x18`, proving that field as `BuffId`.

## Resolved CmbtCliBuff

The apply handler calls RVA `0x12C0530`, which resolves the optional source-agent id, allocates `0x78` bytes, and invokes the `CmbtCliBuff.cpp` constructor at RVA `0x12BEE70`.

```text
CmbtCliBuff  // 0x78 bytes
+0x00 vtable
+0x08 uint32 SkillId
+0x10 SkillDefinition* SkillDefinition
+0x18 uint32 BuffId
+0x20 owner pointer
+0x28 source-agent pointer / ownership head
+0x40 uint32 Duration
+0x44 uint32 ActiveStartTick
+0x48 uint32 Unknown48
+0x4C uint32 IsActive
```

Constructor evidence:

- `+0x08` is copied from `SkillDefinition +0x28`.
- `+0x10` stores the resolved `SkillDefinition*`.
- `+0x18` stores the `BuffId` used by the owner's Buff table.
- the resolved source object is installed at `+0x28` with native ownership/ref bookkeeping in the following fields.
- `+0x40` is initialized from apply-message `Duration`.
- `+0x44` is initialized from the client clock.
- `+0x48` receives apply-message byte `+0x16`, widened to a dword; its semantic name is still unresolved.
- `+0x4C` receives apply-message `IsActive`, widened to a dword.

### IsActive proof

RVA `0x12C0470` checks `CmbtCliBuff +0x4C` before activation. If it is already nonzero the code reaches the ArenaNet assertion:

```text
!buffData->isActive
```

It then writes `1` to `+0x4C`. RVA `0x12C0A10` performs the inverse transition and writes `0` before emitting a distinct character callback.

### Duration / ActiveStartTick proof

RVA `0x12C0470` receives a remaining-duration value for an existing Buff and computes:

```text
elapsed = max(Duration - RemainingDuration, 0)
now = client clock
ActiveStartTick = max(now - elapsed, 0)
IsActive = 1
```

That arithmetic establishes `+0x40` as total `Duration` and `+0x44` as the activation/start tick in the same client clock domain. The exact clock unit is not named here until the clock source at RVA `0x9CDDA0` is independently characterized.

## Direct bridge into BuffEffectEvent

After the resolved Buff is inserted, RVA `0x12C0530` notifies the character through virtual `+0x128` using:

```text
rdx = CmbtCliBuff + 0x10
```

The downstream event consumed by the character/AvCharEffect path is therefore an exact view into the resolved Buff object, not a separate allocation:

```text
BuffEffectEvent = CmbtCliBuff + 0x10

BuffEffectEvent
+0x00 SkillDefinition*   // CmbtCliBuff +0x10
+0x08 uint32 BuffId      // CmbtCliBuff +0x18
+0x10 owner pointer      // CmbtCliBuff +0x20
+0x18 source Agent*      // CmbtCliBuff +0x28
```

This closes the receive-to-effect bridge:

```text
CmbtCliMsg receive
    -> SkillId
    -> CnContext numeric SkillDefinition lookup
    -> CmbtCliBuff allocation/state
    -> CmbtCliBuff +0x10 BuffEffectEvent view
    -> character Buff notification
    -> AvCharEffect_InstallEvent
    -> SkillDefinition +0x40 authored applications
    -> AvCharEffect_Dispatch
    -> EfCliContext
```

## Existing-Buff lifecycle receive family

The adjacent registration entries form a coherent Buff lifecycle family.

### 0x02DE — activate/reconcile existing Buff

Handler RVA `0x12C19A0` consumes:

```text
+0x02 TargetCombatantId
+0x06 BuffId
+0x0A RemainingDuration
```

It finds the existing Buff by `BuffId` and calls RVA `0x12C0470`. That function requires the Buff to be inactive, reconstructs `ActiveStartTick`, sets `IsActive = 1`, and notifies the character through virtual `+0x120` with `buff + 0x10`.

### 0x02DF — apply/create Buff

Handler RVA `0x12C1A10` resolves `SkillDefinition`, constructs the `0x78`-byte Buff, inserts it by `BuffId`, and emits the initial character notification.

### 0x02E0 — duration update + deactivate

Handler RVA `0x12C1B20` consumes `{TargetCombatantId, BuffId, Duration}` and calls RVA `0x12C0A10`. That function finds the existing Buff, requires it to be active, replaces `Duration`, clears `IsActive`, and notifies the character through virtual `+0x130` with `buff + 0x10`.

### 0x02E1 — duration update

Handler RVA `0x12C1B90` consumes the same `{TargetCombatantId, BuffId, Duration}` shape and calls RVA `0x12C0A90`. It replaces `Duration` without forcibly changing `IsActive`, then conditionally notifies through virtual `+0x138` depending on the Buff-definition predicate at RVA `0x12C1910`.

The client therefore maintains Buff identity, authored Skill identity, duration and active/inactive lifecycle as distinct concepts.

## Neighboring registrations still under analysis

The next handlers remain adjacent in `CmbtCliMsg.cpp`:

```text
0x02E2 -> RVA 0x12C1C00
0x02E3 -> RVA 0x12C1C90
```

They resolve the same target-combatant context but feed different collection routines (`0x12C0B20` / `0x12C0CC0`). Their exact receive semantics are deliberately not assigned until the associated collection/key structures are decoded.

## Relation to novelty activation

This entire path is downstream of normal toy-slot activation. The normal activation request remains only:

```text
uint16 0x00F0
uint8  toySlot
```

No `SkillId`, `BuffId`, `SkillDefinition*`, world `IEffectDef*`, target pointer, or effect-manager pointer is present in that request. These identifiers appear later on the receive-side combat Buff path after server resolution/authorization.
