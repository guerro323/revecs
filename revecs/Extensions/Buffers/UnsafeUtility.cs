using System.Runtime.CompilerServices;

namespace revecs.Extensions.Buffers;

public static unsafe class UnsafeUtility
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SameData<T>(ref T left, ref T right)
    {
        var size = Unsafe.SizeOf<T>();
        var leftPtr = (byte*) Unsafe.AsPointer(ref left);
        var rightPtr = (byte*) Unsafe.AsPointer(ref right);

        for (var i = 0; i < size; i++)
            if (leftPtr[i] != rightPtr[i])
                return false;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SameData<T>(T left, T right)
    {
        return SameData(ref left, ref right);
    }
}