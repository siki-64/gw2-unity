# Native GW2 text rendering

**Confirmed build:** 205.780<br>
**Status:** native `CtlText` assignment, measurement, layout, glyph-model creation, frame-content
model grouping, `FontContext` range registration, and the `GrFont` range lookup/coverage-raster path are
statically recovered and partially live-traced. A build-205.780 live glyph probe confirmed that the
tested unsupported Greek/Cyrillic and kana code units reach exact lookup, return no glyph record,
and render as blank advance-only gaps. Literal `U+25A1` resolves to a real square glyph; it is not
automatically substituted for an unsupported code unit.
A fixed client-owned draw call has been live-validated. The root pre-traversal submission experiment was rejected by a live
`GrModel::m_frustum` assertion; the current safe experiment uses the already-observed native content
callback and its current frame id.<br>
**Scope:** this note records build-local evidence only. RVAs below are relative to the 205.780
`Gw2-64.exe` image base.

This is the native text path behind ordinary UI labels. It is distinct from the solid-quad glyph
fallback and from the generic `FrameContentParams` quad submitter.

## Recovered call chain

The current evidence supports this split between control-side callbacks and the reusable FrText layer:

```text
CtlText raw/coded text state
  -> CtlText draw callback sub_141041550
       -> FrApi text draw sub_14106AF90
  -> CtlText measurement callbacks sub_141041770 / sub_141045370
       -> FrApi text measure sub_14106F280
  -> both wrappers
       -> sub_141071AA0                  FrText style/layout
            -> sub_1410715C0             line breaking and per-line measurement
                 -> sub_140AD6EB0         GrFont line measurement
                      -> sub_140AD60F0     GrFont UTF-16 measurement walker
                           -> sub_140AD67F0 exact glyph/range lookup
                                -> sub_140AD7AA0 lazy range load
                                -> sub_140C04030 glyph hash lookup
            -> sub_140AD6A80              standard glyph model creation
               or sub_140AD6BC0            caller-supplied material variant
       -> sub_141074A80                  FrContent model-group append
            -> existing FrContent/FrCache/GrDev/BGFX path
```

The final text append function is `sub_141074A80`, not `sub_141074950`. The latter is the separate
`FrameContentParams` rectangle/type-9 path used by `sub_14106A400`.

### Control text assignment

The current-build `CtlText` callback table is anchored by the `CtlText.cpp` source string at
`0x142117020` and includes these functions:

| Function | Evidence-backed behavior |
|---|---|
| `sub_141045B40` | `rawText` setter; validates the UTF-16 pointer, copies the null-terminated string into the control buffer at `+0x60`, updates count at `+0x6C`, selects raw-text mode at `+0x78 = 0`, then invalidates/rebuilds dependent state. |
| `sub_141045A80` | `codedText` setter; copies the UTF-16 coded string into the same control buffer, sets `+0x78 = 0xFFFFFFFF`, and sends it through `sub_141071560`. |
| `sub_1410459F0` | style setter; stores the style index at `+0xE0`, propagates the style to text children, and invalidates the frame state. |
| `sub_141045910` | material/presentation setter; caches material byte count at `+0xC0`, material data pointer at `+0xC8`, and a 16-byte presentation value at `+0xD0..+0xDF`. |
| `sub_141045C20` | image/material-text setup; loads texture handles and then uses the same coded-text storage/invalidating path. |

The generic frame message wrappers include:

```text
sub_141042D90 -> sub_14106DA10(frameId, 0x5D, codedText, 0)
sub_141042CF0 -> sub_14106DA10(frameId, 0x57, value, 0)
sub_141042BC0 -> sub_14106DA10(frameId, 0x50, 0, decodedText)
sub_141042C10 -> sub_14106DA10(frameId, 0x52, 0, codedText)
sub_141042C60 -> sub_14106DA10(frameId, 0x51, 0, decodedText)
```

`sub_14106DA10` resolves the frame and dispatches the message through the frame message list. The
extended form `sub_14106DAA0` copies the six stack/register payload values into a temporary message
record before dispatching it. These are control/widget message boundaries, not yet client-facing APIs.

`sub_1410446B0` is the current-build `CtlText` message/update dispatcher. Its measurement case (`0x38`)
builds a temporary text parameter block and calls `sub_14106F280`. Its draw/update cases reach
`sub_1410436D0`, which selects the appropriate text callback and ultimately calls
`sub_14106AF90` for native glyph submission.

## FrApi text boundaries

### Draw boundary: `sub_14106AF90` (`RVA 0x106AF90`)

The statically recovered Windows x64 ABI is:

```text
void sub_14106AF90(
    uint32 frameId,             // ECX
    const uint16_t* text,       // RDX; decompiler type is imprecise
    uint32 textCount,           // R8D
    const float rect[4],        // R9
    uint32 textFlags,           // [RSP+0x28]
    uint32 styleId,             // [RSP+0x30]
    uint32 layoutFlags,         // [RSP+0x38]
    uint32 packedColor,         // [RSP+0x40]
    FrTextParams* params,       // [RSP+0x48]
    uint32 contentGroup         // [RSP+0x50]
);
```

The wrapper:

1. validates `frameId` and `text`;
2. resolves `FrFrame*` through `sub_141082170(frameId)`;
3. initializes a temporary model-pointer array;
4. calls `sub_141071AA0(frame + 0x2A4, ...)`;
5. appends the generated models with `sub_141074A80(frame + 0x108, count, models, 0, 0xFF,
   contentGroup, 0)`; and
6. releases only the temporary model-array storage.

The `contentGroup` value is passed to the `FrContent` layer-array index. Native `CtlText` calls use
zero in the directly recovered callback; another native text caller uses six. The accepted range is
not revalidated as a client contract here.

### Measurement boundary: `sub_14106F280` (`RVA 0x106F280`)

The statically recovered ABI is:

```text
float* sub_14106F280(
    float outSize[2],            // RCX; returns the same pointer
    uint32 frameId,              // RDX
    const uint16_t* text,        // R8
    uint32 textCount,            // R9
    const float rect[4],         // [RSP+0x28]
    uint32 textFlags,            // [RSP+0x30]
    uint32 styleId,              // [RSP+0x38]
    uint32 layoutFlags,          // [RSP+0x40]
    const FrTextParams* params   // [RSP+0x48]
);
```

This wrapper copies `0x40` bytes of `params` when non-null. It forces the bounds-output bit and uses a
local four-float bounds rectangle when the caller did not provide one. It calls the same
`sub_141071AA0` layout function with a null model-output array, then returns width and height derived
from the resulting bounds. This is the best future `CalcTextWidth` candidate because it does not expose
a `GrFont*` to managed code.

The measurement ABI remains an uncalled candidate boundary. The draw ABI has been live-validated in a
normal game world and is now used by the managed renderer. The renderer uses the recovered wrapper
only; it does not construct a native widget or retain any native object.

