// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.UnixTools.Tests;

[TestClass]
public class CpioTests
{
    /// <summary>
    /// Test reading an existing archive and writing it as-is.
    /// </summary>
    [TestMethod]
    public void TestReaderWriter()
    {
        var fileStream = new MemoryStream(File.ReadAllBytes(@"cpio_archive_test.cpio"));
        var fileStreamOut = new MemoryStream();

        // Use a block to dispose the reader/writer
        {
            using var reader = new CpioReader(fileStream);
            using var writer = new CpioWriter(fileStreamOut);
            while (reader.TryGetNextEntry(out var entry))
            {
                writer.AddEntry(entry);
            }
        }
    
        var bufferIn = fileStream.ToArray();
        var bufferOut = fileStreamOut.ToArray();

        // The archive was created on Ubuntu/GNU Tools, so the alignment is 512 bytes
        var length = AlignHelper.AlignUp(bufferOut.Length, 512);
        var newBufferOut = new byte[length];
        Array.Copy(bufferOut, newBufferOut, bufferOut.Length);
        bufferOut = newBufferOut;

        // We check that we have the same content
        Assert.AreEqual(bufferIn.Length, bufferOut.Length, "Invalid length of the generated archive");
        for (int i = 0; i < bufferIn.Length; i++)
        {
            Assert.AreEqual(bufferIn[i], bufferOut[i], $"Invalid value 0x{bufferOut[i]:X2} at position {i} / 0x{i:X4}");
        }
    }

    [TestMethod]
    public void TestWithFileSystem()
    {
        var fileStream = new MemoryStream(File.ReadAllBytes(@"cpio_archive_test.cpio"));
        var fileStreamOut = new MemoryStream();

        var fs = new UnixMemoryFileSystem();

        // Use a block to dispose the reader/writer
        {
            using var reader = new CpioReader(fileStream, true);
            using var writer = new CpioWriter(fileStreamOut, true);
            var entries = new List<CpioEntry>();
            while (reader.TryGetNextEntry(out var entry))
            {
                entries.Add(entry);
            }
            entries.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));

            foreach (var entry in entries)
            {
                writer.AddEntry(entry);
            }
        }

        fileStream = fileStreamOut;
        fileStream.Position = 0;
        fileStreamOut = new MemoryStream();
        
        // Use a block to dispose the reader/writer
        {
            using var reader = new CpioReader(fileStream, true);
            using var writer = new CpioWriter(fileStreamOut, true);
            Console.WriteLine("Reading from cpio");
            fs.ReadFrom(reader);
            Console.WriteLine("Writing to cpio");
            fs.WriteTo(writer);
        }
        
        var bufferIn = fileStream.ToArray();
        var bufferOut = fileStreamOut.ToArray();

        File.WriteAllBytes("cpio1.cpio", bufferIn);
        File.WriteAllBytes("cpio2.cpio", bufferOut);

        // We check that we have the same content
        Assert.AreEqual(bufferIn.Length, bufferOut.Length, "Invalid length of the generated archive");
        for (int i = 0; i < bufferIn.Length; i++)
        {
            Assert.AreEqual(bufferIn[i], bufferOut[i], $"Invalid value 0x{bufferOut[i]:X2} at position {i} / 0x{i:X4}");
        }
    }
}