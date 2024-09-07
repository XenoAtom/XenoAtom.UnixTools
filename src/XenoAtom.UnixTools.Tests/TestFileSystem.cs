// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using XenoAtom.UnixTools;

namespace XenoAtom.UnixTools.Tests;

[TestClass]
public class TestFileSystem
{
    [TestMethod]
    public void TestSimple()
    {
        var fs = new UnixMemoryFileSystem();
        var root = fs.RootDirectory;
        Assert.IsTrue(root.IsRoot);
        Assert.AreEqual("/", root.FullPath);
        Assert.AreEqual(UnixDirectory.DefaultMode, root.Mode);
        Assert.AreEqual(UnixFileKind.Directory, root.FileKind);
        Assert.AreEqual(new DeviceId(0, 0), root.Dev);
        Assert.AreEqual(0U, root.Inode.Uid);
        Assert.AreEqual(0U, root.Inode.Gid);
        Assert.AreEqual(0, root.Inode.Index);
        Assert.AreEqual(0, root.Entries.Count);
        Assert.AreEqual(2U, root.HardLinkCount);

        var dir1 = root.CreateDirectory("dir1");
        Assert.IsFalse(dir1.IsRoot);
        Assert.AreEqual("/dir1", dir1.FullPath);
        Assert.AreEqual(UnixDirectory.DefaultMode, dir1.Mode);
        Assert.AreEqual(UnixFileKind.Directory, dir1.FileKind);
        Assert.AreEqual(new DeviceId(0, 0), dir1.Dev);
        Assert.AreEqual(0U, dir1.Inode.Uid);
        Assert.AreEqual(0U, dir1.Inode.Gid);
        Assert.AreEqual(1, dir1.Inode.Index);
        Assert.AreEqual(0, dir1.Entries.Count);
        Assert.AreEqual(2U, dir1.HardLinkCount);
        Assert.AreEqual(1, root.Entries.Count);
        Assert.AreEqual(3U, root.HardLinkCount);
        
        var file1 = dir1.CreateFile("file1", "HelloWorld");
        Assert.AreEqual("/dir1/file1", file1.FullPath);
        Assert.AreEqual(UnixFile.DefaultMode, file1.Mode);
        Assert.AreEqual(UnixFileKind.RegularFile, file1.FileKind);
        Assert.AreEqual(new DeviceId(0, 0), file1.Dev);
        Assert.AreEqual(0U, file1.Inode.Uid);
        Assert.AreEqual(0U, file1.Inode.Gid);
        Assert.AreEqual(2, file1.Inode.Index);
        Assert.AreEqual(1U, file1.HardLinkCount);
        Assert.AreEqual(1, dir1.Entries.Count);

        var linkFile1 = dir1.CreateSymbolicLink("file2", "file1");
        Assert.AreEqual("/dir1/file2", linkFile1.FullPath);
        Assert.AreEqual(UnixSymbolicLink.DefaultMode, linkFile1.Mode);
        Assert.AreEqual(UnixFileKind.SymbolicLink, linkFile1.FileKind);
        Assert.AreEqual(new DeviceId(0, 0), linkFile1.Dev);
        Assert.AreEqual(0U, linkFile1.Inode.Uid);
        Assert.AreEqual(0U, linkFile1.Inode.Gid);
        Assert.AreEqual(3, linkFile1.Inode.Index);
        Assert.AreEqual(1U, linkFile1.HardLinkCount);
        Assert.AreEqual(2, dir1.Entries.Count);

        var hardLink1 = dir1.CreateHardLink("file3", file1);
        Assert.AreEqual("/dir1/file3", hardLink1.FullPath);
        Assert.AreEqual(UnixFile.DefaultMode, hardLink1.Mode);
        Assert.AreEqual(UnixFileKind.RegularFile, hardLink1.FileKind);
        Assert.AreEqual(new DeviceId(0, 0), hardLink1.Dev);
        Assert.AreEqual(0U, hardLink1.Inode.Uid);
        Assert.AreEqual(0U, hardLink1.Inode.Gid);
        Assert.AreEqual(file1.Inode.Index, hardLink1.Inode.Index);
        Assert.AreEqual(3, dir1.Entries.Count);
        Assert.AreEqual(file1.Inode, hardLink1.Inode);
        Assert.AreEqual(2U, file1.HardLinkCount);
        Assert.AreEqual(2U, hardLink1.HardLinkCount);

        var subdir = dir1.CreateDirectory("subdir");
        Assert.AreEqual("/dir1/subdir", subdir.FullPath);
        Assert.AreEqual(UnixDirectory.DefaultMode, subdir.Mode);
        Assert.AreEqual(UnixFileKind.Directory, subdir.FileKind);
        Assert.AreEqual(new DeviceId(0, 0), subdir.Dev);
        Assert.AreEqual(0U, subdir.Inode.Uid);
        Assert.AreEqual(0U, subdir.Inode.Gid);
        Assert.AreEqual(4, subdir.Inode.Index);
        Assert.AreEqual(0, subdir.Entries.Count);
        Assert.AreEqual(2U, subdir.HardLinkCount);
        Assert.AreEqual(4, dir1.Entries.Count);
        Assert.AreEqual(3U, dir1.HardLinkCount);

        var files = root.EnumerateFileSystemEntries(SearchOption.AllDirectories).ToList();
        Assert.AreEqual(5, files.Count);

        Assert.AreEqual("dir1", files[0].Name);
        Assert.AreEqual("file1", files[1].Name);
        Assert.AreEqual("file2", files[2].Name);
        Assert.AreEqual("file3", files[3].Name);
        Assert.AreEqual("subdir", files[4].Name);

        Assert.AreEqual("/dir1", files[0].FullPath);
        Assert.AreEqual("/dir1/file1", files[1].FullPath);
        Assert.AreEqual("/dir1/file2", files[2].FullPath);
        Assert.AreEqual("/dir1/file3", files[3].FullPath);
        Assert.AreEqual("/dir1/subdir", files[4].FullPath);
    }

