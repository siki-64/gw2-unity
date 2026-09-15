# InfoBar native layout recovery

Static audit: 2026-09-13. Source: active Ghidra analysis project, the client image,
image base `0x140000000`. The imported image's SHA-256 is recorded in protocol/catalog.json.
The import was created September 1; its generic PE product version does not establish
the game build. Addresses below identify this imported image, not the current running
executable. No live attachment or rendering validation was performed in this audit.

The layouts are **not fully semantically recovered**. Object extents and named fields
must be distinguished from unknown storage. This note corrects older update-function
and presentation-offset descriptions in [pipeline.md](pipeline.md).

## Parent object: 0x130 bytes

Callback `0x1403ADAD0` allocates `0x130` bytes for message `0x0A`, invokes constructor
`0x1403ACDF0`, stores the frame id at `+0x10`, and initializes the embedded object at
`+0x18`. Message `0x0E` calls destructor `0x1403AD180`, frees `0x130` bytes, and clears
the callback's object slot.

| Range | Representation and evidence | Remaining uncertainty |
|---|---|---|
| `00..07` | primary vtable `0x14192BE50` | full virtual contract |
| `08..0F` | interface vtable `0x14192A4B0` | native interface name |
| `10..13` | frame id from message `0A` | existing WidgetId name retained |
| `14..17` | unknown storage | padding versus field |
| `18..47` | embedded object constructed by `0x14103DB10`, destroyed by `0x14103DC00` | internal field contracts |
| `48..4F` | zero-initialized context; optional creation payload supplies a pointer-sized value | ownership/consumers |
| `50..9F` | ten embedded interfaces | most service names/callback contracts |
| `A0..B7` | tracked unit: value, next, previous | invalidation propagation across every child |
| `B8..BF` | separate Character reference, initialized from context `+98` virtual `+60` | do not equate with unit's Character |
| `C0..C7` | secondary player lookup result maintained during replacement | exact secondary relationship |
| `C8..DB` | smoother C8 | complete policy semantics |
| `DC..EF` | smoother DC | complete policy semantics |
| `F0..103` | smoother F0 | complete policy semantics |
| `104..107` | animation phase | all visual consumers |
| `108..10B` | accumulated update time | external time-source unit contract |
| `10C` | packed flags byte | complete bit meanings |
| `10D..10F` | unknown bytes | padding versus fields |
| `110..113` | subwidget-message discriminator initialized to 4 | complete domain |
| `114..117` | unknown storage | existing float declaration is provisional |
| `118..12F` | tracked message reference: value, next, previous | complete ownership contract |

### Embedded interface registrations

Constructor/destructor pairs pass addresses inside the parent to services. These are
interface subobjects, not child-widget pointers. Slots below are byte offsets in each
service's vtable, not in InfoBar's vtable.

| Parent offset | Service accessor | Register/unregister slots |
|---|---|---|
| `50` | `0x1409B4820`, then context `+98` | `1A8 / 1B0` |
| `58` | Character object | `558 / 560` |
| `60` | `0x14129E360` | `2C8 / 2D0` |
| `68` | `0x141488450` | `168 / 170` |
| `70` | `0x1402661F0` | `2C0 / 2C8` |
| `78` | `0x1402661F0` | `218 / 220` |
| `80` | `0x140396F40` | `10 / 18` |
| `88` | vtable `0x141ADFE38` | registration not established |
| `90` | `0x14028AE40`, conditional on Character reference | `50 / 58` |
| `98` | `0x1402DAD10` | `1F8 / 200` |

Only `+58` is promoted to `CharacterTrackedClient` in this audit.

### Presentation smoothers

[`InfoBarPresentationSmoother.cs`](../../Gw2.Contracts/InfoBars/InfoBarPresentationSmoother.cs)
models the repeated `0x14`-byte state consumed by `0x140312660`:

| Relative offset | Field | Evidence |
|---|---|---|
| `00` | SmoothTime | omega = 2 / field00 |
| `04` | Current | loaded and updated by smoothing step |
| `08` | Target | convergence destination; setter `0x1402DDF20` writes it |
| `0C` | Velocity | carried between steps, zeroed on clamp/snap |
| `10` | SnapTolerance | compared to absolute current-target difference |

Constructor times are `0.05`, `0.025`, `0.025`; current/target/velocity start at zero;
all tolerances are `0.001`. The step clamps current to `[0,1]` and snaps to target
when within tolerance or crossing it.

Instruction anchors: `0x140312679` divides by `+00`; `0x140312697` loads target;
`0x1403126AD` loads current; `0x140312709/70E` write velocity/current;
`0x14031276C` loads tolerance; `0x1403127AC/7B2` snap and clear velocity.
Constructor writes `0x1403ACEF1..0x1403ACF87` establish the adjacent records.
Tick `0x1403AEBE0` passes `parent+C8/DC/F0` to this routine at
`0x1403AED2F..0x1403AED57` and `0x1403AED9A..0x1403AEDC2`.
Thus `+DC` and `+F0` are **record bases/time parameters, not current opacity**.

