# Map-loader stream timeouts

**Confirmed build(s):** `204.489`.<br>
**Status:** build-local state-machine and timeout-field reconstruction.<br>
**Unresolved:** behavior and field contracts in other game builds.

The authoritative field layout is [`MapLoader.cs`](../../../../src/Gw2.Native/World/MapLoader.cs); function
identities and build-specific analysis state remain in the active Ghidra project; see [`re/methodology/ghidra.md`](../../../methodology/ghidra.md).

Build `204.489` contains a state machine in `MapLoaderAdvance`. Its `MapLoader.state` switch moves
through the map, model, map-asset, agent, and ready-wait phases identified by the target's own state
log strings and assertions.

`MapLoaderResetState` initializes three timeout fields. The normal path writes:

- `60000` ms to `MapLoader.modelStreamTimeoutMs`;
- `15000` ms to `MapLoader.mapAssetStreamTimeoutMs`;
- `120000` ms to `MapLoader.agentStreamTimeoutMs`.

An internal target setting selects `5000` ms for each field instead. The later state handlers copy
the three fields into their corresponding countdown fields as the loader enters the model,
map-asset, and agent streaming phases.

The Fast Loading proxy feature replaces the three normal-path immediates with `400` ms. It does
not bypass the state machine or declare unfinished work complete; it shortens the maximum waits
used by those streaming phases. Disabling the feature restores the original immediate values.
