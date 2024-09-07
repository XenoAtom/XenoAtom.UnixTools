// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace XenoAtom.UnixTools;

internal static class AlignHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AlignUp(int value, int alignment)
    {
        Debug.Assert(BitOperations.IsPow2(alignment));
        return value + alignment - 1 & ~(alignment - 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long AlignUp(long value, int alignment)
    {
        Debug.Assert(BitOperations.IsPow2(alignment));
        return value + alignment - 1 & ~((long)alignment - 1);
    }
}