    [TestMethod]
    public void TestRemoveDirectory()
    {
        var fs = CreateSimpleFileSystem();

        var root = fs.RootDirectory;
        var dir1 = root.GetEntry("dir1") as UnixDirectory;
        Assert.IsNotNull(dir1);
        dir1.Delete();

        Assert.AreEqual(0, root.Entries.Count);
        Assert.AreEqual(2U, root.HardLinkCount);
        Assert.IsFalse(dir1.IsAttached);
    }

    [TestMethod]
    public void TestRemoveHardLinkFile()
    {
        var fs = CreateSimpleFileSystem();

        var root = fs.RootDirectory;
        var dir1 = root.GetEntry("dir1") as UnixDirectory;
        Assert.IsNotNull(dir1);
        var file1 = dir1.GetEntry("file1") as UnixFile;
        Assert.IsNotNull(file1);
        file1.Delete();

        Assert.AreEqual(3, dir1.Entries.Count);
        Assert.IsFalse(file1.IsAttached);

        var file3 = dir1.GetEntry("file3") as UnixFile;
        Assert.IsNotNull(file3);
        Assert.AreEqual(1U, file3.HardLinkCount);
        Assert.IsTrue(file3.IsAttached);
    }
    
    private static UnixMemoryFileSystem CreateSimpleFileSystem()
    {
        var fs = new UnixMemoryFileSystem();
        var root = fs.RootDirectory;
        var dir1 = root.CreateDirectory("dir1");
        var file1 = dir1.CreateFile("file1", "HelloWorld");
        dir1.CreateSymbolicLink("file2", "file1");
        dir1.CreateHardLink("file3", file1);
        dir1.CreateDirectory("subdir");
        return fs;
    }
}