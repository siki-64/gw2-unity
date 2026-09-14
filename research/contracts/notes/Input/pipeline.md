# Keybind / input pipeline

**Confirmed build(s):** `204.489` for input-binding import; `205.780` for the native UI pointer
coordinate path.<br>
**Status:** build-local partial reconstruction of modifier polling, input-binding import, and the
FrMouse pointer-to-frame coordinate transform.<br>
**Unresolved:** the general gameplay key-read mechanism, the game's native wheel accumulator contract,
and cross-build validity.

How GW2 (`gw2-64.exe`) reads and imports gameplay keybinds.

## Confirmed facts

- `GetAsyncKeyState` is used by GW2, but only for modifier/lock keys (Shift/Ctrl/Alt/Scroll
  Lock, i.e. `0x10`/`0x11`/`0x12`/`0x91`) at the observed call sites.
- `hid.dll` is loaded in the process; `dinput8.dll` and `xinput*.dll` were absent from the
  observed module list. The general gameplay key-read mechanism is not yet identified.

## Input-binding import

Confirmed against build **204.489** by disassembly and a live import from the Options dialog:

- Selecting `conduit` caused the game to open
  `Documents\Guild Wars 2\InputBinds\conduit.xml`. An idle baseline over every static
  `CreateFileW` call site produced no hits; the selected import produced one file-open hit.
- The same observed thread carried the selected path through the file wrapper and XML-parser
  layers into `IbContext::ImportBindings`.
- `IbContext::ImportBindings` is vtable slot `+0xD0`. It takes the context and a non-null
  UTF-16 source identifier, resolves that identifier to an XML document, traverses the parsed
  binding entries, applies them through the input-binding objects, and returns success or failure.
- A source identifier can be an exported XML filepath or a game-native preset name. During a
  live specialization swap, `_arc_ue-Guardian-Luminary` resolved to a document and the importer
  returned one at its common epilogue. This call did not open the exported profile filepath used
  by the Options dialog.
- `IbContext::ImportBindings` contains no competitive-mode or map-type branch. Its only
  failure returns occur when path preparation fails or the XML/file layer does not produce a
  document; after a document is available, the function completes the binding traversal and
  returns success.
- In a same-session PvP comparison, a direct XML-file call from the DXGI `Present` thread passed path
  preparation but the XML/file layer returned no document. A manual Options-dialog call on the
  game UI thread obtained a document and succeeded. The observed Options caller gets `IbContext`
  and invokes slot `+0xD0` directly; no additional setup call occurs in that caller fragment.
- Calling from a `WH_GETMESSAGE` callback on the foreground game window's owner thread also returned
  failure in PvP. The successful Options event therefore cannot be reproduced merely by moving the
  direct call to either the render thread or the window-owner message thread.
- `GetIbContext` returns `g_IbContext`. The static initializer passes that same storage to
  `IbContext::ctor`, which installs the vtable containing `IbContext::ImportBindings`.

## Auto-swap specialization source

Auto-swap reads the local player's specialization from reconstructed native state; Mumble Link is
not the runtime specialization source.

The guarded sampling path is:

```text
ContextCollection
  -> ChCliContext
       -> LocalCharacter
       -> Players[0..PlayerCount)
            -> ChCliPlayer.Character == LocalCharacter
                 -> embedded ChCliSpecialization
                      -> SelectedSlot2
                           -> SpecializationDefinition.Flags
                           -> SpecializationDefinition.Id
```

`PlayerCount` is used as the player-table bound because the recovered
`ChCliContext::GetPlayerById` accessor applies that same bound before reading `Players[index]`.
The player wrapper is selected by exact native pointer identity against `LocalCharacter`, rather
than by correlating an external player identifier.

The third specialization slot is not itself equivalent to an elite specialization. The shared
addon-owned game-state snapshot preserves the selected slot-2 native definition id and its elite
flag. Auto-swap interprets a non-elite third slot as the `core` preset and translates elite native
definition ids to preset names before the existing debounce/import state machine consumes them.

Sampling runs from the recovered recurring game-thread dispatcher and is throttled to 100 ms. The
same refresh reads `ChCliContext` once and publishes both the character `Agent* -> Profession` table
used by profession healthbar colors and the local-player specialization state used by auto-swap.
All foreign-memory traversal uses the guarded process-memory reader, so map teardown or a stale
context root produces empty/unavailable snapshot state rather than a raw pointer dereference.


## Native UI pointer coordinates (build 205.780)

The self-implemented addon GUI originally treated `ScreenToClient` output as if it were already in
GW2 logical UI coordinates. That was incorrect whenever the client pixel size and native UI scale
differed, producing a hit-test offset that grew with screen position.

Build 205.780's FrMouse path at `sub_14107E440` reads:

```text
0x142893B58  raw mouse X
0x142893B5C  raw mouse Y
0x142893D38  native UI scale X
0x142893D3C  native UI scale Y
```

Before calling the frame screen-to-local conversion, the routine performs:

```text
scaledX = rawX * uiScaleX
scaledY = rawY * uiScaleY
```

The scaled pair remains in the native frame's bottom-origin screen space. The managed addon layout
is top-origin, so Core converts the scaled Y value back through the live root frame height before
hit testing. It reads only scalar values and does not retain a native input/frame pointer. If the
validated native snapshot cannot be read, Core falls back to a Win32 client point scaled by
`rootViewportSize / clientPixelSize`; simply adding the root origin is no longer used.

Wheel delivery is currently addon glue rather than a claimed recovered FrMouse wheel ABI. A
thread-local `WH_MOUSE` observer on the GW2 window thread accumulates vertical wheel deltas and keeps
the existing WndProc untouched. The GUI publishes its window bounds once per completed frame so
left-button/wheel messages inside addon windows can be prevented from clicking/zooming through into
the game while physical button state remains available to the immediate-mode controls. Right-button
messages remain game-owned because GW2 uses their press/release pair for camera control.
