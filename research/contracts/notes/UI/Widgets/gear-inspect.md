# PvP gear inspection

**Confirmed build(s):** `205.780`.<br>
**Status:** read-only PvP provider, equipment, spectator graph, and entry-message reconstruction.<br>
**Unresolved:** whether selecting a previously unpopulated spectator target causes a targeted server push.<br>
**Address scope:** runtime access uses the discovered `ContextCollection` anchor and Native
intra-object offsets. Build-local code coordinates are not part of the access contract.

## Authoritative state boundaries

The inspector combines independent replicated state rather than reading one
monolithic spectator or world-entry payload:

- Physical weapon sets come from ordinary character/inventory world state at
  `ChCliPlayer -> ChCliCharacter -> ChCliInventory`.
- Selected specializations and major traits come from the per-player embedded
  specialization manager at `ChCliPlayer +0x9CB8`.
- PvP equipment comes from the separate server-driven `0x200..0x207`
  `ChCliPvp` provider-update family.

PvP equipment is not embedded in inbound world-transition message `0x100`.
That message starts the client world/load operation; after the client sends
`0x1A5` as its success acknowledgement, the server independently delivers the
per-player PvP provider records. The spectator Equipment and Specializations
tabs only consume already-populated player state and bind UI objects to it.

The gear inspector follows this same global path:

```text
ContextCollection anchor
  -> ChCliContext
  -> ChCliContext.Players[player-list index]
  -> ChCliPlayer.PvpGearManager
  -> PvpGearProvider
```

For the local player, `ChCliContext.LocalCharacter` leads to the selected
inline PvP loadout. `PvpCliContext` and the spectator selection are not used as
the source of remote equipment.

The recovered equipment message registrations for build `205.780` are:

| Message id | Recovered operation |
| ---: | --- |
| `0x200` | rune update |
| `0x201` | relic update |
| `0x202` | amulet update |
| `0x203` | selected Mist Champion/PvP Hero update |
| `0x204` | full rank and equipment update |
| `0x205` | destroy and remove the player's PvP gear provider |
| `0x206` | incremental PvP-rank update |
| `0x207` | combined PvP-rank update |

The registration descriptors themselves confirm these ids and runtime record
sizes: `0x203` is `0x0B` bytes, `0x204` is `0x27`, `0x205` is `0x06`, `0x206`
is `0x0A`, and `0x207` is `0x0F`. The `0x204` handler consumes the first
`0x23` bytes; its descriptor-proven trailing four bytes are represented as one
unknown storage field. The full update writes two PvP-rank definitions, the
provider's rune, relic, amulet, and four sigil entries; incremental updates can
arrive separately.
Selected specializations and major traits are populated in the same player
record through the embedded specialization manager at `+0x9CB8`.


The packet's `PlayerListIndex` at payload `+0x02` is resolved through the persistent
`ChCliContext` player registry before any of these updates are applied:

```text
ChCliContext*
  -> PlayerCount +0x8C bounds check
  -> Players +0x80 [PlayerListIndex]
  -> ChCliPlayer*
```

This is a lookup *inside* `ChCliContext`; it is not an index into `ContextCollection`.
The upstream `ContextCollection` relationship only supplies the `ChCliContext*`.
`ChCliPlayer +0x74` retains the same list index, and player creation writes the newly
constructed player into this exact `Players[index]` slot.

Build `205.780` live read evidence confirms this boundary: with the spectator
panel closed, repeated walks from the global `ContextCollection` anchor found
dozens of providers (31 in the latest pass; the preceding pass found 35) whose owner backlink matched
`ChCliPlayer +0x97B0`, and the specialization scan found 54 player owners. A
simultaneous spectator graph walk found `0` `BtGear` and `0` `BtEqpSlot`
objects. The panel therefore consumes replicated player and PvP-provider state;
it does not produce either one.

The shared access chain is:

