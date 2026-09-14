# Native localization and text-service path

**Confirmed build(s):** unknown; evidence is from an independent July 2026 native consumer plus current repository TLS/context observations.<br>
**Status:** execution-context requirements and service relationship recovered; callable ArenaNet ABI remains unresolved.<br>
**Unresolved:** exact TLS accessor identities, text-service type, localization request structure, callback ABI,
and whether the TLS-backed service feeds the already recovered `CtlText`/`FrText` layout path.

## Execution context

A native localization consumer does not treat text lookup as a context-free helper. It explicitly tracks and validates game TLS context, render/current-thread TLS, text-service state, a text-key helper, a localization request helper, and a callback.

Observed failure states include missing game TLS, missing text service, unavailable text-key helper, unavailable localization request helper, TLS installation failure, and TLS restoration failure.

The path can temporarily install a validated game TLS context while issuing the localization request and then restore the previous thread state. This independently reinforces the repository's existing finding that native client calls cannot be assumed safe merely because the process and pointers are valid.

For the native UI project, text/localization calls should be made from a known native UI/render phase or through a recovered execution-context adapter. Do not call them arbitrarily from DXGI `Present`.

## TLS text-service relationship

The consumer validates text-service state rooted at offset `+0x50` of the borrowed/validated TLS context before issuing a request.

Treat this as `game TLS context +0x50 -> text-service-related state`, not yet as a canonical pointer field. The validation helper may be interpreting a wrapper/reference stored at that location.

The implementation also maintains rediscovered/cache values corresponding to `ptr_offset` and `tls_game_ctx_off`, which indicates that reaching the valid game TLS context is itself a two-stage recovered contract.

## Request behavior

The consumer builds a temporary request object, resolves a text key through a native helper, submits localization work, receives output through a callback path, and restores the previous TLS state.

This is a useful lower-level target than constructing a full `CtlText` widget: `text key -> native localization/text service -> decoded/localized string`.

It does **not** by itself solve glyph layout or rendering. Keep localization/decoding separate from the
recovered renderer path documented in [`../Frames/native-text-rendering.md`](../Frames/native-text-rendering.md):
`string -> metrics/glyphs -> native draw submission`.

For build 205.780, the current static and live font evidence rules out the FrText/localization layer
as a source of fallback-square substitution for direct UTF-16 text. FrText preserves line spans into
the original UTF-16 buffer, while the GrFont layer performs exact code-unit lookup. The tested
Greek/Cyrillic and kana code units returned null glyph records and rendered as blank advance-only
gaps; literal `U+25A1` resolved to its own non-null square glyph. The blank result is therefore not a
localization rewrite to a placeholder, and any square observed in another UI path requires separate
text/codepoint evidence.

## Relationship to FrApi

The generic FrApi surface already has a `TextDecoded`-like target documented in [`../Frames/frapi.md`](../Frames/frapi.md). The TLS-backed localization path is an independent lead for recovering the lower text-service layer behind or beside that API.

## Next steps

- recover the current-build TLS getter and the meaning of the `+0x50` text-service state;
- identify the text-key helper call contract;
- identify the localization request/callback structure;
- trace callers from localized output into `CtlText` and determine whether the same service supplies its
  decoded string or shaping inputs;
- determine whether the same service is used by `CtlText` and the InfoBar name renderer.
