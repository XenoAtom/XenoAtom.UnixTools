// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Text;

namespace XenoAtom.UnixTools;

public static class UnixMemoryFileSystemExtensions
{
    public static void ReadFrom(this UnixMemoryFileSystem fs, CpioReader reader) => fs.RootDirectory.ReadFrom(reader);

    public static void ReadFrom(this UnixDirectory rootDirectory, CpioReader reader, bool overwrite = false)
    {
        rootDirectory.VerifyAttached();
        var fs = rootDirectory.FileSystem!;
            
        var inodes = new Dictionary<uint, UnixFileSystemEntry>();
        while (reader.TryGetNextEntry(out var entry))
        {
            var path = UnixPath.Normalize(UnixPath.Combine(rootDirectory.FullPath, entry.Name));
            UnixFileSystemEntry fsEntry;

            if (inodes.TryGetValue(entry.InodeNumber, out var existingNode))
            {
                fsEntry = fs.CreateHardLink(path, existingNode);
                if (entry.DataStream != null && fsEntry is UnixFile file)
                {
                    file.Content = entry.DataStream;
                }
                else if (entry.LinkName != null && fsEntry is UnixSymbolicLink link)
                {
                    link.Target = entry.LinkName;
                }
            }
            else
            {
                if (path == rootDirectory.FullPath)
                {
                    fsEntry = fs.RootDirectory;
                }
                else
                {
                    if (fs.TryGetEntry(path, out var previousEntry))
                    {
                        if (!overwrite)
                        {
                            throw new InvalidOperationException($"Entry already exists at path {path}");
                        }

                        // If the previous entry is not a directory, we can delete it
                        if (previousEntry is not UnixDirectory)
                        {
                            previousEntry.Delete();
                        }
                    }
                    
                    switch (entry.FileType)
                    {
                        case CpioFileType.Directory:
                            fsEntry = fs.CreateDirectory(path);
                            break;
                        case CpioFileType.RegularFile:
                            fsEntry = fs.CreateFile(path, new UnixFileContent(entry.DataStream!));
                            break;
                        case CpioFileType.SymbolicLink:
                            fsEntry = fs.CreateSymbolicLink(path, entry.LinkName!);
                            break;
                        case CpioFileType.CharacterSpecialDevice:
                            fsEntry = fs.CreateDevice(path, UnixFileKind.CharacterSpecialDevice, entry.Device);
                            break;
                        case CpioFileType.BlockSpecialDevice:
                            fsEntry = fs.CreateDevice(path, UnixFileKind.BlockSpecialDevice, entry.Device);
                            break;
                        default:
                            throw new InvalidDataException($"Unsupported CPIO entry type {entry.FileType}");
                    }
                }

                fsEntry.Dev = entry.Dev;
                fsEntry.Uid = entry.Uid;
                fsEntry.Gid = entry.Gid;
                fsEntry.Mode = entry.Mode;
                fsEntry.LastModifiedTime = entry.ModificationTime;

                // Record nodes
                inodes.Add(entry.InodeNumber, fsEntry);
            }
        }
    }

    public static void WriteTo(this UnixMemoryFileSystem fs, CpioWriter writer) => fs.RootDirectory.WriteTo(writer);

    public static void WriteTo(this UnixDirectory rootDirectory, CpioWriter writer)
    {
        rootDirectory.VerifyAttached();

        var mapEntryToHardLinkCount = new Dictionary<UnixInode, uint>();
        var stack = new Stack<UnixFileSystemEntry>();
        var subEntries = new List<UnixFileSystemEntry>();
        stack.Push(rootDirectory);

        var rootPath = rootDirectory.FullPath;
        var rootPathLength = rootPath.Length;
        if (!rootPath.EndsWith('/'))
        {
            rootPathLength++;
        }

        while (stack.Count > 0)
        {
            var entry = stack.Pop();

            // For hardlinks, we need to keep track of the hardlink count
            // to write the content only once for the last hardlink
            MemoryStream? content = null;
            uint length = 0;
            if (entry.FileKind == UnixFileKind.RegularFile)
            {
                ref var hardLinkCount = ref CollectionsMarshal.GetValueRefOrAddDefault(mapEntryToHardLinkCount, entry.Inode, out var exists);
                if (!exists)
                {
                    hardLinkCount = entry.HardLinkCount;
                }

                hardLinkCount--;

                if (hardLinkCount == 0 && entry is UnixFile file)
                {
                    content = new MemoryStream();
                    file.Content.CopyTo(content, Encoding.UTF8);
                    length = (uint)content.Length;
                    content.Position = 0;
                }
            }

            var relativeName = entry.Name.Length == 0 ? "." : entry.FullPath.Substring(rootPathLength); // Discard the leading /
            writer.AddEntry(new CpioEntry
            {
                Name = relativeName,
                InodeNumber = (uint)entry.Inode.Index,
                FileType = entry switch
                {
                    UnixDirectory => CpioFileType.Directory,
                    UnixFile => CpioFileType.RegularFile,
                    UnixSymbolicLink => CpioFileType.SymbolicLink,
                    UnixDeviceFile device => device.FileKind switch
                    {
                        UnixFileKind.CharacterSpecialDevice => CpioFileType.CharacterSpecialDevice,
                        UnixFileKind.BlockSpecialDevice => CpioFileType.BlockSpecialDevice,
                        _ => throw new InvalidDataException($"Unsupported device type {device.FileKind}")
                    },
                    _ => throw new InvalidDataException($"Unsupported entry type {entry.GetType()}")
                },
                Mode = entry.Mode,
                Uid = entry.Uid,
                Gid = entry.Gid,
                Length = length,
                Dev = entry.Dev,
                HardLinkCount = entry.HardLinkCount,
                Device = entry is UnixDeviceFile deviceFile ? deviceFile.Device : default,
                ModificationTime = entry.LastModifiedTime,
                DataStream = content,
                LinkName = entry is UnixSymbolicLink link ? link.Target : null
            });

            // Proceed to children
            if (entry is UnixDirectory directory)
            {
                subEntries.Clear();
                foreach (var subEntry in directory.Entries)
                {
                    subEntries.Add(subEntry);
                }

                for(int i = subEntries.Count - 1; i >= 0; i--)
                {
                    var child = subEntries[i];
                    stack.Push(child);
                }
            }
        }

        subEntries.Clear();
    }
}