## `FrTextParams` layout

`FrTextParams` is a transient copied parameter block, not an owning object. The promoted managed view is
`Gw2.Contracts/UI/Frames/FrTextParams.cs`.

| Offset | Type | Evidence-backed role |
|---:|---|---|
| `+0x00` | `uint32` | flags tested by `FrText`; bits `0x1`, `0x2`, `0x4`, and `0x8` gate bounds, character count, line data, and custom material pointers respectively. |
| `+0x04` | `uint32` | present in the copied block; not read by the recovered `FrText` code. |
| `+0x08` | pointer | four-float bounds output when flag `0x1` is set. |
| `+0x10` | pointer | character-count output when flag `0x2` is set. |
| `+0x18` | pointer | line-data array when flag `0x4` is set. |
| `+0x20` | pointer | borrowed material byte data when flag `0x8` is set. |
| `+0x28` | `uint32` | material byte count; required with flag `0x8`. |
| `+0x2C` | `uint32` | not read by the recovered layout code. |
| `+0x30` | pointer | float2 offset used when text flags include `0x10`. |
| `+0x38` | `float` | when parameter flag `0x20` is set, added font-height scale term: `fontHeight += fontHeight * value`. |
| `+0x3C` | `uint32` | not read by the recovered layout code. |

The copy size in `sub_14106F280` and the caller stack layout both establish a `0x40`-byte block. The
pointer fields are eight-byte aligned; the apparent four-byte gap after flags is therefore part of the
native layout, not a pointer at `+0x04`.

The optional line-data object uses a native dynamic-array header. The managed `FrTextLineData` view
promotes only the fields directly observed by `sub_141071AA0`: records at `+0x08`, capacity at
`+0x10`, and count at `+0x14`. Its `0x20`-byte record contents remain opaque.

## Layout, style, color, alignment, and scale

`sub_141071AA0` performs the reusable layout work:

- style `0xFFFFFFFF` resolves to the current default style; otherwise `sub_1410717F0(styleId)` returns
  a record from the current-build style table;
- the style record supplies an opaque font key at `+0x10`, style flags at `+0x04`, and fallback packed
  color at `+0x08`;
- `sub_1410790F0` asks the global `FontContext` for a `GrFont*`, font height, and font metadata;
- font metadata and style flags are ORed into the layout flags;
- a color with zero alpha is replaced by the style color, and the resulting alpha must be nonzero;
- `sub_1410715C0` calls `sub_140AD6EB0` to measure a line and chooses the line break, including native
  newline/word-boundary behavior;
- text flags control wrapping and line placement. The recovered arithmetic proves the center/right and
  vertical placement branches, but the complete public enum names are not yet recovered;
- parameter flag `0x1` requests bounds, `0x2` requests the consumed character count, `0x4` requests
  line records, and `0x8` selects the custom material-byte glyph path;
- text flags `0x10` normalize tabs to spaces and force a single layout line in the observed code;
- parameter flag `0x20` applies the additional font-height term described in the layout table.

The exact mapping of every text-flag bit to a public control enum remains unresolved. Do not expose
these raw flags as a managed public API yet.

## Font and model ownership

`sub_140A69220` is the `FontContext` cache lookup used through `sub_1410790F0`. It maps the opaque
64-bit font key to a cached font-data entry, loads a `GrFont` when needed, and explicitly addrefs the
returned `GrFont*`. It also returns the resolved height and metadata. `sub_141071AA0` releases that
font reference after all line models have been built. This confirms operation-scoped font ownership;
it does not authorize retaining the pointer in client code.

`sub_140AD6A80` creates the standard glyph model from the borrowed `GrFont*`, UTF-16 span, height,
position, bounds, color, and layout flags. `sub_140AD6BC0` adds a borrowed material-byte pointer and
byte count. The custom material path is not required for the first proof and should remain unused
until its byte-copy behavior is independently confirmed.

The returned glyph models are placed into the current frame's content group by `sub_141074A80`. The
text wrapper does not release those model references after appending them; native frame/content
cleanup therefore owns the models for the frame. This is strongly inferred from the wrapper and the
frame cleanup path, and is sufficient to prohibit client retention across frames.

## GrFont glyph coverage and font registration

The build-205.780 `GrFont.cpp` path separates glyph coverage from text layout. `sub_140AD60F0`
(`RVA 0xAD60F0`) is a per-string metric routine, not a maximum-font or font-registration table. Its
first argument is a `GrFont*`; it walks a UTF-16 span, calls `sub_140AD67F0` for each code unit, and
returns the accumulated metric pair plus the consumed-character count. The effective native shape is:

```text
sub_140AD60F0(
    GrFont* font,
    uint32_t outMetrics[2],
    const uint16_t* begin,
    const uint16_t* end,
    uint32_t widthLimit,
    uint32_t flags,
    int32_t* consumedCharacters)
```

The lookup has several important boundaries:

- U+0020 is handled as a special space case and does not go through the glyph hash table;
- a missing range or glyph takes the fallback metric path through `sub_140C03550`;
- a resolved glyph uses the metric payload at the returned glyph record and the `sub_140C035A0` /
  `sub_140C036E0` helpers;
- the `ch < 0x180` conditional selects a metric-handling branch. It is not a font-count limit;
- the routine consumes `uint16_t` code units directly. Full supplementary-plane shaping is therefore
  not established by adding another BMP range; surrogate-pair handling remains an upstream question.

`sub_140AD67F0` selects ranges from the `GrFont` object:

| Native location | Promoted view | Evidence-backed role |
|---:|---|---|
| `GrFont +0x10` | `MetricCacheDirty` | Dirty gate that causes `sub_140AD62D0` to rebuild cached metrics. Exact state encoding is unresolved. |
| `GrFont +0x18` | `ActiveRange` | Current range-cache slot. |
| `GrFont +0x20` | `SecondaryRange` | Previous/secondary range-cache slot; the lookup swaps these slots when selecting another range. |
| `GrFont +0x50` | `Ranges` | Native range array. |
| `GrFont +0x5C` | `RangeCount` | Number of `0x70`-byte range records. |

Each `GrFontRange` is `0x70` bytes. `GrFontAddRange` (`RVA 0xAD6900`) copies the
null-terminated UTF-16 range-asset key into the native string/buffer rooted at `+0x10`, stores the
16-bit half-open character bounds at `+0x20` and `+0x28`, writes the observed
`GrFontRangeLoadState.Unloaded == 2`, and initializes the inline glyph hash table at `+0x48`.
The key is passed to the native asset opener but is not observed as an operating-system file path.
The earlier `+0x04` filename interpretation was incorrect; that field is no longer promoted.