```text
BtGear +0xD8
  -> ChCliPlayer* inspected player
  -> ChCliPlayer +0x4F18 = embedded ChCliCharacterContext
  -> ChCliPlayer +0x18 = target ChCliCharacter when present

BtEqpSlot +0x90
  -> shared PvpGearProvider
  -> ChCliPlayer +0x97B0
  -> provider +0x38 = owning ChCliPlayer
```

The retained `BtGear +0xD8` value is the inspected `ChCliPlayer*`; the embedded
context is reached by adding `0x4F18`. When present, the owning
`ChCliPlayer +0x18` supplies the inspected character link. The embedded context
supplies the nine loadout records and related PvP state. In the captured
remote-player state, the inline nine-loadout region was
not populated with the gear definitions. The active gear values were held by
the shared PvP provider consumed by all seven `BtEqpSlot` objects.

## Reconstructed provider construction and population

The static `ChCliPvp.cpp` recovery gives the following update chain on build
`205.780`; the runtime reader does not depend on these code locations:

```text
ChCliPlayer::EnsurePvpManager
  -> operator new(0xD8)
  -> PvpGearProvider::Ctor
       this = new object
       rdx  = owning ChCliPlayer
  -> ChCliPlayer +0x97B0 = PvpGearProvider*

ChCliPvp::ApplyPlayerGearUpdate
  -> resolves target ChCliPlayer from the update record
  -> calls EnsurePvpManager
  -> applies rune, amulet, relic, and sigil[0..3] setters
```

The provider allocation is `0xD8` bytes. Its recovered read-only storage is:

| Provider offset | Value |
| ---: | --- |
| `+0x38` | owning `ChCliPlayer*` |
| `+0x40` | rune definition pointer |
| `+0x48` | relic definition pointer |
| `+0x50` | amulet definition pointer |
| `+0x60` | selected Mist Champion/PvP Hero definition retained by message `0x203` |
| `+0x68` | PvP-rank definition resolved from combined field `+0x06` |
| `+0x70` | provider flags (bit 0 from combined `+0x0E`, bit 1 from message `0x203 +0x0A`) |
| `+0x78` | PvP-rank definition written by incremental message `0x206` |
| `+0x80..+0xAC` | twelve PvP rating values; not weapon ids |
| `+0xB0` | PvP-rank definition resolved from combined field `+0x0A` |
| `+0xB8..+0xD0` | four sigil definition pointers |

The rank classification is direct native evidence, not an id-range guess. The
provider virtual path immediately before these accessors obtains `pvpDef` and
asserts `pvpDef->GetPvpRankCount()`. Its adjacent thread-local accessor resolves
the definitions stored at `+0x78`, `+0x68`, and `+0xB0`. Live definitions exposed
ids such as `1`, `4`, `6`, `18`, `29`, and
`45`; two combined fields can differ, as in the observed pair `42`/`18`.
Neither field is a physical weapon item id.

The PvP Hero classification is likewise direct native evidence. The
`ChCliPvpHeroCollection` initialization path requests content family `0x71`,
matching the resolver request in message `0x203`. `TtPvpHero.cpp` consumes the
same definition layout and
reads its tooltip/name data from definition `+0x38`. Live request-`0x71`
objects expose their content id at `+0x14`; captured providers selected ids
`6`, `7`, and `9`, while other players retained a null selection.

The population caller reaches these setters directly: `SetRune`, `SetAmulet`,
`SetRelic`, and `SetSigil` with indices `0..3`. Each setter also dispatches the
provider notification path used by the bound equipment slots.

`BtEqpSlot::BindPvpProvider` only reads `ChCliPlayer +0x97B0` through
`ChCliPlayer` vtable slot `+0x330`, registers its listener, and rebuilds the
slot. It does not create or populate the manager. Consequently a remote
player can be inspected by reading this provider once the PvP update has
arrived, but an inspector should not call `EnsurePvpManager`: that would
allocate an empty manager and would not request the missing remote update.

## Remote-player population rule

