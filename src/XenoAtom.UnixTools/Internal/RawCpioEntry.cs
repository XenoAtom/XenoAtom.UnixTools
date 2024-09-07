// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace XenoAtom.UnixTools;

internal unsafe struct RawCpioEntry
{
    public const int SizeOf = 110;

    public static ReadOnlySpan<byte> Trailer => "TRAILER!!!"u8;

    // c_magic	6	The magic number, always 070701 for "newc".
    public long c_magic;
    
    // c_ino	8	Inode number (ignored when extracting, set to 0).
    public long c_ino;

    // c_mode	8	File mode and permissions.
    public long c_mode;

    // c_uid	8	User ID of the file owner.
    public long c_uid;

    // c_gid	8	Group ID of the file owner.
    public long c_gid;

    // c_nlink	8	Number of hard links.
    public long c_nlink;

    // c_mtime	8	Modification time of the file (in seconds since epoch).
    public long c_mtime;

    // c_filesize	8	Size of the file in bytes. For directories, this is 0.
    public long c_filesize;

    // c_devmajor	8	Major number of the device (for special files).
    public long c_devmajor;

    // c_devminor	8	Minor number of the device (for special files).
    public long c_devminor;

    // c_rdevmajor	8	Major number of the device for the file this entry represents (for special files).
    public long c_rdevmajor;

    // c_rdevminor	8	Minor number of the device for the file this entry represents (for special files).
    public long c_rdevminor;

    // c_namesize	8	Length of the file name including the trailing null byte.
    public long c_namesize;

    // c_check	8	Checksum (always 00000000 in the "newc" format).
    public long c_check;

    public void ReadFrom(Stream stream)
    {
        // Make sure that before reading we are making the first 2 bytes of c_magic to be ascii `0` (0x30)
        // So that when we convert to hex, we will have a valid hex number
        c_magic = 0x30303030_30303030;
        stream.ReadExactly(AsSpan());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Span<byte> AsSpan()
    {
        var span = MemoryMarshal.CreateSpan(ref Unsafe.As<RawCpioEntry, byte>(ref Unsafe.AsRef(in this)), sizeof(RawCpioEntry)).Slice(2);
        Debug.Assert(span.Length == SizeOf);
        return span;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryConvertTo(out RawCpioEntry2 entry) => HexHelper.IsVectorizedVersionSupported ? TryConvertToVectorized(out entry) : TryConvertToScalar(out entry);
    
    public bool TryConvertToScalar(out RawCpioEntry2 entry)
    {
        ref var srcSpan = ref Unsafe.As<RawCpioEntry, ulong>(ref Unsafe.AsRef(in this));
        Unsafe.SkipInit(out entry);
        ref var destSpan = ref Unsafe.As<RawCpioEntry2, uint>(ref entry);

        var error = !HexHelper.TryParseHex(srcSpan, out destSpan);
        error |= !HexHelper.TryParseHex(Unsafe.Add(ref srcSpan, 1), out Unsafe.Add(ref destSpan, 1));
        error |= !HexHelper.TryParseHex(Unsafe.Add(ref srcSpan, 2), out Unsafe.Add(ref destSpan, 2));
        error |= !HexHelper.TryParseHex(Unsafe.Add(ref srcSpan, 3), out Unsafe.Add(ref destSpan, 3));
        error |= !HexHelper.TryParseHex(Unsafe.Add(ref srcSpan, 4), out Unsafe.Add(ref destSpan, 4));
        error |= !HexHelper.TryParseHex(Unsafe.Add(ref srcSpan, 5), out Unsafe.Add(ref destSpan, 5));
        error |= !HexHelper.TryParseHex(Unsafe.Add(ref srcSpan, 6), out Unsafe.Add(ref destSpan, 6));
        error |= !HexHelper.TryParseHex(Unsafe.Add(ref srcSpan, 7), out Unsafe.Add(ref destSpan, 7));
        error |= !HexHelper.TryParseHex(Unsafe.Add(ref srcSpan, 8), out Unsafe.Add(ref destSpan, 8));
        error |= !HexHelper.TryParseHex(Unsafe.Add(ref srcSpan, 9), out Unsafe.Add(ref destSpan, 9));
        error |= !HexHelper.TryParseHex(Unsafe.Add(ref srcSpan, 10), out Unsafe.Add(ref destSpan, 10));
        error |= !HexHelper.TryParseHex(Unsafe.Add(ref srcSpan, 11), out Unsafe.Add(ref destSpan, 11));
        error |= !HexHelper.TryParseHex(Unsafe.Add(ref srcSpan, 12), out Unsafe.Add(ref destSpan, 12));
        error |= !HexHelper.TryParseHex(Unsafe.Add(ref srcSpan, 13), out Unsafe.Add(ref destSpan, 13));

        return !error;
    }

    public bool TryConvertToVectorized(out RawCpioEntry2 entry)
    {
        ref var srcSpan = ref Unsafe.As<RawCpioEntry, Vector128<byte>>(ref Unsafe.AsRef(in this));
        Unsafe.SkipInit(out entry);
        ref var destSpan = ref Unsafe.As<RawCpioEntry2, ulong>(ref entry);

        var error = !HexHelper.TryParseHex(srcSpan, out destSpan);
        error |= !HexHelper.TryParseHex(Unsafe.Add(ref srcSpan, 1), out Unsafe.Add(ref destSpan, 1));
        error |= !HexHelper.TryParseHex(Unsafe.Add(ref srcSpan, 2), out Unsafe.Add(ref destSpan, 2));
        error |= !HexHelper.TryParseHex(Unsafe.Add(ref srcSpan, 3), out Unsafe.Add(ref destSpan, 3));
        error |= !HexHelper.TryParseHex(Unsafe.Add(ref srcSpan, 4), out Unsafe.Add(ref destSpan, 4));
        error |= !HexHelper.TryParseHex(Unsafe.Add(ref srcSpan, 5), out Unsafe.Add(ref destSpan, 5));
        error |= !HexHelper.TryParseHex(Unsafe.Add(ref srcSpan, 6), out Unsafe.Add(ref destSpan, 6));

        return !error;
    }
}