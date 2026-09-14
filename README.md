# GW2 Unity

Unity 6.6 custom-client research foundation. The goal is to consume inbound GW2 traffic, maintain client state and encode correct outbound traffic from recovered contracts.

Initial state: Unity project and engine-independent protocol package; curated reverse-engineering findings and archive-tooling source. No transport, authentication, wire codec or live-server compatibility is implemented yet.

Open this repository root with Unity **6000.6.0f1**. The protocol package is referenced locally from `Packages/com.siki.gw2.protocol`. Editor validation uses:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath "$PWD" -logFile "$PWD/artifacts/unity-validation.log"
```

Create `artifacts/` before running that command and close this project's interactive editor first.

| Path | Purpose |
| --- | --- |
| Assets/, Packages/, ProjectSettings/ | Unity project, presentation and eventual main-thread state projection |
| Packages/com.siki.gw2.protocol/ | Engine-independent protocol contracts and eventual codecs |
| protocol/ | Build-scoped message catalog and evidence templates |
| research/contracts/ | Recovered game structure and routine contracts with the notes that describe them; excluded from Unity compilation |
| research/data/ | Offline archive/tooling source with its tests; excluded from Unity compilation |
| research/provenance/ | License, attribution and the findings manifest with per-file SHA-256 |
| tools/ | Future offline asset conversion and replay tools |
| tests/fixtures/ | Sanitized, documented fixtures for future codec/replay tests |
| captures/local/ | Ignored private capture workspace; create on demand, never tracked |
| local-assets/ | Ignored local game assets and conversion output; create on demand, never tracked |
| docs/ | Architecture and evidence workflow |

Start with [the workflow](docs/workflow.md). Imported reverse-engineering findings retain their original license; see `THIRD_PARTY_NOTICES.md`.