In the captured remote-player objects, the inline PvP loadout records under
`ChCliPlayer +0x4F18` were unpopulated, including the alternate templates.
The active remote gear instead appeared in `ChCliPlayer +0x97B0` when the
decoded PvP update path had ensured and filled the provider. The same provider
is consumed by `BtEqpSlot::BindPvpProvider` when the spectator panel is shown;
spectator mode is not required for the provider population path.

Therefore, an empty captured remote `ChCliPlayer +0x4F18` loadout must not be
treated as proof that the player has no PvP gear. Without the decoded PvP
update, the manager pointer is null and there is no populated remote loadout
record to inspect directly. This is a static/runtime contract recovered for
build `205.780`, not yet a guaranteed cross-build contract.

## Slot discriminator

`BtEqpSlot +0x98` is now mapped as `PvpGearSlotType`:

| Value | Meaning |
| ---: | --- |
| `0` | amulet |
| `1` | relic |
| `2` | rune |
| `3` | sigil |

The live panel contained four type-`3` slots and one each of types `0`, `1`,
and `2`. Sigil slots additionally exposed `BtEqpSlot +0x9C` values `0..3`,
matching `PvpWeaponUpgradeIndex`; the other slot types used a non-index
sentinel.

The four sigil provider getter/storage offsets are now resolved. Entry into an
ordinary competitive PvP arena, with spectator mode disabled, produced the
same remote-player combined and incremental updates and populated the provider
through the same setters. This establishes that provider population is tied to
PvP player-update data, not to the spectator-mode flag. Later `0x204`/`0x203`
updates and `0x205` removals also arrived without a nearby client request.
Whether selecting a previously unpopulated spectator target independently
causes a targeted provider update has not been isolated; selection must not be
described as the population trigger without that evidence. The recovered read
path intentionally does not invoke any native mutation or allocation.

### World-entry request correlation

A build-`205.780` dual-boundary trace correlated the raw client-send entry
(`Msg::Raw`, RVA `0xFEA110`) with the inbound decoded-message-id store
(`DispatchStream`, RVA `0xFE91E4`) on one monotonic timeline. The world-entry
sequence sent `0x1A9`, `0x1A8`, `0x19A`, and `0x1A5`; the first entry-associated
`0x204` arrived about 691 ms after `0x1A5`. Static callers place these messages
in `MsCliMsg`/`MsCliPlayer`, and `0x1A5` is sent from an asynchronous
mission/load-completion callback. This timing does not establish it as a PvP
provider refresh request.

More importantly, the same 180-second capture observed 19 `0x204`, 19 `0x203`,
and 12 `0x205` records. Multiple later `0x204`/`0x203` pairs arrived without any
nearby outbound message; these correspond to ordinary server-driven player
population changes. The capture therefore found no per-player or general PvP
provider resend request. Replaying the mission-ready message would enter an
unproven world-state protocol transition and must not be treated as a safe
refresh operation.

The reusable correlator is:

```text
Gw2.Tools.exe --build 205.780 --pid <pid> gw2 pvp world-entry-correlation 180 100000
```

It logs low-volume outbound records with their raw payload and caller RVA, logs
only inbound `0x200..0x207` ids, and restores both temporary tracepoints on
normal completion.

For payload-level inspection, the tooling-only payload tracer reads the decoded
`HandlerInfo` and payload immediately before dispatch. It arms the ordinary and
ring-buffer-wrap decode paths together with the raw outbound send entry, so one
transition is sufficient and both directions share one monotonic timeline:

```text
Gw2.Tools.exe --build 205.780 --pid <pid> gw2 pvp message-payloads 60 4096
```

Each matching record includes the message id, dispatch type, handler and schema
RVAs, schema-declared size, up to 4096 payload bytes, and the decoder path that
handled it. The tracer suspends peer threads while single-stepping and restores
all temporary breakpoints on completion. Outbound records include their message
id, raw bytes, and caller RVA. The lower-level
`inbound-pvp-message` tracepoint mode remains available for isolated site
testing. This functionality is intentionally part of `Gw2.Tools`.

