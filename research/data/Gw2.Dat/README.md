# Reusable GW2 archive decoding

Reference `Gw2.Dat.csproj` from a tool or future client. The library has no project
dependencies, native calls, process attachment, renderer, or game-build gate.
The `Gw2.Dat` namespace matches the assembly name; the upstream source used
`Gw2.Core.Dat`, which implied a parent assembly that is not part of this
repository. Consumers must rebuild after this normalization.

```csharp
using Gw2.Dat;

using var archive = Gw2DatArchive.Open(datPath);
using var asset = new MemoryStream();
archive.DecodeFileId(fileId, asset);
byte[] decoded = asset.ToArray();
if (Gw2CntcPack.LooksLike(decoded))
{
    var content = Gw2CntcPack.Parse(decoded);
    foreach (var root in content.Objects)
        Console.WriteLine($"{root.Type:X}: {root.Offset:X}");
}
```

## Layers and ownership

- `Gw2DatArchive`: read-only file access, header/MFT/ATOC, cached file-ID lookup,
  raw extraction and decoded stream output. Record indices are zero-based;
  ATOC MFT indices are converted once. Positional reads permit concurrent callers
  with separate destination streams. Dispose only after those callers finish.
- `Gw2DatMethod0Decoder`: pure managed byte/span-to-stream routines, including
  Method 0 Huffman/LZ and Method 1 delta decoding. Method 1 requires the matching
  old decoded source supplied by the caller. The legacy class name is retained.
- `Gw2DatScanner`: record classification and bounded prefix capture. It still
  decodes the full record; a bounded prefix does not mean bounded decode work.
- `Gw2CntcPack`: indexed content roots, strings, and fixup queries over decoded
  PF/cntc data. Keep its backing memory unchanged for the lifetime of the view.

Destination streams belong to the caller and remain open. Decode errors can
leave partial output; publish an asset only after decoding succeeds. Compressed
entries currently buffer the raw entry and stripped input; decoded output is
streamed with a bounded LZ history. Use stream output for large assets.

## Coverage and remaining work

This is an archive-decoding foundation, not a complete client asset pipeline.
Supported archive compression flags are 0 (existing byte-for-byte path) and 8;
unknown flags/methods fail explicitly. CRC words are stripped for compressed
records but are **not verified**. The existing uncompressed-record framing
requires further real-archive validation. The file-ID cache is a snapshot:
use a stable archive and reopen after updates.

Content-store parsing does not deserialize every content class or resolve all
cross-file references. Texture/model/map/audio decoders, schema/version dispatch,
asset dependency loading, and renderer-ready resources remain separate work.
Do not interpret an indexed root's estimated byte extent as a recovered class
layout. No proprietary archive fixtures are included.

Validation: `dotnet test data/Gw2.Dat.Tests/Gw2.Dat.Tests.csproj` runs standalone
synthetic archive, compression, and content-store fixtures without the game client.
These tests do not establish compatibility with every record in a real Gw2.dat.
