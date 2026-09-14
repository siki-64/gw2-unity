# Addon content lifetime

Evidence: Ghidra `gw2-re` / `Gw2-64.exe`, image base `0x140000000`, inspected
2026-09-05. The running game's MumbleLink reported build 205780. Static function
bodies and the redraw wrapper's disassembly were checked; live addon acceptance
of the change below remains pending.

- `0x14106A400` resolves a frame ID and appends a model through `0x141074950`.
- `0x141075FC0` drains pending frame content, calling `0x1410760F0`.
- `0x1410760F0` calls `0x141075830` to release old layer entries, then sends
  content messages and traverses the rebuilt content.
- `0x141073900` flattens retained frame content for the cache draw path.
- `0x14106B320(uint frameId)` resolves the frame via `0x141082170`, adds
  `0x108`, and tail-calls `0x141075A80`. That helper schedules the content on
  the pending list via `0x14023B130`.

Therefore clearing managed queues or hiding a managed window does not itself
remove its native models. Request the anchored frame's normal rebuild at outer
traversal entry, including when addon windows are hidden. Do not request it from
the content callback: that would reinsert a node during the same list drain.
Never clear a borrowed child's native layers directly; they also contain game UI.

`NativeFrameRedrawRequest` validates the complete build-local wrapper bytes before
calling it and resolves the frame ID anew. No native frame pointer is retained.
Also keep `SetEnabled(true)` idempotent: applying settings during GUI rendering
must not reset the anchor or allow another submission in the same traversal.

Acceptance: close F6/F9 windows, start/end a queue, hover native tooltips, and
change Companion settings. Verify that old windows and offset timer copies do
not remain, and that native UI and frame rate remain normal.