For build `205.780`, the four pre-dispatch sites are RVAs `0xFE9339`,
`0xFE9348`, `0xFE94F4`, and `0xFE9503`: two handler-call forms on each decode
buffer path. The decoded 32-bit message id is stored at `MsgConn +0x40` at
`0xFE91E4`, while the current `HandlerInfo` pointer remains at `MsgConn +0x48`.
Filtering must use the former; schema and handler metadata use the latter.
Earlier decode-stage candidates `0xFE92CF` and
`0xFE947C` execute for the same traffic but precede the dispatch calls and must
not be used for payload filtering.

This layout is live-confirmed on build `205.780`. One world-entry capture
processed 6,128 generic dispatches and decoded 15 `0x204`, 15 `0x203`, three
`0x205`, and one each of `0x200`, `0x201`, and `0x202`. All matching records
used the ordinary path in that run. The recovered handler/schema RVA pairs
were `0x1253C10/0x25C3990` (`0x200`), `0x1254500/0x25C3AB0` (`0x201`),
`0x1253CC0/0x25C3BA0` (`0x202`), `0x1253D70/0x25C3DE0` (`0x203`),
`0x1253E30/0x25C3FA0` (`0x204`), and `0x1254130/0x25C41E0` (`0x205`).

A subsequent five-boundary correlation armed those four inbound sites together
with the raw client-send entry. The entry sequence was `0x1A9` at 3942 ms,
`0x1A8` at 3944 ms, `0x19A` at 4408 ms, and `0x1A5` at 4577 ms. The first
`0x204` arrived at 5336 ms, 759 ms after `0x1A5`; `0x200`/`0x201`/`0x202`
followed at 7300-7310 ms. Message `0x19D` was sent at 12455 ms, after the
initial roster burst, so it cannot initiate entry-time population. Across the
60-second run the trace captured 14 each of `0x204` and `0x203`, three `0x205`,
and one each of `0x200` through `0x202`.

This repeatably makes `0x1A5` the nearest outbound predecessor, but does not
prove request/response semantics: its statically recovered caller is still the
asynchronous mission-ready/load-completion path. Replaying it remains unsafe
until its server-side state transition or a narrower downstream request is
identified.

Follow-up caller tracing resolves that direction. The `0x1A5` sender at RVA
`0x1417060` is called from success callback `0x14177D0`, which tests `EDX` and
calls the sender only when the asynchronous operation succeeds; failure routes
to callback `0x1417830`. The callback is registered by the inbound handler at
RVA `0x14111560`. A live handler-entry trace captured decoded record pointer
`RDX=0x2810D2841F0`; its first bytes were `00 01 47 01`, identifying inbound
message `0x100`. The handler parses the server record, initializes mission/world
state, starts the asynchronous load, and registers the completion callback.

The recovered causal chain is therefore server `0x100` -> local world/load
operation -> client `0x1A5` success acknowledgement -> server PvP provider
updates. There is no reversible client request at this boundary. Sending
`0x1A5` without the corresponding server transition would acknowledge work the
client was never assigned, while locally replaying `0x100` would re-enter a
broad world-load path. Neither is a safe build-refresh mechanism.

## Incremental population records

The live setter trace and the adjacent `ChCliPvp.cpp` functions show that the
client does not require the full-build routine for every remote update. Build
`205.780` has a family of packed incremental handlers immediately before the
combined handler:

| message | observed input | resolved operation |
| ---: | --- | --- |
| `0x200` | record `+0x02` player-list index, record `+0x06` rune content id | resolve through the thread-local PvP content context with request code `0x23`, then `SetRune` |
| `0x202` | record `+0x02` player-list index, record `+0x06` amulet content id | resolve with request code `0x36`, then `SetAmulet` |
| `0x203` | record `+0x02` player-list index, PvP Hero content id `+0x06`, and a byte at `+0x0A` | resolve through `ChCliPvpHeroCollection` request code `0x71`, then store the selected Mist Champion definition through `PvpGearProvider::SetPvpHero` |
| `0x204` | full packed record | applies two PvP-rank definitions and a flag, then amulet, relic, rune, and four sigils |
| `0x205` | record `+0x02` player-list index | destroy and free the `0xD8`-byte provider and clear `ChCliPlayer +0x97B0` |
| `0x206` | record `+0x02` player-list index and rank id `+0x06` | resolve a PvP-rank definition and store it at provider `+0x78` |
| `0x207` | record `+0x02`, rank ids `+0x06/+0x0A`, flag `+0x0E` | apply the same combined PvP-rank helper used by the full record |