`GrFontEnsureRangeLoaded` passes `GrFontRange +0x10` directly to the native asset opener. On a
successful load it copies the encoded byte stream into the native byte-buffer rooted at `+0x30`;
the data pointer consumed by the glyph decoder is `+0x38`. The observed load states are
`Loaded == 0`, `Failed == 1`, and `Unloaded == 2`. `+0x24` is refreshed from a global
tick/frame-like counter whenever the range is touched.

`sub_140C04030` resolves a `0x34`-byte `GrFontGlyphRecord`. `DataOffset` is at `+0x00`,
`RangeIndex` at `+0x28`, the exact UTF-16 `CodeUnit` at `+0x2C`, and the hash-chain
`NextIndex` at `+0x30`. These views are recorded in
`Gw2.Contracts/Graphics/GrFont.cs`; `Gw2.Contracts/Graphics/FontContext.cs` also promotes the
fixed registration-range descriptor shape and slot names. All pointers remain native-owned.

`sub_140AD7AA0` (`RVA 0xAD7AA0`) lazy-loads a selected range through the native asset service, parses its
glyph data, marks the `GrFont` cache dirty, and reports `Failed to load font range 0x%x - 0x%x` when the
asset cannot be opened. `sub_140AD62D0` (`RVA 0xAD62D0`) then rebuilds the aggregate metric state.
This means that changing only `sub_140AD60F0` to accept a previously missing code unit would make
measurement disagree with line breaking and glyph-model creation; it would not create drawable glyph
data.

The earlier font-registration path is also identified. The `FontContext` vtable at
`0x141BF8168` has `sub_140A68B80` (`RVA 0xA68B80`) at slot `+0x10` and
`sub_140A69220` (`RVA 0xA69220`) at slot `+0x30`:

- `sub_140A68B80` opens a native resource, validates the `ANTF` magic (`0x544E4641`), parses `0x78`-byte
  records, and registers them through `sub_140A68E40`;
- each parsed record supplies a font key through `sub_140A69910`, thirteen range/file slots, and the
  fixed thirteen-entry character-range descriptor at `RVA 0x01BF8100`. The descriptor entries are
  8-byte pairs of 32-bit half-open bounds; the encoded range-asset codec is recovered below;
- `sub_1410717F0` returns a style-table record whose `+0x10` value is the opaque font key;
  `sub_1410790F0` dispatches that key to `FontContext`, where `sub_140A69220` resolves the cached
  `GrFont*` and operation-scoped font metadata.

### Fixed build-205.780 character-range slots

`sub_140A68B80` passes the static table at `RVA 0x01BF8100` and count `13` into
`sub_140A68E40`. The table is recovered exactly as these half-open BMP/UTF-16 intervals:

| Slot | Promoted description | Range |
|---:|---|---|
| 0 | ASCII graphic characters | `[U+0021, U+007F)` |
| 1 | Latin-1 Supplement | `[U+00A1, U+00FF)` |
| 2 | Latin Extended-A | `[U+0100, U+0180)` |
| 3 | Greek and Cyrillic | `[U+0391, U+0460)` |
| 4 | punctuation and symbols | `[U+2010, U+266B)` |
| 5 | CJK Symbols and Punctuation | `[U+3000, U+3020)` |
| 6 | Hiragana and Katakana | `[U+3041, U+3100)` |
| 7 | bopomofo | `[U+3105, U+312A)` |
| 8 | Hangul compatibility jamo | `[U+3131, U+318F)` |
| 9 | Hangul syllables | `[U+AC00, U+D7A4)` |
| 10 | CJK unified ideographs | `[U+4E00, U+9FA6)` |
| 11 | CJK compatibility ideographs | `[U+F900, U+FA6B)` |
| 12 | Halfwidth and Fullwidth Forms | `[U+FF01, U+FFE7)` |

These are registration slots, not a claim that every face constructs all thirteen concrete
`GrFontRange` objects. In the font-construction path around `RVA 0xA69462`, an empty range-asset key
skips the `GrFontAddRange` call entirely. A missing range asset therefore means no concrete
range and an eventual null lookup/default advance; it cannot by itself produce the visible square.

The table is important for the observed behavior: Greek/Cyrillic and kana are explicit first-class
native font slots, just like Han. Their presence in the table rules out a generic "engine only knows
Latin and Han" limitation.

### Recovered per-glyph RLE stream

Ghidra decompilation of `FntRle.cpp` (`RVA 0xC21010` through `0xC21270`) recovers the range
asset's per-glyph stream format. Each glyph begins with four bytes: an observed-but-unresolved
header byte, `width - 1`, `height - 1`, and an RLE type. The decoder then emits exactly
`width * height` 8-bit coverage values, so the next byte after the final span is the next glyph.

- Type `1` stores the coverage value directly. Intermediate values other than `0x00` and `0xFF`
  represent one pixel; `0x00` and `0xFF` are followed by a variable-length span count.
- Types `0` and `0xFF` alternate zero/full-coverage spans, with the type selecting the first value.
- A span count below `0xFF` represents `count + 1` pixels. `0xFF` is an escape for a 256-pixel
  base plus continuation bytes; an exact 256-pixel span is therefore `FF 00`.

`GrFontEnsureRangeLoaded` uses the stream boundaries to create records, while
`GrFontBuildGlyphMetrics` (`RVA 0xC039E0`) decodes the same coverage to rebuild ink bounds and
derived metric fields. The managed offline model and round-trip/live-prefix tests are in
`Gw2.Contracts/Graphics/GrFontRle.cs` and `tests/Gw2.Contracts.Tests/GrFontRleTests.cs`.

### Direct UTF-16 span preservation

The reusable FrText path does not create a sanitized character buffer before GrFont lookup.
`sub_141071AA0` and `sub_1410715C0` retain line spans as pointers/counts into the original UTF-16
input and pass those same spans into the GrFont measurement/model builders. The direct client-owned
draw wrapper likewise supplies its caller-owned UTF-16 buffer synchronously.

Consequently, at this layer `U+0416`, `U+03A9`, `U+3042`, and `U+30AB` reach
`GrFontFindGlyph` as those exact code units; FrText does not rewrite them to `U+25A1`.

### Static and live behavior for unsupported UTF-16 code units

The combined static control flow distinguishes the null-glyph blank path from non-null encoded glyph
coverage:

1. `GrFontFindGlyph` performs exact range selection and exact hash lookup by the original 16-bit
   code unit. It has no fallback-to-square branch.
2. A null lookup in both `GrFontMeasureText` and `GrFontRasterizeText` uses
   `GrFont.DefaultAdvance`; the null branch does not call the glyph coverage rasterizer.
3. When a range asset is present, `GrFontEnsureRangeLoaded` starts at `StartCodeUnit`, parses one
   self-delimiting encoded glyph/RLE blob, inserts that record under the current exact code unit, increments the code
   unit, and repeats until the encoded byte stream is exhausted. This is a sequential physical
   glyph stream, not a sparse cmap or character-alias table.
