namespace Gw2.Core.Dat;

public enum Gw2DatRecordKind
{
    Unknown,
    Atoc,
    PackFile,
    ContentStore,
}

/// <summary>
/// Controls a sequential archive scan. The scan is deliberately metadata-first:
/// it captures only a bounded decoded prefix and never writes records to disk.
/// Call <see cref="Gw2DatScanner.DecodeRecord"/> when a full payload is needed.
/// </summary>
public readonly record struct Gw2DatScanOptions(
    uint StartRecord = 0,
    uint? RecordCount = null,
    int PrefixBytes = 64,
    bool ContinueOnError = true)
{
    public void Validate(uint archiveEntryCount)
    {
        if (StartRecord >= archiveEntryCount && archiveEntryCount != 0)
            throw new ArgumentOutOfRangeException(nameof(StartRecord));
        if (PrefixBytes is < 0 or > 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(PrefixBytes));
    }
}

public sealed record Gw2DatRecordScan(
    uint RecordIndex,
    Gw2DatMftEntry Entry,
    Gw2DatRecordKind Kind,
    long DecodedSize,
    byte[] Prefix,
    Exception? Error);

/// <summary>
/// Generic, read-only scanner over the archive's MFT records.
/// </summary>
public sealed class Gw2DatScanner
{
    private readonly Gw2DatArchive _archive;

    public Gw2DatScanner(Gw2DatArchive archive)
    {
        _archive = archive ?? throw new ArgumentNullException(nameof(archive));
    }

    public IEnumerable<Gw2DatRecordScan> ScanRecords() =>
        ScanRecords(new Gw2DatScanOptions(PrefixBytes: 64, ContinueOnError: true));

    public IEnumerable<Gw2DatRecordScan> ScanRecords(Gw2DatScanOptions options)
    {
        options.Validate(_archive.MftEntryCount);

        var start = options.StartRecord;
        var end = options.RecordCount is null
            ? _archive.MftEntryCount
            : Math.Min(
                (long)_archive.MftEntryCount,
                (long)start + options.RecordCount.Value);

        for (var record = (long)start; record < end; record++)
        {
            var recordIndex = checked((uint)record);
            var entry = _archive.ReadMftEntry(recordIndex);
            using var capture = new PrefixCaptureStream(options.PrefixBytes);
            Gw2DatRecordScan result;

            try
            {
                _archive.DecodeEntry(recordIndex, capture);
                result = new(
                    recordIndex,
                    entry,
                    Classify(recordIndex, capture.Prefix),
                    capture.DecodedSize,
                    capture.Prefix,
                    null);
            }
            catch (Exception ex) when (options.ContinueOnError)
            {
                result = new(recordIndex, entry, Gw2DatRecordKind.Unknown, capture.DecodedSize, capture.Prefix, ex);
            }

            yield return result;
        }
    }

    /// <summary>
    /// Decodes one record directly to the caller's stream.
    /// </summary>
    public void DecodeRecord(uint recordIndex, Stream destination) =>
        _archive.DecodeEntry(recordIndex, destination);

    /// <summary>
    /// Decodes one record to a byte array. Prefer <see cref="DecodeRecord"/>
    /// for large records or when a downstream parser can consume a stream.
    /// </summary>
    public byte[] ReadDecodedRecord(uint recordIndex)
    {
        using var output = new MemoryStream();
        DecodeRecord(recordIndex, output);
        return output.ToArray();
    }

    private static Gw2DatRecordKind Classify(uint recordIndex, ReadOnlySpan<byte> prefix)
    {
        if (recordIndex == 1)
            return Gw2DatRecordKind.Atoc;
        if (prefix.Length >= 12 && prefix[..2].SequenceEqual("PF"u8) && prefix[8..12].SequenceEqual("cntc"u8))
            return Gw2DatRecordKind.ContentStore;
        if (prefix.Length >= 2 && prefix[..2].SequenceEqual("PF"u8))
            return Gw2DatRecordKind.PackFile;
        return Gw2DatRecordKind.Unknown;
    }

    private sealed class PrefixCaptureStream : Stream
    {
        private readonly byte[] _prefix;
        private int _prefixLength;

        public PrefixCaptureStream(int prefixBytes) => _prefix = new byte[prefixBytes];

        public long DecodedSize { get; private set; }
        public byte[] Prefix => _prefix[.._prefixLength];

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => DecodedSize;
        public override long Position
        {
            get => DecodedSize;
            set => throw new NotSupportedException();
        }

        public override void Flush() { }
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            DecodedSize = checked(DecodedSize + buffer.Length);
            var count = Math.Min(buffer.Length, _prefix.Length - _prefixLength);
            if (count == 0)
                return;

            buffer[..count].CopyTo(_prefix.AsSpan(_prefixLength));
            _prefixLength += count;
        }
    }
}