The combined record's fixed-definition fields are recovered from its call
sites:

```text
+0x02  player-list index used by the ChCliPlayer lookup
+0x06  PvP-rank definition id
+0x0A  unaligned PvP-rank definition id
+0x0E  PvP-rank flag byte
+0x0F  rune content id, resolved with request code 0x23
+0x13  relic content id, resolved with request code 0x23
+0x17  amulet content id, resolved with request code 0x36
+0x1B  pointer to four 32-bit sigil content ids
```

The four sigil ids at `[*record + 0x1B] + 0x00, +0x04, +0x08, +0x0C` are
resolved with request code `0x23` and passed to `SetSigil` with indices `0..3`.
The resolver is reached through the current thread-local context returned by
`0x9B4820`, its `+0xE0` interface, and virtual slot `+0x70`. The provider's
own indexed getter is a different virtual slot: `PvpGearProviderVtable +0x70`
(`0x11F7690`), which bounds-checks the index below four and reads
`provider + 0xB8 + index * 8`.

An earlier live capture caught `SetRune` with `RDX` holding resolved definition
`79983` (The Lynx), and a return-site candidate in the incremental handler at
`0x1253CA9`. A focused entry trace also captured the short incremental rune
record while entering PvP:

```text
record +0x02 = 0x00000001  player-list index passed to the player lookup
record +0x06 = 0x000052CA  rune content id 21194 (The Ogre)
```

The short incremental records are packed and may be unaligned. Only the first
`0x0A` bytes of the rune/amulet/rank form and `0x0B` bytes of message `0x203`
form belong to those records; bytes beyond the form belong to adjacent memory
and must not be decoded as combined-record fields.

## Payload and target correlation

Prior static/runtime analysis established the target correlation without
changing the process: each packed update's `+0x02` field resolves through the
`ChCliContext` player lookup to a `ChCliPlayer`, whose `+0x74` value matches the
payload field. The provider's `+0x38` owner then matches that same player. The
field is therefore named `PlayerListIndex` in the native payload layouts for
build `205.780`. Remote roster objects may have a null `+0x18` character link,
so the list index, player name, and provider owner are the reliable correlation
fields for this path.

The same analysis established the `SetSigil` argument order as
`(provider, index, definition)`. The four-entry order is the native
`PvpWeaponUpgradeIndex` order: `0 = weapon set 1 first`, `1 = weapon set 2
first`, `2 = weapon set 1 second`, and `3 = weapon set 2 second`. The public
snapshot presents those entries as MH1, MH2, OH1, and OH2, respectively; the
native array remains the authoritative index contract.

All current validation is read-only. The message/handler observations are
evidence for how the world payload is populated, but no code address is a
runtime contract and no live breakpoint or mutation is required to consume the
already-populated world payload.

## Read-only live resolution

The PvP Hero, rank, and equipment fields can be resolved without attaching a
debugger or writing to the game. First enumerate the live providers; the
scanner prints the PvP Hero definition id, provider flags, rank-definition
ids, all seven PvP gear definitions, and physical inventory weapons when the
player's character/inventory exists:

```text
Gw2.Tools.exe --build 205.780 --pid <pid> gw2 pvp provider-scan 20
Gw2.Tools.exe --build 205.780 --pid <pid> gw2 pvp spectator-graph 256
```

The spectator graph command starts at the same `ContextCollection` anchor as
the provider scan, walks `ChCliContext.Players` to each `ChCliPlayer`, and
prints the provider relationship and resolved state. It performs only process
memory reads; the spectator UI is not the source of the payload.