4. `GrFontBuildGlyphMetrics` has a genuine empty/no-coverage path, proving that an absent visual
   glyph can be represented as blank. The decoder does not inherently turn empty data into tofu.
5. `GrFontRasterizeText` resolves the record's `RangeIndex` and passes
   `EncodedGlyphData + DataOffset` into `GrFontRasterizeGlyphCoverage`. After lookup, the
   rasterizer does not need the original codepoint; it only decodes the selected record's coverage.
6. `GrFontRasterizeGlyphCoverage` composites decoded 8-bit coverage into the destination text
   bitmap with a per-pixel maximum operation. No later character-aware substitution exists in this
   recovered layer.

The live build-205.780 result now narrows the behavior:

- `U+0416`, `U+03A9`, `U+3042`, and `U+30AB` reached `GrFontFindGlyph` unchanged, returned
  `glyph=<null>`, and reported `default-advance=4`. The screenshot showed the resulting blank
  spaces, not square placeholders.
- `U+6F22` resolved to a non-null record in the CJK range `[U+4E00,U+9FA6)`.
- Literal `U+25A1` resolved to a non-null record in range index `3`, `[U+2010,U+266B)`, and the
  screenshot showed the square glyph. This is an explicit character in the test string, not a
  fallback generated for the other code units.

This confirms that the null-glyph path is advance-only and does not synthesize or rasterize a
placeholder square. The probe records the exact input code unit, returned record code unit, range
index, data offset, and selected range bounds; it does not yet inspect the decoded ink coverage of
an individual non-null record.

The intervention boundary is therefore:

| Goal | Required native path | Unsafe shortcut |
|---|---|---|
| Add characters to an existing face | Add or replace a native range asset, or inject an equivalent range and glyph-record set before `sub_140AD67F0`; preserve metric rebuild, line breaking, and model creation. | Patching `sub_140AD60F0` or only changing fallback widths. |
| Add a new selectable face | Register a new ANTF/font-data record and opaque key, then route a style record to that key. | Treating the display-name strings or a new style ID as sufficient. |

### Synthetic assets for empty fixed ranges

`NativeFontRangeWarmup` is a separate build-205.780 development experiment for capturing the
complete registered range set of each observed 15px or 16px face. When any native glyph lookup
identifies a live supported-height `GrFont`, it calls the proven `GrFontFindGlyph` target once with
the start code unit of each unloaded concrete range. This exercises the game's own lazy asset loader;
it does not create, replace, or retain glyph data. The operation is one-shot per observed font pointer
and must still be validated against a fresh live dump before being treated as stable. The 15px path is
important for native in-game chat, which uses the smaller face.

`NativeFontRangeAssets` supplies complete contiguous 15px and 16px payloads for every fixed slot that
is absent from at least one captured face:

| Slot | Range | Synthetic key |
|---:|---|---|
| 3 | `[U+0391,U+0460)` | `gw2reverse/font/greek-cyrillic` |
| 6 | `[U+3041,U+3100)` | `gw2reverse/font/hiragana-katakana` |
| 7 | `[U+3105,U+312A)` | `gw2reverse/font/bopomofo` |
| 12 | `[U+FF01,U+FFE7)` | `gw2reverse/font/halfwidth-fullwidth` |

At initialization it rasterizes these ranges with installed system fonts. Unavailable or unassigned
code units receive an explicit empty 1x1 coverage record so the native sequential parser retains one
record per code unit. On the first null lookup in an extendable range, the bridge first scans the
current `GrFont`: if a concrete native range already exists, native behavior remains authoritative.
Otherwise it calls `GrFontAddRange` with the matching opaque synthetic key, copies the complete stream
through the native byte-buffer helper, creates every record with the native glyph parser/hash inserter,
marks the range loaded, and invokes the native metric rebuild. Any failure preserves the original null
lookup. A successful bridge emits a `native-font-range-asset` diagnostic record.

