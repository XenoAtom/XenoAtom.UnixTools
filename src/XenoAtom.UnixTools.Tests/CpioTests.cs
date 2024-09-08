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

        var fs = new UnixInMemoryFileSystem();
        var entries = new List<CpioEntry>();

        // Use a block to dispose the reader/writer
        {
            using var reader = new CpioReader(fileStream, true);
            using var writer = new CpioWriter(fileStreamOut, true);
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
            fs.ReadFrom(reader);
            fs.WriteTo(writer);
        }
        
        var bufferIn = fileStream.ToArray();
        var bufferOut = fileStreamOut.ToArray();

        //File.WriteAllBytes("cpio1.cpio", bufferIn);
        //File.WriteAllBytes("cpio2.cpio", bufferOut);

        // We check that we have the same content
        Assert.AreEqual(bufferIn.Length, bufferOut.Length, "Invalid length of the generated archive");
        for (int i = 0; i < bufferIn.Length; i++)
        {
            Assert.AreEqual(bufferIn[i], bufferOut[i], $"Invalid value 0x{bufferOut[i]:X2} at position {i} / 0x{i:X4}");
        }

        // Check that we have the same entries
        var entriesOut = new List<CpioEntry>();
        fileStreamOut.Position = 0;
        using (var reader = new CpioReader(fileStreamOut, true))
        {
            while (reader.TryGetNextEntry(out var entry))
            {
                entriesOut.Add(entry);
            }
        }

        Assert.AreEqual(entries.Count, entriesOut.Count, "Invalid number of entries");
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var entryOut = entriesOut[i];

            Assert.AreEqual(entry.Name, entryOut.Name, "Invalid name");
            Assert.AreEqual(entry.InodeNumber, entryOut.InodeNumber, "Invalid inode number");
            Assert.AreEqual(entry.FileType, entryOut.FileType, "Invalid file type");
            Assert.AreEqual(entry.Dev, entryOut.Dev, "Invalid dev");
            Assert.AreEqual(entry.Mode, entryOut.Mode, "Invalid mode");
            Assert.AreEqual(entry.Uid, entryOut.Uid, "Invalid uid");
            Assert.AreEqual(entry.Gid, entryOut.Gid, "Invalid gid");
            Assert.AreEqual(entry.Length, entryOut.Length, "Invalid length");
            Assert.AreEqual(entry.HardLinkCount, entryOut.HardLinkCount, "Invalid hard link count");
            Assert.AreEqual(entry.Device, entryOut.Device, "Invalid device");
            Assert.AreEqual(entry.ModificationTime, entryOut.ModificationTime, "Invalid modification time");
            Assert.AreEqual(entry.Checksum, entryOut.Checksum, "Invalid checksum");

            Assert.AreEqual(entry.LinkName, entryOut.LinkName, "Invalid link name");

            if (entry.DataStream == null)
            {
                Assert.IsNull(entryOut.DataStream, "Invalid content");
            }
            else
            {
                Assert.IsNotNull(entryOut.DataStream, "Invalid content");
                Assert.AreEqual(entry.DataStream.Length, entryOut.DataStream.Length, "Invalid content length");

                var contentIn = new MemoryStream();
                var contentOut = new MemoryStream();
                entry.DataStream.Position = 0;
                entry.DataStream.CopyTo(contentIn);
                entryOut.DataStream.Position = 0;
                entryOut.DataStream.CopyTo(contentOut);

                Assert.AreEqual(contentIn.Length, contentOut.Length, "Invalid content length");
                for (int j = 0; j < contentIn.Length; j++)
                {
                    Assert.AreEqual(contentIn.GetBuffer()[j], contentOut.GetBuffer()[j], $"Invalid content at position {j}");
                }
            }
        }
    }
}