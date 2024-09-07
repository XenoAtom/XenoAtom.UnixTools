// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Enumeration;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace XenoAtom.UnixTools;

public sealed class UnixDirectory : UnixFileSystemEntry
{
    private const int MaximumPathDepth = 2048;

    public const UnixFileMode DefaultMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
    
    internal UnixDirectory(string name, UnixInode node) : base(name, node)
    {
    }
    
    public bool IsRoot => Parent == null && Name.Length == 0;

    public SortedDictionary<string, UnixFileSystemEntry>.ValueCollection Entries => InternalEntries.Values;

    private SortedDictionary<string, UnixFileSystemEntry> InternalEntries => Inode.GetDictionaryContent();
    
    public UnixFile CreateFile(string path, bool createDirectory = false)
    {
        if (!TryGetDirectory(path, createDirectory, true, out var dir, out var name, out var notFoundReason))
        {
            throw new ArgumentException(notFoundReason, nameof(path));
        }
        
        var file = new UnixFile(name.ToString(), CreateNode(UnixFileKind.RegularFile));
        dir.AddEntry(file);
        return file;
    }

    public UnixFile CreateFile(string path, UnixFileContent content, bool createDirectory = false)
    {
        if (!TryGetDirectory(path, createDirectory, true, out var dir, out var name, out var notFoundReason))
        {
            throw new ArgumentException(notFoundReason, nameof(path));
        }
        
        var node = CreateNode(UnixFileKind.RegularFile, content.Data);
        node.Mode = UnixFile.DefaultMode;
        var file = new UnixFile(name.ToString(), node);
        dir.AddEntry(file);
        return file;
    }

    public UnixDirectory CreateDirectory(string path, bool createDirectory = false) 
    {
        if (!TryGetDirectory(path, createDirectory, true, out var dir, out var name, out var notFoundReason))
        {
            throw new ArgumentException(notFoundReason, nameof(path));
        }

        var node = CreateNode(UnixFileKind.Directory, new SortedDictionary<string, UnixFileSystemEntry>(StringComparer.Ordinal));
        node.Mode = DefaultMode;
        var directory = new UnixDirectory(name.ToString(), node);
        dir.AddEntry(directory);
        return directory;
    }

    public UnixDeviceFile CreateDevice(string path, UnixFileKind kind, DeviceId id, bool createDirectory = false)
    {
        if (kind != UnixFileKind.CharacterSpecialDevice && kind != UnixFileKind.BlockSpecialDevice)
        {
            throw new ArgumentException("Invalid kind for a device file. Must be either CharacterSpecialDevice or BlockSpecialDevice", nameof(kind));
        }
        if (!TryGetDirectory(path, createDirectory, true, out var dir, out var name, out var notFoundReason))
        {
            throw new ArgumentException(notFoundReason, nameof(path));
        }

        var device = new UnixDeviceFile(name.ToString(), CreateNode(kind))
        {
            Device = id
        };
        dir.AddEntry(device);
        return device;
    }

    public UnixSymbolicLink CreateSymbolicLink(string path, string target, bool createDirectory = false)
    {
        if (!TryGetDirectory(path, createDirectory, true, out var dir, out var name, out var notFoundReason))
        {
            throw new ArgumentException(notFoundReason, nameof(path));
        }

        // A symbolic link doesn't check if the target exists
        var node = CreateNode(UnixFileKind.SymbolicLink, target);
        node.Mode = UnixSymbolicLink.DefaultMode;
        var link = new UnixSymbolicLink(name.ToString(), node);
        dir.AddEntry(link);
        return link;
    }

    public TEntry CreateHardLink<TEntry>(string path, TEntry target, bool createDirectory = false) where TEntry: UnixFileSystemEntry
    {
        if (!TryGetDirectory(path, createDirectory, true, out var dir, out var name, out var notFoundReason))
        {
            throw new ArgumentException(notFoundReason, nameof(path));
        }

        var link = (TEntry)target.CloneWithName(name.ToString());
        link.Inode.HardLinkCount++;
        dir.AddEntry(link);
        return link;
    }

    public bool TryGetEntry(string path, [NotNullWhen(true)] out UnixFileSystemEntry? entry)
    {
        if (!TryGetDirectory(path, false, true, out var dir, out var name, out var notFoundReason))
        {
            entry = null;
            return false;
        }
        
        return dir.InternalEntries.TryGetValue(name.ToString(), out entry);
    }

    public UnixFileSystemEntry this[string path] => GetEntry(path);
    
    public UnixFileSystemEntry GetEntry(string path)
    {
        if (!TryGetEntry(path, out var entry))
        {
            throw new ArgumentException($"An entry with the path `{path}` does not exist in the directory `{Name}`");
        }
        return entry;
    }
    
    public void RemoveEntry(string path)
    {
        var entry = GetEntry(path);
        entry.Parent!.InternalEntries.Remove(entry.Name);
        
        if (entry is UnixDirectory directory)
        {
            foreach (var child in directory.Entries.ToList())
            {
                directory.RemoveEntry(child.Name);
            }
        }

        // Decrement the parent hardlink count if the entry is a directory
        // because of the implicit `..` entry in the subdirectory
        if (entry.FileKind == UnixFileKind.Directory)
        {
            Inode.HardLinkCount--;
        }

        entry.Parent = null;
        entry.FileSystem = null;

        // Decrease the hard link count
        entry.Inode.HardLinkCount--;

        // If the hard link count is 0 or 1 (for directory), the node is not used anymore, so we can remove the inode from the filesystem
        if (entry.Inode.HardLinkCount == 0 || (entry.FileKind == UnixFileKind.Directory && entry.Inode.HardLinkCount == 1))
        {
            entry.Inode.HardLinkCount = 0;
        }
    }

