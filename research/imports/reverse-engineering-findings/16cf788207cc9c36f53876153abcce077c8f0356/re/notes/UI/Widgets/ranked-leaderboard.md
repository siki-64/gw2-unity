# Ranked leaderboard widget

**Confirmed build(s):** `205.780`.  
**Status:** static reconstruction of bind, identity refresh, color storage and render fallback;
earlier live gray/white and notification traces retained below.
**Unresolved:** fresh live correlation of the matching contact callback with the color write,
semantic names of the social-state virtuals, and whether a PvP queue pop reaches this callback.

## Source and function anchors

The generic leaderboard controls retain ArenaNet source paths for
`Ui/Widgets/Leaderboard/LbPage.cpp` and `LbEntry.cpp`. The PvP page retains
`Ui/Widgets/Pvp/Stats/PvpRatingLeaderboard.cpp`.

| RVA | Working name | Evidence |
|---|---|---|
| `0x006E1740` | `LbEntry_ControlCallback` | `LbEntry.cpp` assertions; allocates and destroys the per-row state |
| `0x006E2840` | `LbEntry_Bind` | binds a leaderboard entry and constructs its column controls |
| `0x007EB8E0` | `PvpRatingLeaderboard_ControlCallback` | `PvpRatingLeaderboard.cpp` assertions; allocates an `0x88`-byte page state |
| `0x007EBFD0` | `PvpRatingLeaderboard_InitializeControls` | constructs the page controls and installs their presentation colors |
| `0x007EC620` | `PvpRatingLeaderboard_Refresh` | requests a page of entries and refreshes page-control visibility |
| `0x008DBB60` | `AccountNameControl_SetIdentity` | copies 16 bytes to `+0x68`, then tail-calls refresh |
| `0x008DB920` | `AccountNameControl_SetColor` | sets child 0 to supplied color, child 1 to `0xFF808080` |
| `0x01045860` | `CtlText_SetColorOverride` | copies four bytes to `+0x98`, invalidates frame on change |
| `0x01071AA0` | `FrText` model construction | zero-alpha override selects style-record color at `+0x08` |

These names are intentionally scoped to observed behavior. They do not yet establish native class
names or complete prototypes.

## Generic `LbEntry` state

Control event `0x0A` in `LbEntry_ControlCallback` allocates `0xD0` bytes. The recovered tail is only
a partial layout:

| Offset | Observed use |
|---|---|
| `+0x18` | owning frame/control ID used for child lookup |
| `+0x70` | flags; bit 1 is set or cleared by the bind path |
| `+0x78` | grid pointer (`m_grid` assertion); listener at row `+0x68` is registered with it |
| `+0x80` | displayed rank, supplied to bind or obtained from entry virtual `+0x20` |
| `+0x88` | bound entry/interface pointer |
| `+0x90` | copied 16-byte board ID used by page callbacks |
| `+0xA0` | transient pointer into the bound column array |
| `+0xB0` | owned allocation pointer released during event `0x0E` |
| `+0xB8` | associated length/capacity state |

The code-level [LbEntry](../../../../src/Gw2.Native/UI/Widgets/LbEntry.cs) is deliberately partial:
allocation size and accessed fields are modeled, while unknown base storage and buffer metadata
remain gaps. It is a borrowed native view, not an allocation or ownership contract.

`LbEntry_Bind` obtains a column array from the bound leaderboard object. Each observed column record
has stride `0x20`; at most five records are rendered by the loop (child indices 3 through 7). The
entry interface selects between numeric and string presentations through adjacent virtual methods.

Near the end of the bind, `LbCliContext::LookupBoardState` (interface virtual `+0x10`) is called with
the copied board ID at `LbEntry +0x90`. It looks the GUID up in a table at context `+0x38..+0x40` and
returns the stored 32-bit board state. When that state is `1` or `2`, and the associated
portal identity is the local/active object (virtual `+0xA8`), child control 0 receives a
special presentation token `0xFF4EC04E`. Live tracing showed the lookup returning `0` for every row
on an initial gray leaderboard bind.
Opening Friends produced no re-entry into this bind path while names still changed white, ruling this
branch out as the direct Friends-triggered transition.

The per-row portal/leaderboard object's vtable is at RVA `0x01926D78`. Its virtual `+0xA8` compares the
object against the active object at the portal context's `+0x1F8 -> +0x50`; virtual `+0xB0`
copies the 16-byte identity from object `+0x10`; virtual `+0xB8` returns the mode at object `+0x78`.
Live binds produced a distinct object address per row, so it must not be documented as a single
leaderboard-wide instance. In mode 0, bind passes that GUID to the account-name control's
`SetIdentity`; it is therefore an account/contact lookup key, distinct from the board-selection GUID
copied from bind argument 4 into row `+0x90`. Existing `PlCliLeaderboard_*` Ghidra names are historical
working names, not proof of the native class or of board identity semantics.