The provider scan prints `ownerPlayer`, `playerListIndex`, and `agentId` for
each provider whose `ChCliPlayer +0x97B0` backlink still points to that provider.
Use those fields to correlate the provider with the player, then read the
provider and its non-null payload objects directly:

```text
Gw2.Tools.exe --build 205.780 --pid <pid> debug qwords <provider-va> 28
Gw2.Tools.exe --build 205.780 --pid <pid> debug qwords <payload-va> 8
```

`provider +0x60` is a `PvpHeroDefinition*`. Its live objects carry content
request type `0x71` at `+0x10` and the PvP Hero definition id at `+0x14`.
The rank definitions at `+0x68`, `+0x78`, and `+0xB0` expose their separate
definition ids at `+0x28`.

For change correlation, snapshot the provider before and after a controlled
rank or equipment update:

```text
Gw2.Tools.exe --build 205.780 --pid <pid> debug memory-diff <provider-va> 0xD8 30
Gw2.Tools.exe --build 205.780 --pid <pid> debug snapshot <provider-va> 0xD8 before.snap
Gw2.Tools.exe --build 205.780 --pid <pid> debug snapshot <provider-va> 0xD8 after.snap
Gw2.Tools.exe --build 205.780 debug snapshot-diff before.snap after.snap
```

The four sigils are independently verified by reading
`provider +0xB8 + index * 8` and their definition `+0x28` ids; they are
independent of the PvP Hero definition at `+0x60`.

The gear context is an embedded subobject of the `ChCliPlayer` object from
`ChCliContext +0x80`. For the local character, the client constructs
`ChCliCharacterContext` at `ChCliPlayer +0x4F18`; that subobject has a separate
vtable, is `0x4108` bytes, and owns the nine `0x730`-byte PvP loadouts. The
parent constructor places the next unidentified subobjects at `+0x9020` and
`+0x9080`, before `ChCliProgress` at `+0x9758`. This distinction explains why
the player-list name pointer at `ChCliPlayer +0x68` does not overlap the gear
loadout array at `ChCliCharacterContext +0x48`.

## Equipped-weapon boundary

The `ChCliPvp` provider payload contains the selected Mist Champion/PvP Hero,
PvP ranks, ratings, rune, relic, amulet, and four weapon-upgrade/sigil
definitions. It contains no physical weapon item definition. This corrects
the earlier interpretation of provider `+0x60/+0x68/+0x78/+0xB0` as a possible
weapon payload: `+0x68/+0x78/+0xB0` are PvP-rank definitions, while `+0x60` is
the request-`0x71` PvP Hero definition.

Physical weapon item ids use the character/inventory world-state path:
`ChCliPlayer +0x18 -> ChCliCharacter +0x3F0 -> ChCliInventory +0x160`, then
ordinary equipment slots `29..32`. Contrary to the earlier local-only wording,
live build-`205.780` provider scans found this chain populated for remote roster
players. One captured remote player exposed item ids `76158`, `96990`, `97581`,
and `29174`; another exposed `26432`, `26357`, and `31022` with an empty second
offhand. A null remote character or inventory still makes those physical item
ids unavailable for that particular roster entry.


## Inspector presentation target

The standalone Build Inspector intentionally follows the legacy spectator PvP build-panel
information architecture rather than a generic property table:

```text
player
  specialization line 1 | adept | master | grandmaster
  specialization line 2 | adept | master | grandmaster
  specialization line 3 | adept | master | grandmaster
  weapons                | set I main | set I off | set II main | set II off
  sigils                 | below the corresponding weapon slot
  centered PvP equipment | amulet | rune | relic
```

The current text layout preserves those lanes so specialization background art and trait/item
icons can replace the textual cells later without changing the panel structure. Raw content ids
remain in the separate Diagnostics view.

The ContextCollection/native graph is authoritative for build semantics. The inspector must not
use the public GW2 API to decide which specialization, trait, weapon, or competitive equipment a
player has equipped. API data is presentation enrichment only (for example artwork, icons, and
tooltip text) after the native identity is already known.

