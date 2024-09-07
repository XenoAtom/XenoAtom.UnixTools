// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace XenoAtom.UnixTools;

internal static class HexHelper
{
    public static bool IsVectorizedVersionSupported
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Ssse3.IsSupported || (AdvSimd.IsSupported && AdvSimd.Arm64.IsSupported);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ConvertToHex(ulong value, out Vector128<byte> vec)
    {
        Vector128<byte> input = Vector128.CreateScalar(value).AsByte();
        Vector128<byte> low = input & Vector128.Create((byte)0x0F);
        Vector128<byte> high = input >>> 4;
        var chars = Vector128.Create("0123456789ABCDEF"u8);
        var lowShort = Vector128.WidenLower(Shuffle(chars, low));
        var highShort = Vector128.WidenLower(Shuffle(chars, high));
        highShort <<= 8;
        vec = Shuffle((highShort | lowShort).AsByte(), Vector128.Create((byte)7, 6, 5, 4, 3, 2, 1, 0, 15, 14, 13, 12, 11, 10, 9, 8));
    }
    
    public static void ConvertToHex(uint value, out ulong hex)
    {
        var lhex = (ulong)ByteToHex((int)((value & 0xF)));
        lhex <<= 8;
        lhex |= (ulong)ByteToHex((int)(value >> 4) & 0xF);
        lhex <<= 8;
        lhex |= (ulong)ByteToHex((int)(value >> 8) & 0xF);
        lhex <<= 8;
        lhex |= (ulong)ByteToHex((int)(value >> 12) & 0xF);
        lhex <<= 8;
        lhex |= (ulong)ByteToHex((int)(value >> 16) & 0xF);
        lhex <<= 8;
        lhex |= (ulong)ByteToHex((int)(value >> 20) & 0xF);
        lhex <<= 8;
        lhex |= (ulong)ByteToHex((int)(value >> 24) & 0xF);
        lhex <<= 8;
        lhex |= (ulong)ByteToHex((int)(value >> 28) & 0xF);
        hex = lhex;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseHex(Vector128<byte> vec, out ulong value)
    {
        // Based on "Algorithm #3" https://github.com/WojciechMula/toys/blob/master/simd-parse-hex/geoff_algorithm.cpp
        // http://0x80.pl/notesen/2022-01-17-validating-hex-parse.html
        // by Geoff Langdale and Wojciech Mula
        // Move digits '0'..'9' into range 0xf6..0xff.
        Vector128<byte> t1 = vec + Vector128.Create((byte)(0xFF - '9'));
        // And then correct the range to 0xf0..0xf9.
        // All other bytes become less than 0xf0.
        Vector128<byte> t2 = SubtractSaturate(t1, Vector128.Create((byte)6));
        // Convert into uppercase 'a'..'f' => 'A'..'F' and
        // move hex letter 'A'..'F' into range 0..5.
        Vector128<byte> t3 = (vec & Vector128.Create((byte)0xDF)) - Vector128.Create((byte)'A');
        // And correct the range into 10..15.
        // The non-hex letters bytes become greater than 0x0f.
        Vector128<byte> t4 = AddSaturate(t3, Vector128.Create((byte)10));
        // Convert '0'..'9' into nibbles 0..9. Non-digit bytes become
        // greater than 0x0f. Finally choose the result: either valid nibble (0..9/10..15)
        // or some byte greater than 0x0f.
        Vector128<byte> nibbles = Vector128.Min(t2 - Vector128.Create((byte)0xF0), t4);

        Vector128<byte> output;
        if (Ssse3.IsSupported)
        {
            output = Ssse3.MultiplyAddAdjacent(nibbles, Vector128.Create((short)0x0110).AsSByte()).AsByte();
        }
        else
        {
            // Workaround for missing MultiplyAddAdjacent on ARM
            Vector128<short> even = AdvSimd.Arm64.TransposeEven(nibbles, Vector128<byte>.Zero).AsInt16();
            Vector128<short> odd = AdvSimd.Arm64.TransposeOdd(nibbles, Vector128<byte>.Zero).AsInt16();
            even = AdvSimd.ShiftLeftLogical(even, 4).AsInt16();
            output = AdvSimd.AddSaturate(even, odd).AsByte();
        }

        // Accumulate output in lower INT64 half and take care about endianness
        output = BitConverter.IsLittleEndian
            ? Vector128.Shuffle(output, Vector128.Create((byte)6, 4, 2, 0, 14, 12, 10, 8, 0, 0, 0, 0, 0, 0, 0, 0))
            : Vector128.Shuffle(output, Vector128.Create((byte)0, 2, 4, 6, 8, 10, 12, 14, 0, 0, 0, 0, 0, 0, 0, 0));

        value = output.AsUInt64().ToScalar();
        return AddSaturate(nibbles, Vector128.Create((byte)(127 - 15))).ExtractMostSignificantBits() == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> Shuffle(Vector128<byte> vec, Vector128<byte> indices)
    {
        if (Ssse3.IsSupported)
        {
            return Ssse3.Shuffle(vec, indices);
        }
        else if (AdvSimd.Arm64.IsSupported)
        {
            return AdvSimd.Arm64.VectorTableLookup(vec, indices);
        }
        else
        {
            throw new PlatformNotSupportedException();
        }
    }

    public static bool TryParseHex(ulong source, out uint dest)
    {
        var c = (uint)(HexToByte((byte)source) << 4);
        uint err = c;
        uint localDest = c;
        c = HexToByte((byte)(source >> 8));
        err |= c;
        localDest |= c;

        source >>= 16;
        c = (uint)(HexToByte((byte)source) << 4);
        err |= c;
        localDest <<= 8;
        localDest |= c;
        c = HexToByte((byte)(source >> 8));
        err |= c;
        localDest |= c;

        source >>= 16;
        c = (uint)(HexToByte((byte)source) << 4);
        err |= c;
        localDest <<= 8;
        localDest |= c;
        c = HexToByte((byte)(source >> 8));
        err |= c;
        localDest |= c;

        source >>= 16;
        c = (uint)(HexToByte((byte)source) << 4);
        err |= c;
        localDest <<= 8;
        localDest |= c;
        c = HexToByte((byte)(source >> 8));
        err |= c;
        localDest |= c;

        dest = localDest;
        return err != 0xFF;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> SubtractSaturate(Vector128<byte> left, Vector128<byte> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.SubtractSaturate(left, right);
        }
        else if (AdvSimd.IsSupported)
        {
            return AdvSimd.SubtractSaturate(left, right);
        }
        else
        {
            throw new PlatformNotSupportedException();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> AddSaturate(Vector128<byte> left, Vector128<byte> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.AddSaturate(left, right);
        }
        else if (AdvSimd.IsSupported)
        {
            return AdvSimd.AddSaturate(left, right);
        }
        else
        {
            throw new PlatformNotSupportedException();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte HexToByte(nint hex) => Unsafe.Add(ref MemoryMarshal.GetReference(HexToByteLookup), hex);
    
    private static ReadOnlySpan<byte> HexToByteLookup =>
    [
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 15
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 31
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 47
        0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 63
        0xFF, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 79
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 95
        0xFF, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 111
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 127
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 143
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 159
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 175
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 191
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 207
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 223
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 239
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF // 255
    ];


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ByteToHex(int b) => Unsafe.Add(ref MemoryMarshal.GetReference(ByteToHexLookup), b);

    private static ReadOnlySpan<byte> ByteToHexLookup =>
    [
        (byte)'0',
        (byte)'1',
        (byte)'2',
        (byte)'3',
        (byte)'4',
        (byte)'5',
        (byte)'6',
        (byte)'7',
        (byte)'8',
        (byte)'9',
        (byte)'A',
        (byte)'B',
        (byte)'C',
        (byte)'D',
        (byte)'E',
        (byte)'F',
    ];

}