## PvP rating page

The PvP rating page state allocated by control event `0x0A` is `0x88` bytes. Confirmed page fields
include:

| Offset | Observed use |
|---|---|
| `+0x18` | owning frame/control ID |
| `+0x20` | shared control/layout state |
| `+0x58` | observer registered with the leaderboard manager; unregistered on destruction |
| `+0x60`, `+0x68` | additional interface vtable subobjects; complete roles not yet named |
| `+0x70` | page mode (`0`, `1`, `2`, or another/no-page state) |
| `+0x78` | leaderboard/interface pointer |
| `+0x80` | zero-based page index; multiplied by 10 |

`PvpRatingLeaderboard_Refresh` requests ten entries for modes 0 and 2. Mode 1 uses a separate
request virtual and the current account/context value. It then updates child visibility, including
page controls 5 and 6.

`PvpRatingLeaderboard_InitializeControls` creates page controls 5 and 6 with both
`0xFFB4B4B4` and `0xFFFFFFFF` presentation colors. These are page-level controls. The row-name
palette below is different; page colors do not establish entry-name behavior.

## Live account-name evidence

With the ranked leaderboard open, the visible label `gabumon` existed in decoded heap data as the
full UTF-16 account handle `gabumon.5298`. Other entries in the same data region also carried hidden
four-digit discriminators. The ranked UI therefore has a stable account identity available even
though it renders only the name portion.

The first pointer followed from that string led to an `FrText` object, not a leaderboard record.
Static analysis of its function pointer reached `Engine/Frame/FrText.cpp`; removal dispatches frame
event `0x39`. A confirmed `0x400`-byte capture around that text object was byte-identical while the
visible leaderboard name changed from gray to white. Consequently, the tested text object's storage
does not hold the gray/white state.

The local player's leaderboard name is always white, including immediately after reopening the
leaderboard when the other tested names are gray. White therefore cannot mean only "currently shown
in the Friends window." Static bind explains the local exception: virtual `+0xA8` skips the
post-refresh gray override for the local/active identity. A later refresh can clear other rows'
account-name overrides without rebinding them. The temporal correlation with opening Friends is
live-observed; its exact notification-to-write sequence still needs a paired live capture.

## Friends-triggered notification

A valid trace at RVA `0x008DB430` while opening Friends produced 140 hits from the same return site,
RVA `0x01044DD9`. That return site follows an indirect callback in the generic text-control event
`0x39` path. Static analysis shows the event appends text, refreshes the control, and dispatches a
boolean indicating whether the supplied text pointer is non-null.

RVA `0x008DB430` itself is a listener thunk. Given a listener subobject, it reads the owning frame ID
at `listener-0x40` and the callback owner at `listener-0x08`, resolves the native control from the
frame ID, and calls the owner's first virtual method. It does not consume the incoming control ID or
boolean arguments. The earlier paired `0`/`1` register values therefore describe generic text-update
traffic, not a decoded friend or relationship state.

The focused `leaderboard-name-notify` trace mode records the listener, owning frame ID, callback
owner/vtable/callback RVAs, and incoming registers so leaderboard-owned listeners can be separated
from the other contact controls updated by opening Friends. A fresh gray-to-white transition is
required; reopening Friends after the names are already white does not exercise this path.

The focused gray-to-white capture produced 134 hits at `0x008DB430`. Of these, 133 continued through
RVA `0x0047DB10`; tracing that adapter produced 133 hits with one shared owner vtable at RVA
`0x022E8148` and one shared first virtual callback at RVA `0x014A7DB0`. Static adjacency and the
nearby source string identify this chain as `CntNameGridCell.cpp`. The terminal callback forwards an
owner field at `+0x58` through the interface at `+0x50`, virtual `+0x28`.

This chain is the Friends list populating its own name-grid cells. It is temporally correlated with
the leaderboard color change but is not the leaderboard update itself. The zero `resolvedControl`
values and per-contact owner allocations should not be interpreted as a relationship flag or as
leaderboard row objects.

## Account-name child and contact observer

The leaderboard row's child 2 uses an account-name compound control whose main callback is RVA
`0x008DAEC0` (`AccountNameControl_ControlCallback`). Its event `0x0A` allocates `0x78` bytes and
registers the subobject at state `+0x60` with the portal/contact manager returned by RVA `0x2661F0`.
The partial control-specific tail is:

| Offset | Observed use |
|---|---|
| `+0x50` | optional owner callback installed by the containing row |
| `+0x58` | observer/listener vtable pointer |
| `+0x60` | registered portal/contact observer subobject |
| `+0x68` | 16-byte account/contact identity key |

RVA `0x008DB590` (`AccountNameControl_OnContactUpdated`) is the registered observer callback. It asks
the incoming contact object for its 16-byte identity and compares all four dwords against state
`+0x68`. Only an exact match calls RVA `0x008DB630` (`AccountNameControl_RefreshIdentity`). This is a
identity update path used by the leaderboard's shared account-name control, not a leaderboard-only
callback. Destruction unregisters the observer through manager virtual `+0x08` before freeing state.

