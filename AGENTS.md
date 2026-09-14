# Working rules

- Unity presentation belongs in Assets/Gw2.Client. Engine-independent C# belongs in Packages/; keep it compatible with the installed Unity 6000.6 editor and validate in that editor.
- Read docs/workflow.md before protocol or findings-derived contract work.
- Distinguish native memory, decoded handler records, and wire encoding in every contract. Never serialize a native struct by copying its memory.
- Record source commit, source path, hash, build, direction, layer, confidence and unresolved fields. Do not silently carry message IDs into another build.
- Preserve unknown fields and raw values. Native content IDs, public API IDs, player-list indices and agent IDs are separate namespaces.
- Keep research/contracts and research/data as reviewed reference material separate from runtime code; port reviewed semantics into runtime code rather than editing findings in place.
- Add independent byte fixtures and malformed-input checks when implementing codecs. Outbound codecs additionally require captured serialization and session-state evidence.
- Never report replay or editor compilation as live server compatibility.
- Do not commit credentials, session keys, private captures, Gw2.dat or extracted game assets.