Native specialization ids and public `/v2/specializations` ids are different namespaces.
`TraitDefinition.Id` is likewise a client-native id and is not a public `/v2/traits` id.
Build 205.780 was exhaustively captured through validated
`SpecializationDefinition.TraitDefinitions` arrays: 81 specializations with nine major traits
each. The 729 native trait ids live in `TraitDefinition`, alongside the recovered native
layouts/access points only. Live ContextCollection pointers and ids remain authoritative. Any
future correlation or presentation layer must remain outside `Gw2.Contracts` and must never replace
an observed native trait identity with an API-derived result.

Trait lines are captured independently. A missing third/elite line no longer invalidates the first
two core lines; the roster label shows the elite specialization when one is equipped, otherwise it
falls back to the core profession when at least one trait line is present.

The recovered native `TraitDefinition` currently has verified id/tier fields but no verified
display-name/localization pointer. Build Inspector therefore keeps presentation correlation outside
`Gw2.Contracts`: `TraitPresentationCatalog` is keyed by native `TraitDefinition.Id` and additionally
checks the observed native specialization/tier/choice before returning a label. Its public trait id is
only an optional handle for icon/tooltip enrichment. Runtime API availability does not participate in
trait or specialization identity/name selection.

## Equipped-skill boundary

The spectator's five configured non-weapon skills are populated through a
separate per-player skill-state path, not through the PvP equipment provider.
Build `205.780` message `0x264` resolves payload `+0x08` as
`PlayerListIndex`, payload `+0x02` as a skill content id, and writes slot
`+0x06` into one of two five-entry definition-pointer arrays at
`ChCliPlayer +0x9BD8 -> ChCliSkill +0x60/+0x88`. Payload `+0x07` selects the
array, but its higher-level context enum is not yet named.

This path is distinct from runtime message `0x27C`, which updates
`ChCliCharacter +0x520 -> ChCliSkillbar` and can represent temporary combat
state such as transformations. For remote build/spectator inspection, the
per-player `ChCliSkill` state is therefore the stronger configured-skill
source. See `remote-pvp-equipped-skills.md` for the recovered handler,
payload layout, and remaining reader-side unknowns.

## Specializations and selected traits

The `BtTraits`/`SkEquipSpecLine` spectator path binds the embedded
specialization manager directly from `ChCliPlayer +0x9CB8`. Selected
specializations are pointers at `+0x58`, `+0x60`, and `+0x68`; each selected
line's major traits are pointers at `+0x78..+0xB8` (three consecutive pointers
per line). The native access layer reads those pointers from the player-list
entry, so this path works for remote players without requiring a character or
agent object.

## Read-only spectator graph walk

With the spectator Equipment tab open, a build `205.780` read-only walk found
one `BtGear` object and seven `BtEqpSlot` objects. The `BtGear +0xD8` pointer
was the inspected `ChCliPlayer*`; every equipment slot bound the same
`PvpGearProvider` through `BtEqpSlot +0x90`. The slot discriminator at `+0x98`
and the native upgrade index at `+0x9C` decoded as follows:

| Slot type | Upgrade index | Definition field | Resolved value |
| --- | ---: | --- | --- |
| relic | sentinel `4` | `+0x88` | Pirate Queen `106573` |
| rune | sentinel `4` | `+0x88` | The Revenant `70651` |
| sigil | `2` | `+0x88` | Revocation `81207` |
| sigil | `1` | `+0x88` | Doom `21150` |
| amulet | sentinel `0` | `+0x68` | Demolisher `34` |
| sigil | `0` | `+0x88` | Energy `21152` |
| sigil | `3` | `+0x88` | Exposure `81268` |

The four sigil slots therefore match the provider array by native index
`0..3` (weapon set 1 first, weapon set 2 first, weapon set 1 second, weapon
set 2 second). No additional `BtEqpSlot` type or physical weapon item
definition was present in the panel. Physical weapons therefore come from the
character/inventory slots, not an eighth `BtEqpSlot` or the provider's PvP-rank
fields.
