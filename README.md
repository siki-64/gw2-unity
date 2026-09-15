# GW2 Unity

Unity 6.6 custom-client research foundation. The goal is to consume inbound GW2 traffic, maintain client state and encode correct outbound traffic from recovered contracts.

Initial state: Unity project and engine-independent protocol package; curated reverse-engineering findings and archive-tooling source. For build 205.780 the transport cipher, handshake key derivation, inbound frame container and direction-specific schema corpora are recovered and validated against private captures, and the package implements inbound and outbound codecs. This is **not** live-server compatibility: no runtime wire message is registered as supported (`supportedWireBuilds` stays empty), outbound sequencing is unrecovered, and replay or editor compilation is not live interop.

Open this repository root with Unity **6000.6.0f1**. The protocol package is referenced locally from `Packages/com.siki.gw2.protocol`. Editor validation uses:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath "$PWD" -logFile "$PWD/artifacts/unity-validation.log"
```

Create `artifacts/` before running that command and close this project's interactive editor first.

| Path | Purpose |
| --- | --- |
| Assets/, Packages/, ProjectSettings/ | Unity project, presentation and eventual main-thread state projection |
| Packages/com.siki.gw2.protocol/ | Engine-independent protocol contracts and build 205.780 codecs (transport cipher, frame deframer + LZ4, schema decode/encode, direction-aware inbound/outbound codecs) |
| protocol/ | Build-scoped message catalog and evidence templates |
| research/contracts/ | Recovered game structure and routine contracts with the notes that describe them; excluded from Unity compilation |
| research/data/ | Offline archive/tooling source with its tests; excluded from Unity compilation |
| research/provenance/ | License, attribution and the findings manifest with per-file SHA-256 |
| tools/ | Offline reverse-engineering workflow tooling: catalog validation and build stamping, evidence hashing and provenance, the Ghidra anchor export, the Python decoders/ciphers/KDF, the schema-corpus extractors, and the `Gw2.Protocol` offline test harness. See [tools/README.md](tools/README.md) |
| tools/re/fixtures/<build>/ | Sanitized captured/synthetic byte fixtures (transport cipher, handshake KDF, `0x264` inbound wire, `0x120` outbound wire, keystream reuse) |
| tests/fixtures/ | Placeholder for future Unity-side replay fixtures; captured fixtures currently live under `tools/re/fixtures/` |
| captures/local/ | Ignored private capture workspace; create on demand, never tracked |
| local-assets/ | Ignored local game assets and conversion output; create on demand, never tracked |
| docs/ | Architecture and evidence workflow |

Start with [the workflow](docs/workflow.md). Imported reverse-engineering findings retain their original license; see `THIRD_PARTY_NOTICES.md`.