| Channel base | Current | Target | Child property consumers in tick |
|---|---|---|---|
| `C8` | `CC` | `D0` | slots 2 and 5 |
| `DC` | `E0` | `E4` | slots 3 and 4 |
| `F0` | `F4` | `F8` | slot 7 |

Slot 3 receives E0 inside the branch triggered by CC changing or flag bit 1. Slot 4
has its own E0-change check. Preserve this unusual gating; do not infer independent
channel updates. These values drive presentation properties, not CombatTracker health fill.

The tick compares all three current/target pairs before its early-out. On that path
it adds incoming delta time to `+108`; on work it uses that sum and clears `+108`.
Instructions `0x1403AEC88..0x1403AEC96` and `0x1403AECC9..0x1403AECDD` confirm this.
`0x1403AEEE6..0x1403AEF0C` advances `+104` by delta-time times pi, wrapped at two pi.

## Replacement, setup, tick, teardown

`0x1403AF840` is reached by parent message `4E`. It runs only when the incoming unit
differs from `+A0`. It calls `0x1403AD510` for child setup, then `0x1403AFBD0` for
tracked-unit replacement, propagates the unit to children, maintains slot 7 through
`0x1403B16A0`, and finalizes through `0x1403AFB40`. It is not an unconditional tick.

`0x1403AD510` sets flag `20` when the type-0 predicate is nonzero, calls health setup
`0x1403AD850`, then skips other child setup when that predicate is nonzero. The older
pseudocode's inverted flag assignment is incorrect for this image. Parent message
`41` reaches the separate presentation tick `0x1403AEBE0`.

`0x1403AFBD0` unlinks `+A0/+A8/+B0`, clears `+C0`, maintains the Character tracked
client when appropriate, and links the replacement unit. Character callback
`0x1403AE380` receives `this=parent+58`: it clears the unit if the removed Character
resolves to that unit, and separately clears/unregisters `+B8` when matched.
The destructor calls replacement with null, unregisters service interfaces, unlinks
both tracked references, and destroys the embedded `+18` object. Full child destruction
order and manager slot-generation/reuse semantics remain open.

## Child inventory

| Slot | Tag | Recovered role/layout |
|---|---|---|
| 0 | unresolved here | tracked-message-associated auxiliary child |
| 1 | unresolved here | auxiliary child laid out by `0x1403AEFC0` |
| 2 | `10030 / 10830` | AsHealth, existing 0x78-byte layout |
| 3 | unresolved here | agent-bound auxiliary child, receives channel DC |
| 4 | `10630` | agent-bound child with context/policy setters; channel DC |
| 5 | `2430` | conditional child; channel C8 |
| 6 | `30` | name child; existing NameRenderCtx layout |
| 7 | `630` | new 0x78-byte layout; visual/native class identity unresolved |

### Slot 7

[`InfoBarSlot7Widget.cs`](../../Gw2.Contracts/InfoBars/InfoBarSlot7Widget.cs)
is distinct from AsHealth despite their equal allocation sizes. `0x1403B16A0` uses
callback `0x1403BDC60`, tag `630`, slot 7, layer 3, and parent as creation context.
The wrapper forwards to `0x1403BDD00`; message `35` can replace context at `+50`.

The handler allocates/frees `78` bytes; frame id is `+18`, embedded storage is
`+20..4F`, callback context is `+50`, tracked agent is `+58/+60/+68`, and integer
geometry selector is `+70`. `+1C` and `+74` are unclassified storage.
Initialization instructions: `0x1403BDEB0..0x1403BDF2D`; unlink/destruction:
`0x1403BE069..0x1403BE0C6`.

Primary vtable `0x141AE0EA0` points to setter `0x1403BE220`. It uses tracked-reference
helper `0x140254E00`, queries service `0x140396F40` virtual `+58`, and stores EAX at
widget `+70` (`0x1403BE230..0x1403BE253`). Measurement message `38` selects 8x8 when
zero and 16x16 otherwise. Draw message `08` also selects between two pairs of data
records based on a Character predicate. This does not identify the in-world symbol.

## Completion requirements

Remaining work: parent embedded storage/interface callbacks and flags; NameRenderCtx
unknown fields and enum domains; child slots 0/1/3/4/5 and slot-7 visual identity;
manager/container layouts and reuse; shared framework storage. Unknown scalar
declarations in older overlays are not proof of native scalar types. Layout tests
check compiled overlays, not live identity, native semantics, or cross-build validity.
