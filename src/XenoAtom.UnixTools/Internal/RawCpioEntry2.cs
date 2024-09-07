// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace XenoAtom.UnixTools;

internal struct RawCpioEntry2
{
    public uint c_magic;
    // c_ino	8	Inode number (ignored when extracting, set to 0).
    public uint c_ino;
    // c_mode	8	File mode and permissions.
    public uint c_mode;
    // c_uid	8	User ID of the file owner.
    public uint c_uid;
    // c_gid	8	Group ID of the file owner.
    public uint c_gid;
    // c_nlink	8	Number of hard links.
    public uint c_nlink;
    // c_mtime	8	Modification time of the file (in seconds since epoch).
    public uint c_mtime;
    // c_filesize	8	Size of the file in bytes. For directories, this is 0.
    public uint c_filesize;
    // c_devmajor	8	Major number of the device (for special files).
    public uint c_devmajor;
    // c_devminor	8	Minor number of the device (for special files).
    public uint c_devminor;
    // c_rdevmajor	8	Major number of the device for the file this entry represents (for special files).
    public uint c_rdevmajor;
    // c_rdevminor	8	Minor number of the device for the file this entry represents (for special files).
    public uint c_rdevminor;
    // c_namesize	8	Length of the file name including the trailing null byte.
    public uint c_namesize;
    // c_check	8	Checksum (always 00000000 in the "newc" format).
    public uint c_check;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ConvertTo(out RawCpioEntry entry)
    {
        if (HexHelper.IsVectorizedVersionSupported)
        {
            ConvertToVectorized(out entry);
        }
        else
        {
            ConvertToScalar(out entry);
        }
    }

    public void ConvertToScalar(out RawCpioEntry entry)
    {
        ref var srcSpan = ref Unsafe.As<RawCpioEntry2, uint>(ref Unsafe.AsRef(in this));
        Unsafe.SkipInit(out entry);
        ref var destSpan = ref Unsafe.As<RawCpioEntry, ulong>(ref entry);

        HexHelper.ConvertToHex(srcSpan, out destSpan);
        HexHelper.ConvertToHex(Unsafe.Add(ref srcSpan, 1), out Unsafe.Add(ref destSpan, 1));
        HexHelper.ConvertToHex(Unsafe.Add(ref srcSpan, 2), out Unsafe.Add(ref destSpan, 2));
        HexHelper.ConvertToHex(Unsafe.Add(ref srcSpan, 3), out Unsafe.Add(ref destSpan, 3));
        HexHelper.ConvertToHex(Unsafe.Add(ref srcSpan, 4), out Unsafe.Add(ref destSpan, 4));
        HexHelper.ConvertToHex(Unsafe.Add(ref srcSpan, 5), out Unsafe.Add(ref destSpan, 5));
        HexHelper.ConvertToHex(Unsafe.Add(ref srcSpan, 6), out Unsafe.Add(ref destSpan, 6));
        HexHelper.ConvertToHex(Unsafe.Add(ref srcSpan, 7), out Unsafe.Add(ref destSpan, 7));
        HexHelper.ConvertToHex(Unsafe.Add(ref srcSpan, 8), out Unsafe.Add(ref destSpan, 8));
        HexHelper.ConvertToHex(Unsafe.Add(ref srcSpan, 9), out Unsafe.Add(ref destSpan, 9));
        HexHelper.ConvertToHex(Unsafe.Add(ref srcSpan, 10), out Unsafe.Add(ref destSpan, 10));
        HexHelper.ConvertToHex(Unsafe.Add(ref srcSpan, 11), out Unsafe.Add(ref destSpan, 11));
        HexHelper.ConvertToHex(Unsafe.Add(ref srcSpan, 12), out Unsafe.Add(ref destSpan, 12));
        HexHelper.ConvertToHex(Unsafe.Add(ref srcSpan, 13), out Unsafe.Add(ref destSpan, 13));
    }

    public void ConvertToVectorized(out RawCpioEntry entry)
    {
        ref var srcSpan = ref Unsafe.As<RawCpioEntry2, ulong>(ref Unsafe.AsRef(in this));
        Unsafe.SkipInit(out entry);
        ref var destSpan = ref Unsafe.As<RawCpioEntry, Vector128<byte>>(ref entry);

        HexHelper.ConvertToHex(srcSpan, out destSpan);
        HexHelper.ConvertToHex(Unsafe.Add(ref srcSpan, 1), out Unsafe.Add(ref destSpan, 1));
        HexHelper.ConvertToHex(Unsafe.Add(ref srcSpan, 2), out Unsafe.Add(ref destSpan, 2));
        HexHelper.ConvertToHex(Unsafe.Add(ref srcSpan, 3), out Unsafe.Add(ref destSpan, 3));
        HexHelper.ConvertToHex(Unsafe.Add(ref srcSpan, 4), out Unsafe.Add(ref destSpan, 4));
        HexHelper.ConvertToHex(Unsafe.Add(ref srcSpan, 5), out Unsafe.Add(ref destSpan, 5));
        HexHelper.ConvertToHex(Unsafe.Add(ref srcSpan, 6), out Unsafe.Add(ref destSpan, 6));
    }
}