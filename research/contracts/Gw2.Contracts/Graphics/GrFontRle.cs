namespace Gw2.Contracts;

internal readonly record struct GrFontRleGlyph(
    byte HeaderByte,
    byte RleType,
    int Width,
    int Height,
    byte[] Coverage);

internal static class GrFontRle
{
    internal static bool TryDecode(
        ReadOnlySpan<byte> encoded,
        out GrFontRleGlyph glyph,
        out int consumed)
    {
        glyph = default;
        consumed = 0;
        if (encoded.Length < 4)
            return false;

        var width = encoded[1] + 1;
        var height = encoded[2] + 1;
        var pixelCount = checked(width * height);
        var rleType = encoded[3];
        if (rleType is not (0 or 1 or 0xFF))
            return false;

        var coverage = new byte[pixelCount];
        var input = 4;
        var output = 0;
        var currentValue = rleType == 0 ? (byte)0xFF : (byte)0;
        var spansRemaining = 0;

        while (output < pixelCount)
        {
            if (spansRemaining == 0)
            {
                if (input >= encoded.Length)
                    return false;

                if (rleType == 1)
                {
                    currentValue = encoded[input++];
                    if (currentValue is not (0 or 0xFF))
                    {
                        spansRemaining = 1;
                        continue;
                    }
                }
                else
                {
                    currentValue = (byte)~currentValue;
                }

                if (!TryReadSpanLength(encoded, ref input, out spansRemaining))
                    return false;
                if (spansRemaining > pixelCount - output)
                    return false;
            }

            coverage.AsSpan(output, spansRemaining).Fill(currentValue);
            output += spansRemaining;
            spansRemaining = 0;
        }

        glyph = new GrFontRleGlyph(
            encoded[0],
            rleType,
            width,
            height,
            coverage);
        consumed = input;
        return true;
    }

    internal static byte[] Encode(
        ReadOnlySpan<byte> coverage,
        int width,
        int height,
        byte headerByte = 0,
        byte rleType = 1)
    {
        if (width is < 1 or > 256)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height is < 1 or > 256)
            throw new ArgumentOutOfRangeException(nameof(height));
        if (coverage.Length != checked(width * height))
            throw new ArgumentException("coverage length must equal width * height", nameof(coverage));
        if (rleType is not (0 or 1 or 0xFF))
            throw new ArgumentOutOfRangeException(nameof(rleType));

        if (rleType != 1)
        {
            for (var index = 0; index < coverage.Length; index++)
            {
                if (coverage[index] is not (0 or 0xFF))
                    throw new ArgumentException(
                        "RLE types 0 and 0xFF only encode binary coverage",
                        nameof(coverage));
            }
        }

        var result = new List<byte>(4 + coverage.Length);
        result.Add(headerByte);
        result.Add((byte)(width - 1));
        result.Add((byte)(height - 1));
        result.Add(rleType);

        if (rleType == 1)
        {
            var index = 0;
            while (index < coverage.Length)
            {
                var value = coverage[index];
                var runLength = 1;
                if (value is 0 or 0xFF)
                {
                    while (index + runLength < coverage.Length &&
                           coverage[index + runLength] == value)
                    {
                        runLength++;
                    }
                }

                result.Add(value);
                if (value is 0 or 0xFF)
                    AppendSpanLength(result, runLength);
                index += runLength;
            }
        }
        else
        {
            // FntRle.cpp toggles the current value before reading the first
            // alternating span: type 0 starts with zero coverage, while
            // type 0xFF starts with full coverage.
            var value = rleType == 0 ? (byte)0 : (byte)0xFF;
            var index = 0;
            while (index < coverage.Length)
            {
                var runLength = 0;
                while (index + runLength < coverage.Length &&
                       coverage[index + runLength] == value)
                {
                    runLength++;
                }

                if (runLength == 0)
                    throw new ArgumentException(
                        "alternating RLE requires coverage to alternate by run",
                        nameof(coverage));

                AppendSpanLength(result, runLength);
                index += runLength;
                value = (byte)~value;
            }
        }

        return [.. result];
    }

    private static bool TryReadSpanLength(
        ReadOnlySpan<byte> encoded,
        ref int input,
        out int spanLength)
    {
        spanLength = 0;
        if (input >= encoded.Length)
            return false;

        var countByte = encoded[input++];
        spanLength = countByte + 1;
        if (countByte != 0xFF)
            return true;

        spanLength = 0x100;
        while (true)
        {
            if (input >= encoded.Length)
                return false;

            countByte = encoded[input++];
            spanLength = checked(spanLength + countByte);
            if (countByte != 0xFF)
                return true;
        }
    }

    private static void AppendSpanLength(List<byte> result, int spanLength)
    {
        if (spanLength < 1)
            throw new ArgumentOutOfRangeException(nameof(spanLength));

        if (spanLength <= 0x100)
        {
            // 0xFF is an escape, so an exact 256-byte run is encoded as
            // 0xFF, 0x00 rather than a single count byte.
            if (spanLength < 0x100)
            {
                result.Add((byte)(spanLength - 1));
                return;
            }

            result.Add(0xFF);
            result.Add(0);
            return;
        }

        result.Add(0xFF);
        var remaining = spanLength - 0x100;
        while (remaining >= 0xFF)
        {
            result.Add(0xFF);
            remaining -= 0xFF;
        }

        result.Add((byte)remaining);
    }
}
