# Contract-to-client workflow

## Architecture

Inbound: transport bytes -> session framing/decryption/decompression -> build-specific wire decoder -> semantic event -> client-state reducer -> Unity main-thread presentation.

Outbound: user intent -> session/state validation -> semantic command -> build-specific encoder -> session framing -> transport.

Keep transport, session, protocol, state, assets and Unity presentation as distinct responsibilities. Add assemblies when each has implementation, rather than create empty projects now. Runtime code must not reference injection, pointers, native UI, discovery or patching from reverse-engineering findings. Offline .NET 10 asset tools may reuse Gw2.Dat independently after review; do not reference its DLL directly from Unity.

## Evidence progression

1. Select one scenario and build. Record executable hash, capture point, connection role, direction, timestamps and triggering action. Store private captures under captures/local.
2. Trace both sides of the boundary: wire bytes and native schema-decoder/serializer output. Record framing, bit packing, sizes, state prerequisites and transforms separately. A handler record is not a wire fixture.
3. Add a catalog entry using protocol/message-template.json. Mark each claim as unknown, static, observed or independently reproduced. Keep contradictory evidence explicit.
4. Write a sanitized fixture with its provenance and expected semantic result. Synthetic fixtures test implementation only; they cannot confirm the protocol. Preserve unknown fields and avoid assigning unproven names.
5. Implement one bounded codec and deterministic state update. Validate truncation at every boundary, bad lengths/counts, unknown IDs, allocation limits and no partial state mutation. Round trips supplement independent fixtures; they do not replace them.
6. Replay offline, then visualize the resulting state in Unity. Keep Unity API access on the main thread and bound inbound work per frame.
7. For outbound messages, recover the actual serializer and session state machine, sequence/acknowledgement rules and required prior messages. Compare encoded bytes to captured output before a controlled interoperability run.
8. Record validation separately: static analysis, synthetic tests, captured replay, Unity editor/player and live interoperability. On build changes, revalidate the affected catalog rather than changing one global number and assuming compatibility.

## First milestones

1. Recover transport/connection roles, handshake, framing, ID encoding and schema decoding. Authentication, encryption/compression and reconnect behavior remain unknown here.
2. Recover player lifecycle and identifier mapping, then replay a minimal player state with one configured-skill update. The 0x264 research is a handler-level starting point.
3. Recover world-entry 0x100 and outbound completion 0x1A5 fully, including prerequisites; the existing semantic trace is not sufficient to emit the acknowledgement.
4. Extend into movement/spawn/despawn, then PvP equipment and skill state. Treat ordering, destruction and index reuse as first-class state behavior.
5. Convert local archive assets through offline tooling into Unity-ready resources, keeping content IDs and public API IDs distinct.

## Scope of the findings

The research spans build 205780 and is a historical evidence scope, not a declaration of current server support. Reuse the re/notes research, the Gw2.Native contracts (field semantics, enum values, handler records; not pointer offsets or ABI) and the Gw2.Dat archive/texture/schema tooling source. Do not reuse native UI, D3D11 proxy, injection or patching, which are tied to the official client's in-process execution, nor live captures, caches, dumped fonts or game assets.

No confirmed wire encoders, transport handshake or complete message schema catalog have been established, so the initial runtime registers no supported wire messages.

## Updating findings

Snapshots under `research/imports/reverse-engineering-findings/<source-commit>/` are immutable. To refresh, add a new snapshot named by the full source commit of a clean checkout rather than editing an existing one, and record each file's original path and SHA-256 in its `manifest.json`. Review manifest differences before promoting facts. Do not import dumps, generated atlases or private traces by globbing the whole repository.

Unity reference: https://discussions.unity.com/t/unity-6-6-is-now-available/1735357. The local editor version is authoritative for this checkout; validate compatibility in the editor rather than infer it from a .NET SDK build.