`AccountNameControl_RefreshIdentity` looks up the matching contact through manager virtual `+0xF8`
and obtains a second identity object through virtual `+0x280`, also passing state `+0x68`.
The assembly at `0x008DB72C` explicitly loads that key into RDX before the call at `0x008DB73D`.
The decompiler omits the argument: calling this a current/local-identity getter was incorrect.

## White/gray mechanism

The two children are text controls: child 0 holds an optional alternate display name and child 1
holds the formatted account name. `0x008DB470` constructs both; bind selects text style 6 for both.
The account-name control itself has no persistent color field in its recovered tail.

For the mode-0 identity path, bind calls `SetIdentity` (which refreshes immediately), then tests
identity virtual `+0xA8`. If false, it calls compound virtual `+0x18` (`0x008DB920`) with
`0xFFC8C8C8` from `0x014956C0`. That setter gives child 0 the supplied gray and unconditionally
gives child 1 `0xFF808080` from data RVA `0x01B6D6D8`. This happens **after** the initial refresh.
Rank and numeric columns separately receive `0xFFC8C8C8` for non-local identities.

Refresh uses the following exact predicate; virtual meanings remain deliberately unnamed:

```text
contact = manager.vF8(accountIdentity)
identity = manager.v280(accountIdentity)
eligible = (identity != null && signed(identity.v148()) >= 2)
        || (contact != null && contact.v48() != 0 && contact.v58() != 0)
alternate = identity != null && eligible && !frameFlag400
          ? identity.v250() : null
child1.colorOverride = alternate != null ? 0xFF808080 : 0
```

If both lookups are null, refresh writes placeholder text and returns before the color setter,
leaving the previous override intact. Otherwise it writes both text children and then the override.
The contact name comes from contact virtual `+0x50`, with identity virtual `+0x138` as fallback.
Formatter `0x0026AA20` removes a prefix through a colon and, unless frame flag `0x800` is set,
truncates at the account discriminator's dot. Flag `0x400` suppresses the alternate-name path.
The leaderboard creates this compound control with flags `0x630`, including `0x400`, so ordinary
refreshes on this path select a zero override for child 1 whenever at least one lookup succeeds.

Text virtual `+0x58` is `0x01045860`: it compares the four incoming color bytes with
`CtlText +0x98`, stores them if different, and invalidates the owning frame via `0x0106B320`.
The draw path carries that value through `0x0104436D0`, `0x01041550`, and `0x0106AF90` to
`0x01071AA0`. There, **any zero-alpha color selects the current text style's color**, not
transparent black. Style resolution at `0x010717F0` uses 0x18-byte records with color at `+0x08`;
the static fallback record at `0x025B0890` contains `0xFFFFFFFF` there. The live-selected style 6
record was not captured in this pass; prior observations establish that the resulting name is white.

Thus gray-to-white can result from removing bind's explicit gray on a later identity refresh.
It does not require a friend boolean becoming true, a board-state change, or a write to the
previously sampled `FrText` allocation. The meaningful storage is the account-name child `CtlText`.

## Code-level partial views

- [LbEntry](../../../../src/Gw2.Native/UI/Widgets/LbEntry.cs): `0xD0`, grid, rank, entry and board key.
- [PvpRatingLeaderboard](../../../../src/Gw2.Native/UI/Widgets/PvpRatingLeaderboard.cs): `0x88`, page mode/index and interfaces.
- [AccountNameControl](../../../../src/Gw2.Native/UI/Widgets/AccountNameControl.cs): `0x78`, observer and account key.
- [CtlText](../../../../src/Gw2.Native/UI/Widgets/CtlText.cs): `0xF0`, color override `+0x98` and style `+0xE0`.

These are build-specific inspection layouts. Unknown base fields remain omitted, and the views add
no hooks or runtime writes. GUID fields preserve the native 16 bytes; do not confuse formatted GUID
endianness with the raw hex identity dumps. Ghidra inspection was read-only, including dry-run
disassembly of the small setters that lack function definitions.

## Next validation

1. Reopen the ranked leaderboard to reset the names gray, then trace RVA `0x008DB590` while opening
   Friends. Capture the observer state, its 16-byte key, and the matching incoming contact object.
2. Correlate that hit with the call at `0x008DB8E8`, then capture child `CtlText +0x98` changing
   from `0xFF808080` to zero and resolve its style at `+0xE0` to the live style-record color.
3. Repeat that narrow trace during a queue pop to determine whether matchmaking reaches the same
   social-identity path.

No GW2 process was running during this static follow-up, so these fresh live checks were not performed.
The partial structs reflect independently verified static accesses, not a claim of live validation.
Runtime heap addresses are session-local; RVAs and layouts above are build-specific.