All four synthetic paths are live-validated together in native 15px chat using Greek/Cyrillic
`΁E΁E΁EΩ` / `ЁEЁEЁEЁEЯ`, kana `ぁEア ぁEカ`, Bopomofo `㄁E㄁E㄁E㄁E, and fullwidth
`�E�E�E� �E�E�E� �E�E�E�E�E�E�E�`. Fresh same-run diagnostics recorded `loaded-synthetic` for keys
`gw2reverse/font/greek-cyrillic`, `gw2reverse/font/hiragana-katakana`,
`gw2reverse/font/bopomofo`, and `gw2reverse/font/halfwidth-fullwidth`; subsequent lookups returned
the exact `U+03A9`, `U+0416`, `U+3042`, and `U+30AB` native records from the new concrete ranges.

The initial blank result was traced to an incorrect 16-byte managed `GLYPHMETRICS` view; Win32 uses a
20-byte structure because `gmptGlyphOrigin` is two 32-bit `LONG` values. After correcting that ABI and
removing an incorrect bitmap-row reversal, glyph identity and orientation rendered correctly. Slots
3, 6, 7, and 12 now have live visual confirmation.

This implementation does **not** replace empty keys inside parsed ANTF records or teach the native
asset service to open the synthetic keys before `GrFontEnsureRangeLoaded`. Payload resolution occurs
at the post-lookup bridge, keeping the unresolved asset-opener ABI out of the patch while preserving
the game's range ownership, parser, glyph map, metric rebuild, and rasterizer.

The range extension is build-gated and registered in the normal patch catalog. It remains isolated
from range warmup, and existing native ranges always win.
The range warmup is also development-only: it intentionally causes the native loader to populate
existing range caches, but does not replace font data. A fresh post-warmup dump remains the acceptance
check for both the target font pointer and the loaded-range set.


### Promoted GrFont routine names

The static trace now supports more specific semantic names for the shared lookup, measurement, and
coverage-rasterization path. These names document recovered behavior only; no runtime call sites or
hooks use them yet.

| RVA | Promoted semantic name | Evidence-backed behavior |
|---:|---|---|
| `0x00AD60F0` | `GrFontMeasureText` | Walks UTF-16 code units, calls exact glyph lookup, accumulates glyph metrics, and uses `GrFont.DefaultAdvance` when lookup returns null. |
| `0x00AD62D0` | `GrFontRebuildMetrics` | Rebuilds derived glyph/font metric state while `MetricCacheDirty` is set. |
| `0x00AD67F0` | `GrFontFindGlyph` | Checks two cached `GrFontRange*` values, scans the `0x70`-byte range array, lazy-loads the matching range, and performs exact UTF-16 glyph lookup. It has no placeholder-substitution branch. |
| `0x00AD6900` | `GrFontAddRange` | Constructs one concrete range only for a non-empty range asset key, copies that UTF-16 key at `+0x10`, stores the 16-bit bounds, and initializes the glyph map. |
| `0x00AD7AA0` | `GrFontEnsureRangeLoaded` | Opens `RangeAssetKey`, copies its encoded data, creates one glyph record per sequential UTF-16 code unit, and marks the font metric cache dirty. |
| `0x00C04030` | `GrFontGlyphMapFind` | Hash lookup in the range's contiguous `0x34`-byte glyph-record array using `CodeUnit` and `NextIndex`. |
| `0x00C039E0` | `GrFontBuildGlyphMetrics` | Initializes a decoder at `EncodedGlyphData + DataOffset` and derives the glyph metric payload. |
| `0x00C03820` | `GrFontScanGlyphCoverageBounds` | Scans decoded glyph coverage while deriving non-empty ink bounds. |
| `0x00AD7680` | `GrFontRasterizeText` | Rendering-side UTF-16 walker; uses the same glyph lookup, resolves `RangeIndex`, and passes the owning range's encoded glyph data to the glyph coverage rasterizer. |
| `0x00C03AB0` | `GrFontRasterizeGlyphCoverage` | Decodes one glyph's coverage and composites 8-bit values into the destination surface with a per-pixel max operation. |

The corresponding passive views remain in `Gw2.Contracts/Graphics/GrFont.cs`. In addition to the
previously promoted range/cache fields, the current build directly supports:

- `GrFont +0x2C -> Height`;
- `GrFont +0x38 -> Spacing38`, a spacing/tracking-related input whose exact public semantic remains
  unresolved;
- `GrFont +0x3C -> DefaultAdvance`, used on a null glyph lookup;
- `GrFontRange +0x10 -> RangeAssetKey`, the copied null-terminated UTF-16 asset key passed directly to the lazy-loader's native asset opener;
- `GrFontRange +0x20/+0x28` are 16-bit UTF-16 half-open range bounds;
- `GrFontRange +0x38 -> EncodedGlyphData`;
- `GrFontGlyphRecord +0x00 -> DataOffset`;
- `GrFontGlyphRecord +0x28 -> RangeIndex`;
- `GrFontGlyphRecord +0x2C -> CodeUnit`;
- `GrFontGlyphRecord +0x30 -> NextIndex`.

The loader starts at `StartCodeUnit`, increments the code unit once per parsed encoded glyph, and
asserts that the running code unit remains below `TermCodeUnit`. This makes the native range asset a
contiguous BMP/UTF-16 code-unit interval rather than an arbitrary cmap at this layer.

The render walker at `0xAD7680` does not consume persistent atlas UV/page metadata from the glyph
record. It selects the owning range with `RangeIndex`, reads `EncodedGlyphData`, and
`GrFontRasterizeGlyphCoverage` decodes coverage into the destination text bitmap. The recovered
compositor writes `max(existingCoverage, decodedCoverage)` per pixel. Therefore the recovered layer
is better described as encoded per-glyph coverage/rasterization than as a conventional GPU font atlas.

A genuinely null `GrFontFindGlyph` result takes the default-advance branch in both measurement and
rasterization and produces a blank gap; it cannot produce a fallback square. Combined with exact
UTF-16 pointer preservation, sequential exact range-record construction, and the codec's explicit
empty-glyph path, a visible square must come from a non-null record's encoded coverage or from a
different text path. The live test's explicit `U+25A1` record is evidence for the former character,
not for fallback substitution.

The concrete ANTF/range asset names have not yet been captured live and no font resource or runtime
object has been modified. The development probe has now confirmed the concrete lookup records and
data offsets for the seven controlled characters; it remains observation-only and does not authorize
a fallback implementation.

The UTF-16 text, `FrTextParams`, bounds/line-data outputs, offset pointer, and custom material bytes
must all remain valid for the duration of the synchronous call only. The native raw-text setter copies
its input into `CtlText` storage; this does not make the FrApi draw wrapper asynchronous-safe for an
client caller.

## Native coordinate and root-frame validation

The first broad C# text test exposed two independent coordinate facts. `sub_141071AA0` starts a
laid-out line from the rectangle's upper Y edge and subtracts the resolved font height before each
following line. Native text therefore uses a bottom-origin vertical rectangle for this call context;
the managed layout uses top-origin screen coordinates. This is why the same sequence
appeared bottom-to-top when submitted without conversion.

The broad test also proved that the observed `sub_14106A400` frame is not a safe client root. In the
live session the first callback commonly carried frame `0x2A`; the current value-only `FrFrame` read
for that id was approximately `(1117.5,42.0)-(1222.5,147.0)`. The independently recovered root
relation `sub_141086220` returns `DAT_142893CB0 - 0x60`, and the same pointer matched the frame-table
entry for id `1`; its current rectangle was `(0,0)-(1920,1080)`. Hovering a native button changed
which child content appeared first, explaining the user-visible change in placement/stability.

The runtime resolves that root id by value-only reads of the build-local frame manager at
`0x142893C10` (`+0x08` object array, `+0x14` count) and root-table head at `0x142893CB0`, then reads
only the root `FrFrame` screen rectangle for coordinate conversion. It submits client text using the
current native content callback's frame id, not the separately resolved root id. If that callback is
already the root frame, text is skipped fail-closed because a late root append is unsafe. A failed
root resolution also drops client text rather than using an arbitrary child. The pointer is used only
for the synchronous read and is never retained; the submitted native text still owns no frame, font,
model, or material pointer.

### Rejected root pre-traversal experiment

The inner `sub_141075FC0` entry was initially treated as a universal pre-traversal seam. That was too
strong: its body only calls the root `sub_141075E10` traversal when the global scale pair changes;
otherwise it drains a pending list. Injecting text at that entry therefore did not guarantee that
the newly appended root models passed through `sub_141075CE0` and `sub_140A85050`.

Live validation with the C# UI enabled displayed the injected labels, then GW2 generated `Crash.dmp`.
The dump's exception context resolves to `sub_1409DDA80`, with the error arguments identifying:

```text
GrModel.cpp
m_frustum
line 0x9B4 (2484)
```

This is direct evidence that the injected model reached a later consumer without `m_frustum`, not a
generic process or managed exception. The phase hook remains observation-only. Native text submission
has been moved back to the live `sub_14106A400` callback, where the current callback frame is already
the native content context. Root-frame insertion is explicitly refused there until a traversal
boundary that guarantees `sub_140A85050` has been live-confirmed.

## Live validation

Read-only tracepoints were installed temporarily against the running build-205.780 process and
restored after each capture:

| Boundary | Live result |
|---|---|
| `sub_14106F090` (`RVA 0x106F090`) | 20 hits on thread `15592`, all from caller RVA `0x9563DF`, confirming the native pre-traversal phase is active on the same thread as the UI text work. |
| `sub_14106F280` (`RVA 0x106F280`) | 30 hits on thread `15592`; callers included RVAs `0x10454E5` and `0x10418C6`, consistent with live native text-control measurement. |
| `sub_141071AA0` (`RVA 0x1071AA0`) | 20 hits on thread `15592`; callers were `0x106B091` and `0x106F38F`, the return sites of the draw and measurement wrappers. |
| `sub_140AD6A80` (`RVA 0xAD6A80`) | 20 hits on thread `15592`, all from caller RVA `0x10724BA` inside `sub_141071AA0`. |
| `sub_140AD6EB0` (`RVA 0xAD6EB0`) | 20 hits on thread `15592`, caller RVA `0x107166C`, confirming the native font metric helper is active. |
| `sub_141074A80` (`RVA 0x1074A80`) | 20 hits on thread `15592`, caller RVA `0x106B0C1`; observed model count was `1` in the capture. |
| `sub_140A69220` (`RVA 0xA69220`) | 20 hits on thread `15592`; the stable `FontContext` object was observed at runtime, with opaque font-key arguments. |

These captures confirm execution of the native text path and its thread/phase relationship. They do not
prove that an arbitrary client call to `sub_14106AF90` is legal from the current
`sub_14106A400` observation callback, nor do they prove the long-term callback candidate's detour ABI.

## Live boundary confirmation in the current session

The following observation-only traces were run against build `205.780`, process PID `13696`, while
the game was in a normal world frame. They are historical evidence; the shipped runtime does not
expose a diagnostic/proof settings switch.
The process remained responsive throughout; no native arguments were modified and no native pointer
was retained.

The trace at `sub_14106AF90` produced 30 hits on UI thread `3048`. The dominant native caller was
`caller-rva=0x0104173F`, the return site after `sub_141041550`; the leading registers matched the
recovered draw boundary: `RCX=frameId`, `RDX=text`, `R8=textCount`, and `R9=rect`. The remaining
scalar arguments were present in the six captured stack slots. A captured text pointer decoded to a
real UTF-16 label beginning with `[17:`, matching the draw-observer previews rather than an
unrelated pointer. The live entry byte was `E9` because the draw observer was already
installed; this is the observer patch, not evidence that the build-local native function starts with
an unconditional jump.

The trace at `sub_141071AA0` produced 30 hits on the same UI thread. Its callers were
`caller-rva=0x0106F38F` (the measurement wrapper) and `caller-rva=0x0106B091` (the draw wrapper).
The calls carried a text pointer and count in the same positions as the static `FrText.cpp` body,
confirming that both wrappers converge on the shared layout routine before glyph model creation.

The trace at `sub_141074A80` produced two hits in the same sample. Both returned to
`caller-rva=0x0106B0C1`, the call site inside `sub_14106AF90`; `RDX=1` was the generated-model count
and `R8` was the temporary model-array pointer. This is live evidence that the native draw wrapper
appends at least one `GrModel*` for a visible text run through the current frame content.

The traversal/order pair was also traced separately in the same running process. `sub_141075CE0`
produced 20 hits on thread `3048`, all from `caller-rva=0x010763FC`. `sub_140A85050` produced 30
hits on that thread, all from `caller-rva=0x01075DAC`, the static call site inside
`sub_141075CE0`. Together with the static body, this confirms that traversal calls the frustum
assignment helper for content models. It does not yet constitute a single-event timestamp ordering
trace between a client append and that assignment, so the long-term callback ABI remains unconfirmed.

The root-frame proof crash is therefore classified as an ordering/lifetime failure: the client model
was appended without a guaranteed subsequent traversal that assigns its frustum. The later cache
render reached `sub_140A883D0`, which asserted `GrModel + 0x40` (`m_frustum`) was null. The accepted
text arguments and the successful child-frame proof show that this was not evidence against the
recovered text ABI. The active safe route is the observed `sub_14106A400` callback with its current
non-root frame id; root insertion is refused. The phase hook remains observation-only until a direct
pre-traversal boundary and its insertion-to-frustum order are live-confirmed.

## Staged measurement seam

The repository now contains a disabled probe for the recovered `sub_14106F280` measurement
boundary. It is initialized only for build `205.780`, reads the live entry bytes at RVA
`0x0106F280`, and requires the exact current prologue:

```text
48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57
48 81 EC A0 00 00 00 41 8B E9 49 8B D8 8B F2 48 8B F9
```

The candidate measurement scaffold uses the recovered nine-argument ABI and the promoted
`FrTextParams` layout, but remains gated by a compile-time-disabled switch and an independently set
native phase confirmation. Internal diagnostics install separate observation-only patches
around `sub_14106F090`, `sub_141075FC0`, and the two text wrappers; those callbacks record ordering
but do not invoke measurement or alter native arguments. Startup validation therefore cannot call the
measurement function or affect the working rectangle renderer. All text, bounds, output, and
parameter storage in the future measurement call are stack-owned and synchronous; no native pointer
is retained.

The measurement and phase observers are internal diagnostics only. They are not selected by a
settings file or module option, so startup cannot enable measurement or temporary proof calls.

## Historical native draw proof

The first reusable native text boundary was validated with a temporary proof call. That proof is
retired from the runtime and is retained here only as build-local evidence. Its original guards were:

- build `205.780` is active;
- the complete `sub_14106AF90` entry signature at `RVA 0x0106AF90` matches;
- the native frame-content and recurring game-thread patches are active; and
- the draw and pre-traversal phase observation patches are active.

The retired validation probe invoked the call once per native UI generation from the inner native
phase entry, before the `FrContent` traversal that assigns `GrModel::m_frustum`. The host callback ran
at that same point to build the managed UI queue. The current renderer no longer emits this fixed
probe call.

```text
sub_14106AF90(
    frameId,
    L"GW2 NATIVE TEXT TEST",
    20,
    { 100.0, 100.0, 700.0, 140.0 },
    0, 0xFFFFFFFF, 0, 0xFFFFFFFF,
    nullptr,
    0);
```

The text and rectangle are stack-owned and valid only during the synchronous call. `params` is null,
matching a statically recovered native caller, so the proof does not construct `FrTextParams`, supply
custom material bytes, or retain a font/model/frame pointer. The native wrapper creates temporary
glyph models and appends them to the current frame content group. The attempt is capped at 256
submissions per runtime configuration and logs `proof-requested`, `proof`, and `proof-submit` records
to `%LOCALAPPDATA%\\GW2Reverse\\native-text-probe.log`. While the ordinary draw stream has reached
its 128-record diagnostic cap, proof calls are separately observable for the first 16 entries as
`proof-draw-entry` records.

The first acceptance criterion is a stable `proof-submit` followed by a draw-observer record with
`text-preview="GW2 NATIVE TEXT TEST"`, and visible native text at the proof rectangle. A crash,
black-screen flicker, missing draw record, or no visible text failed the proof. This fixed proof
produced visible text in the
normal game world without instability. Its raw rectangle did not yet map one-to-one to screen pixels,
so the native transform convention remains unresolved. It does not enable native measurement.

## managed native text UI route

The managed native GUI route is active whenever its validated build and phase guards are available.
The managed text route keeps the existing C# layout and queues each non-empty line for the next
validated content callback. The submission path runs the host callback and then calls the
recovered `sub_14106AF90` wrapper synchronously before native content traversal. The queue is copied
and cleared before invoking native code; no UTF-16, frame, font, model, or material pointer is
retained across the call. If the native route is unavailable, text is dropped for that frame.

The outer `sub_141075FC0` observation entry owns the renderer generation. The generic game-thread
dispatcher is intentionally not used as a frame boundary: in a normal world it can run more than
once during a single outer/inner UI traversal, which caused the full client text set to be appended
repeatedly to that traversal and presented as flickering duplicates. The outer entry resets the
once-per-traversal guards before any inner text/content submissions begin.

Generation ownership alone is insufficient because the first child content submission is unstable:
hovering a native GW2 control can make its tooltip child arrive first. The runtime therefore pins the
first validated non-root submission frame and waits for that same frame in subsequent traversals
instead of migrating client text between child queues. After eight complete traversals without the
pinned frame, the anchor is discarded and reacquired; this bounded fallback covers map/root lifecycle
changes without retaining a native frame pointer.

The route currently supplies the managed layout rectangle, packed color, default style (`0xFFFFFFFF`),
and zero text/layout flags. It deliberately does not claim native measurement, native font-scale,
alignment, clipping, or wrapping semantics yet. The managed width estimate and line height are only
used to provide a positive native rectangle. This keeps the experiment useful while isolating the
remaining coordinate and layout work.

Successful activation logs `native-ui=enabled`. Native UI text calls are reported separately as
`native-ui-draw-entry`.
If the native route is unavailable or unstable on a new build, the build/signature guard disables
submission for that frame while preserving the original native payload.

Historical diagnostic traces contain the first observation events,
for example `boundary=outer-entry`, `boundary=inner-entry outer-pending=yes`, and
`boundary=inner-exit outer-pending=yes`. The expected order is outer entry, inner entry, inner exit.
The observer is still considered unconfirmed until this ordering is seen in the running process
without instability and the surrounding frame relationship is understood.

## Measurement ABI observation

Diagnostic mode also installs an independent entry observer at
`sub_14106F280 @ RVA 0x0106F280`. The build-local entry guard is the exact five-byte displaced
instruction:

```text
48 89 5C 24 08
```

The surrounding signature includes the recovered prologue through the initial argument copies:

```text
48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57
48 81 EC A0 00 00 00 41 8B E9 49 8B D8 8B F2 48 8B F9
```

The observer does not call or alter the measurement routine. Its trampoline preserves the incoming
volatile GPRs `RAX`, `RCX`, `RDX`, `R8..R11`, `XMM0..XMM5`, and `RFLAGS`, then copies the candidate
candidate nine-argument ABI into a stack-only scratch record before invoking managed diagnostics.
It also copies the original entry return address for caller attribution:

The scratch path uses `pushfq`, an aligned `0x160`-byte local allocation, and `popfq`; this keeps
the Windows x64 call-site alignment correct while preserving the original entry stack before the
native continuation. The original fifth through ninth arguments are copied from the adjusted
entry-stack offsets into the record below.

```text
offset  field
0x00    float* outSize
0x08    uint frameId
0x10    const wchar_t* text
0x18    uint textCount
0x20    const float* bounds
0x28    uint textFlags
0x2C    uint styleId
0x30    uint layoutFlags
0x38    const FrTextParams* params
0x40    return address into caller
```

The three scalar fields at `0x28`, `0x2C`, and `0x30` are copied with 32-bit moves so their
packed record offsets are preserved; pointer fields use 64-bit moves. This distinction is part of
the trampoline contract and is covered by the scratch-record layout test. The record is `0x48`
bytes; the return address is diagnostic metadata, not a retained native pointer.

The callback copies that record synchronously and logs the phase, frame ID, pointer values, text
count, a capped UTF-16 preview, and the scalar flags. The preview and all pointer values are
session-local diagnostics; no native pointer or scratch record is retained. The observation stream
is capped at 128 entries and logs `snapshot=unreadable` or `text-preview=<unreadable>` on a failed
safe read. Each readable record includes the absolute return address and a module-relative
`caller-rva`, allowing the control-side call site to be identified without dereferencing the
caller. A successful record inside `phase=inner` would correlate native text measurement with the
same FrContent traversal that already carried the live frame-content submissions, but it would
still not authorize a client-owned measurement call.

## Draw ABI observation

Diagnostic mode also installs a separate entry observer at
`sub_14106AF90 @ RVA 0x0106AF90`. Its build-local entry guard is again the five-byte
`48 89 5C 24 08` store, with this surrounding signature:

```text
48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 48 89 7C 24 20
41 56 48 83 EC 70 49 8B E9 45 8B F0 48 8B FA 8B D9 85 C9 75 19
```

The draw trampoline uses `pushfq`, an aligned `0x180`-byte local allocation, and `popfq`. Its
stack-only observation record is:

```text
offset  field
0x00    uint frameId
0x08    const wchar_t* text
0x10    uint textCount
0x18    const float rect[4]
0x20    uint textFlags
0x24    uint styleId
0x28    uint layoutFlags
0x2C    uint packedColor
0x30    const FrTextParams* params
0x38    uint contentGroup
0x40    return address into caller
```

The callback safe-reads the rectangle and capped UTF-16 preview, then logs the scalar style,
layout, color, content-group, phase, and caller RVA. It does not call `sub_14106AF90`, modify its
arguments, or retain any native pointer. This is intended to identify the real `CtlText` draw
callers and compare their arguments with the measurement stream before any client-owned draw call
is considered.

## Live validation after removing `-maploadinfo`

Build 205.780 session PID 11424 was started without `-maploadinfo` after deploying the measurement
caller and draw observers. The process remained responsive and produced 128 stable records in each
of the measurement, draw, and generic frame-submission streams. Draw previews included chat text,
percentages, names, and ordinary world/UI labels; rectangles such as `(0,0,83,16)`, colors such as
`0xFFE95050` and `0xFF50E050`, and style/layout values such as style `0x3`/layout `0x8` were
plausible. The corrected packed scalar decode is therefore live-validated for both wrapper
observers.

All 128 draw records and all 128 generic frame submissions were `phase=inner`, which confirms that
the existing `sub_141075FC0` observation window encloses the native draw/content stream in this
session. The dominant draw caller was `caller-rva=0x0104173F`, the return after the call at
`0x14104173A` in `sub_141041550`; the two `contentGroup=6` records came from the statically
identified `sub_1410562D0` call at `0x1410563A5`. This confirms the primary `CtlText` draw route
without naming the wrapper's unknown control fields more strongly than the evidence supports.

All 128 measurements in this capture were `phase=outside`. The dominant measurement caller was
`caller-rva=0x010418C6`, the return after the call at `0x1410418C1` in `sub_141041770`; the other
observed callers match the statically recovered calls in `sub_1410562D0` and `sub_141044FB0`.
This separates the observed measurement/update timing from the draw/content traversal and means
`sub_141075FC0` is not established as the general native text-measurement boundary. The phase
relationship is now confirmed for draw submission, remains unconfirmed for measurement, and the
native measurement call remains disabled. The validated draw path is used by the Core renderer without
an external configuration gate.

The existing `sub_14106A400` observation callback now adds a second bounded stream without reading
or retaining the native descriptor:

```text
frame-submit=1 frame=0x00000BEB parameters=0x... phase=inner ordering=-1 layer=2
  material=0x... color=0xFFFFFFFF rect=(100,200,300,220) outer-pending=yes thread=5
```

`phase=inner` means the generic frame-content submission was observed while
`sub_141075FC0` was active; `outer` means it occurred after entry to `sub_14106F090` but before
the traversal entry; `outside` means it was not nested in either observed boundary. The frame ID
and descriptor address are session-local correlation values. The snapshot copies only the already
promoted `FrameContentParams` fields at `+0x00` ordering key, `+0x04` layer, `+0x08` material,
`+0x18` packed color, and `+0x1C..+0x2B` rectangle. A failed safe read is logged as
`payload=unreadable`. The logger is capped at 128 frame submission records and does not alter the
native call or retain the descriptor/material pointers.

## Historical live validation of the client-owned draw path

A fresh build-205.780 session after the proof-window extension remained responsive in the normal
world frame. The screen showed `GW2 NATIVE TEXT TEST` over the game world, not only on the loading
screen. The latest log marker reported a matched draw signature and active frame-content,
game-thread, and draw-observation gates.

The temporary validation probe submitted through the recovered ABI 256 times before reaching its
intentional cap. The observer recorded 16 entries, including:

```text
native-ui-draw-entry=1 phase=inner frame=0x00000045
  text-preview="GW2 NATIVE TEXT TEST" rect=(100,100,700,140)
  style=0xFFFFFFFF layout=0x00000000 color=0xFFFFFFFF params=0x0 content-group=0
native-ui-draw-entry=13 phase=inner frame=0x00000006
native-ui-draw-entry=14 phase=inner frame=0x00000066
native-ui-draw-entry=15 phase=inner frame=0x00000076
native-ui-draw-entry=16 phase=inner frame=0x00000BB4
```

The exact return address for the proof records is the managed call site, so its module-relative
`caller-rva` is not meaningful; this is expected and does not change the recovered native ABI.
The repeated `phase=inner` records and visible normal-world output confirm that the direct draw
wrapper was reusable from the current native content callback after loading. They also confirm that
the native wrapper consumes the stack-owned UTF-16 string synchronously. This was the pre-fix
placement; the root-frame variant subsequently asserted because its models were appended after root
frustum assignment.

The raw rectangle was not screen-global when it was submitted to the arbitrary observed child frame:
`(100,100,700,140)` appeared at a different screen position in the submitted screenshot. The broad
test then showed the child-frame and bottom-origin causes of that displacement. The proof cap also
means the fixed text intentionally disappears after 256 submissions; it is not a persistent widget.

## Candidate boundaries and remaining risks

**Confirmed:** the control assignment functions, FrApi draw/measure wrappers, shared layout function,
font metric and glyph-model helpers, and the `FrContent` model-group append function execute in the
current build. The `FrTextParams` offsets above are supported by the `0x40` copy and direct field use.

**Strongly inferred:** native text produces one `GrModel*` per laid-out run/line, the model reference is
owned by the current frame content group after `sub_141074A80`, and font references are operation-scoped.

**Confirmed:** the existing generic frame-content callback is inside the same native draw traversal
in the validated session and accepts the recovered `sub_14106AF90` call with client-owned stack data,
but late root-frame insertion is not safe because the new models may miss frustum assignment.

**Strongly supported by static and observation traces:** the inner phase entry is before the
`sub_141075CE0`/`sub_140A85050` traversal work. The guarded implementation now builds and submits
client text there, while keeping the original frame-content callback for the existing rectangle path.
The new pre-traversal placement still requires live validation after deployment; until that test
passes, no proof/UI gate is required; the renderer remains controlled only by build/signature
validation.

Remaining risks are sustained call rate, the exact CtlText vtable/message ABI for arbitrary
client-created controls, the complete text-flag enum, the safe content-group choice, model cleanup
under all frame modes, custom material copying, and whether the existing generic observation callback
remains suitable for a full client text API without ordering side effects. Root-frame lookup and the
bottom-origin conversion are confirmed for this build/session but remain build-local and require
revalidation after game updates or a different root-frame lifecycle.

## Recommended next implementation step

The next implementation step is to live-test the root-frame and bottom-origin conversion with the
existing C# windows. Confirm that the panel text remains in its managed top-to-bottom positions while
the fixed proof appears at the intended top-origin test rectangle. Then reduce duplicate submissions
by validating the once-per-root-traversal callback boundary around `sub_14106F090` and
`sub_141075FC0`; do not move the implementation there until its ABI/order/lifetime is live-confirmed.
`CalcTextWidth` remains a separate measurement task because its observed timing is outside the draw
traversal.

Do not retain `GrFont*`, `GrModel*`, material data, or frame pointers; do not construct/destroy
`FrFrame`; and do not enable the probe from a DXGI `Present` hook.


## Current managed text-layout guardrails

The active tool GUI no longer assumes that its fallback bitmap metrics are exact native FrText
metrics. `CalcTextWidth` uses a conservative proportional-character estimate rather than a fixed
monospace advance, which improves button sizing, wrapping, centering, and footer placement without
calling the still-unvalidated native measurement ABI.

`PushFontScale` remains part of the module GUI contract, but while native FrText is active Core keeps
the effective layout scale at 1.0 because the recovered draw wrapper does not yet receive a validated
font-scale argument. The bitmap fallback may still honor the requested scale. This is intentional:
silently scaling managed hit/layout geometry while native text remains unscaled would reintroduce
visual/input disagreement.

For scrolled children, the managed renderer intersects rectangle clips through the parent chain and
only queues a native text line when the complete estimated line rectangle is inside the current clip.
That is a fail-closed presentation rule, not a claim that FrText scissor/clipping ownership has been
recovered. Partially clipped lines disappear at the boundary until a native clipping contract is
validated.
