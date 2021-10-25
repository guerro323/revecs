using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace revecs.Utility;

public static class SpanUtility
{
    public static Span<T> ToSpan<T>(this ref T t)
        where T : struct
    {
        return MemoryMarshal.CreateSpan(ref t, 1);
    }

    // basically code from MemoryMarshal but without constraints
    public static Span<TTo> UnsafeCast<TFrom, TTo>(this Span<TFrom> span)
    {
        // Use unsigned integers - unsigned division by constant (especially by power of 2)
        // and checked casts are faster and smaller.
        uint fromSize = (uint)Unsafe.SizeOf<TFrom>();
        uint toSize = (uint)Unsafe.SizeOf<TTo>();
        uint fromLength = (uint)span.Length;
        int toLength;
        if (fromSize == toSize)
        {
            // Special case for same size types - `(ulong)fromLength * (ulong)fromSize / (ulong)toSize`
            // should be optimized to just `length` but the JIT doesn't do that today.
            toLength = (int)fromLength;
        }
        else if (fromSize == 1)
        {
            // Special case for byte sized TFrom - `(ulong)fromLength * (ulong)fromSize / (ulong)toSize`
            // becomes `(ulong)fromLength / (ulong)toSize` but the JIT can't narrow it down to `int`
            // and can't eliminate the checked cast. This also avoids a 32 bit specific issue,
            // the JIT can't eliminate long multiply by 1.
            toLength = (int)(fromLength / toSize);
        }
        else
        {
            // Ensure that casts are done in such a way that the JIT is able to "see"
            // the uint->ulong casts and the multiply together so that on 32 bit targets
            // 32x32to64 multiplication is used.
            ulong toLengthUInt64 = (ulong)fromLength * (ulong)fromSize / (ulong)toSize;
            toLength = checked((int)toLengthUInt64);
        }

        return MemoryMarshal.CreateSpan(
            ref Unsafe.As<TFrom, TTo>(ref MemoryMarshal.GetReference(span)),
            toLength
        );
    }
}