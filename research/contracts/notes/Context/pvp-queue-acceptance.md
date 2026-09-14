# Match acceptance and queue timing

Build 205780, inspected 2026-09-05 in Ghidra analysis of the client image and the
running game (PID 3352 in this session). Heap addresses are session-local.

`PexQueueReady.cpp` at `0x1408195A0` / `0x140819730` obtains the match through
`PexCliContext` vtable `+0x48`. The live slot is RVA `0x400A10`, a getter for
`this+0xD0`. During the user's accept-window reproduction this changed from null
to `0x239E7286140` at 08:46:33 UTC. The queue object's flags were still queued
(`+0x1B0 = 0x1C`), so the existing queued boolean cannot pause the timer.

Match presence alone is insufficient: the object remained after acceptance.
The native countdown `0x14144ED90` requires match state 4. The state getter in the
virtual-base vtable (`0x1422DF878`, slot 6) is thunk `0x14144F0B0`, which adjusts
`this` before `0x14144E4A0` reads `[rcx-0x70]`. The constructor at `0x14144E940`
and vbtable `0x1422DF8C8` establish that this resolves to match-object `+0xC0`.
The running object's later state was 8. State 4 was identified from native
countdown code; it was not sampled directly during this capture.

The runtime reads the state through guarded memory access and propagates
`IsPaused` through Core and the GUI ABI. The timer freezes on acceptance, resumes
without counting the paused interval, and excludes pauses from completed samples.
Existing match-map suppression still ends queue display after map entry.

Validation remaining: install the rebuilt DLL and observe the displayed time
across a real accept window, including a failed pop returning to matchmaking.
