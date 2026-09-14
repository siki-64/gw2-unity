namespace Gw2.Native;

// Canonical authored effect identity used by IEffectDef. The two qwords are
// stored exactly as they appear in the native object at +0x00/+0x08.
internal readonly record struct EffectContentKey(ulong Low, ulong High)
{
    private const ulong FnvOffsetBasis = 0xCBF29CE484222325;
    private const ulong FnvPrime = 0x00000100000001B3;

    // VfxDenoiser-compatible FNV-1a over the 16 native key bytes. The 128-bit
    // key remains the canonical identity; this hash is only an index/log value.
    internal ulong GetFnv1a64()
    {
        var hash = FnvOffsetBasis;
        HashQword(ref hash, Low);
        HashQword(ref hash, High);
        return hash;
    }

    internal bool IsEmpty => Low == 0 && High == 0;

    public override string ToString() => $"{High:X16}{Low:X16}";

    private static void HashQword(ref ulong hash, ulong value)
    {
        for (var shift = 0; shift < 64; shift += 8)
        {
            hash ^= (byte)(value >> shift);
            hash *= FnvPrime;
        }
    }
}

internal readonly record struct EffectDefinitionIdentity(
    uint DefinitionId,
    EffectContentKey ContentKey)
{
    internal ulong KeyHash => ContentKey.GetFnv1a64();
}
