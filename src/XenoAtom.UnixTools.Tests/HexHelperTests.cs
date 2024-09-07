// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;

namespace XenoAtom.UnixTools.Tests;

[TestClass]
public class HexHelperTests
{
    [TestMethod]
    public void TestHexToDecimal()
    {
        var input = "0123456789ABCDEF"u8.ToArray();
        bool b;
        if (HexHelper.IsVectorizedVersionSupported)
        {
            var data = Vector128.Create(input);
            b = HexHelper.TryParseHex(data, out var value);
            Assert.IsTrue(b);

            Assert.AreEqual(0x89abcdef01234567UL, value);
        }

        var input64 = Unsafe.As<byte, ulong>(ref input[0]);
        b = HexHelper.TryParseHex(input64, out var v1);
        Assert.IsTrue(b);
        Assert.AreEqual(0x01234567U, v1);
        
        input64 = Unsafe.As<byte, ulong>(ref input[8]);
        b = HexHelper.TryParseHex(input64, out var v2);
        Assert.IsTrue(b);
        Assert.AreEqual(0x89abcdefU, v2);
    }

    [TestMethod]
    public void TestDecimalToHex()
    {
        if (HexHelper.IsVectorizedVersionSupported)
        {
            var value = 0x89abcdef01234567UL;
            HexHelper.ConvertToHex(value, out var vec);
            var array = new byte[16];
            vec.CopyTo(array);
            Assert.AreEqual("0123456789ABCDEF", Encoding.UTF8.GetString(array));
        }

        var value1 = 0x01234567U;
        HexHelper.ConvertToHex(value1, out var hex1);
        Assert.AreEqual("01234567", Encoding.UTF8.GetString(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref hex1, 1))));

        var value2 = 0x89abcdef;
        HexHelper.ConvertToHex(value2, out var hex2);
        Assert.AreEqual("89ABCDEF", Encoding.UTF8.GetString(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref hex2, 1))));
    }
}