    public IEnumerable<UnixFileSystemEntry> EnumerateFileSystemEntries(SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        if (searchOption == SearchOption.TopDirectoryOnly)
        {
            foreach (var entry in Entries)
            {
                yield return entry;
            }
        }
        else
        {
            foreach (var entry in Entries)
            {
                yield return entry;
                if (entry is UnixDirectory directory)
                {
                    foreach (var child in directory.EnumerateFileSystemEntriesRecursive())
                    {
                        yield return child;
                    }
                }
            }
        }
    }

    public IEnumerable<UnixFileSystemEntry> EnumerateFileSystemEntries(string searchPattern, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        foreach (var entry in Entries)
        {
            if (FileSystemName.MatchesSimpleExpression(searchPattern, entry.Name, false))
            {
                yield return entry;
            }

            if (searchOption == SearchOption.AllDirectories && entry is UnixDirectory directory)
            {
                foreach (var child in directory.EnumerateFileSystemEntriesRecursive())
                {
                    yield return child;
                }
            }
        }
    }

    private IEnumerable<UnixFileSystemEntry> EnumerateFileSystemEntriesRecursive()
    {
        foreach (var entry in Entries)
        {
            yield return entry;
            if (entry is UnixDirectory directory)
            {
                foreach (var child in directory.EnumerateFileSystemEntriesRecursive())
                {
                    yield return child;
                }
            }
        }
    }

    private void AddEntry(UnixFileSystemEntry entry)
    {
        Debug.Assert(entry is not null);
        if (!InternalEntries.TryAdd(entry.Name, entry))
        {
            throw new ArgumentException($"An entry with the name `{entry.Name}` already exists in the directory `{FullPath}`");
        }
        entry.Parent = this;
        entry.FileSystem = FileSystem;

        // Increment the parent hardlink count if the entry is a directory
        // because of the implicit `..` entry in the subdirectory
        if (entry.FileKind == UnixFileKind.Directory)
        {
            Inode.HardLinkCount++;
        }
    }

    private bool TryGetDirectory(string path, bool createDirectory, bool skipLastEntry, out UnixDirectory currentDirectory, out ReadOnlySpan<char> lastName, [NotNullWhen(false)] out string? notFoundReason)
    {
        VerifyAttached();
        ValidatePath(path);

        var normalizedRelativePath = UnixPath.Normalize(path);
        var fullPath = FullPath;
        var targetPath = UnixPath.Normalize(UnixPath.Combine(fullPath, normalizedRelativePath)).AsSpan();

        currentDirectory = this;
        lastName = default;
        notFoundReason = null;

        // We only accept relative path
        if (!targetPath.StartsWith(fullPath) && (targetPath.Length > fullPath.Length && targetPath[fullPath.Length] != '/'))
        {
            notFoundReason = $"The resolved path `{targetPath}` is not a subpath of the directory `{fullPath}`";
            return false;
        }

        targetPath = targetPath.Slice(fullPath.Length + (IsRoot ? 0 : 1));
        if (targetPath.Length == 0)
        {
            currentDirectory = this;
            return true;
        }
        
        var buffer = ArrayPool<byte>.Shared.Rent(Unsafe.SizeOf<Range>() * MaximumPathDepth);
        var ranges = MemoryMarshal.Cast<byte, Range>(buffer.AsSpan());
        var rangeCount = targetPath.Split(ranges, '/');

        try
        {
            for (var i = 0; i < rangeCount; i++)
            {
                var offset = ranges[i].Start.Value;
                var length = ranges[i].End.Value - offset;
                Debug.Assert(length > 0); // This should have been fixed by UnixPath.Normalize

                var spanName = targetPath.Slice(offset, length);
                lastName = spanName;

                var name = spanName.ToString();

                var isLastEntry = i == rangeCount - 1;
                if (currentDirectory.InternalEntries.TryGetValue(name, out var entry))
                {
                    if (isLastEntry && skipLastEntry)
                    {
                        return true;
                    }

                    if (entry is UnixDirectory directory)
                    {
                        currentDirectory = directory;
                        continue;
                    }

                    notFoundReason = $"An entry with the name `{name}` already exists and is not a directory";
                    return false;
                }
                else
                {
                    if (createDirectory && (!skipLastEntry || !isLastEntry))
                    {
                        var directory = currentDirectory.CreateDirectory(name);
                        currentDirectory = directory;
                    }
                    else
                    {
                        if (isLastEntry && skipLastEntry)
                        {
                            return true;
                        }

                        notFoundReason = $"A directory entry with the name `{name}` does not exist";
                        return false;
                    }
                }

            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        lastName = default;
        return true;
    }
    
    private void ValidatePath(string path)
    {
        UnixPath.Validate(path);
        if (path.Length == 0) throw new ArgumentException("Path cannot be empty", nameof(path));
    }

    private UnixInode CreateNode(UnixFileKind kind, object? data = null)
    {
        VerifyAttached();
        var fileSystem = FileSystem!;
        var node = new UnixInode(fileSystem.NextInodeIndex++, kind, data)
        {
            HardLinkCount = kind == UnixFileKind.Directory ? 2U : 1,
        };
        return node;
    }

    internal override UnixFileSystemEntry CloneWithName(string name) => new UnixDirectory(name, Inode);

    internal static UnixDirectory CreateRoot(UnixMemoryFileSystem fs)
    {
        var node = new UnixInode(0, UnixFileKind.Directory, new SortedDictionary<string, UnixFileSystemEntry>(StringComparer.Ordinal))
        {
            Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute,
            HardLinkCount = 2,
        };
        var root = new UnixDirectory(string.Empty, node)
        {
            FileSystem = fs
        };
        return root;
    }
}