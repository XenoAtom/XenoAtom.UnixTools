// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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

    internal SortedDictionary<string, UnixFileSystemEntry> InternalEntries => Inode.GetDictionaryContent();
    
    public UnixFile CreateFile(string path, bool createIntermediateDirectories = false)
    {
        var (dir, name) = ResolveEntryForCreate(path, createIntermediateDirectories);
        var file = new UnixFile(name!, CreateNode(UnixFileKind.RegularFile));
        dir.AddEntry(file);
        return file;
    }

    public UnixFile CreateFile(string path, UnixFileContent content, bool createIntermediateDirectories = false)
    {
        var (dir, name) = ResolveEntryForCreate(path, createIntermediateDirectories);
        var node = CreateNode(UnixFileKind.RegularFile, content.Data);
        var file = new UnixFile(name!, node);
        dir.AddEntry(file);
        return file;
    }

    public UnixDirectory CreateDirectory(string path, bool createIntermediateDirectories = false) 
    {
        var (dir, name) = ResolveEntryForCreate(path, createIntermediateDirectories);
        var node = CreateNode(UnixFileKind.Directory, new SortedDictionary<string, UnixFileSystemEntry>(StringComparer.Ordinal));
        var directory = new UnixDirectory(name!, node);
        dir.AddEntry(directory);
        return directory;
    }

    public UnixDeviceFile CreateDevice(string path, UnixFileKind kind, DeviceId id, bool createIntermediateDirectories = false)
    {
        if (kind != UnixFileKind.CharacterSpecialDevice && kind != UnixFileKind.BlockSpecialDevice)
        {
            throw new ArgumentException("Invalid kind for a device file. Must be either CharacterSpecialDevice or BlockSpecialDevice", nameof(kind));
        }

        var (dir, name) = ResolveEntryForCreate(path, createIntermediateDirectories);
        var device = new UnixDeviceFile(name!, CreateNode(kind))
        {
            Device = id
        };
        dir.AddEntry(device);
        return device;
    }

    public UnixSymbolicLink CreateSymbolicLink(string path, string target, bool createIntermediateDirectories = false)
    {
        var (dir, name) = ResolveEntryForCreate(path, createIntermediateDirectories);
        // A symbolic link doesn't check if the target exists
        var node = CreateNode(UnixFileKind.SymbolicLink, target);
        var link = new UnixSymbolicLink(name!, node);
        dir.AddEntry(link);
        return link;
    }

    public TEntry CreateHardLink<TEntry>(string path, TEntry target, bool createIntermediateDirectories = false) where TEntry: UnixFileSystemEntry
    {
        var (dir, name) = ResolveEntryForCreate(path, createIntermediateDirectories);
        var link = (TEntry)CloneEntry(target, false, newName: name!);
        dir.AddEntry(link);
        return link;
    }

    public bool ContainsEntry(string path)
    {
        return TryGetEntry(path, out _);
    }

    public bool TryGetEntry(string path, [NotNullWhen(true)] out UnixFileSystemEntry? entry)
    {
        var dir = this;
        var name = path;
        
        if (path.Contains('/') && !TryGetDirectory(path, false, true, out dir, out name, out var notFoundReason))
        {
            entry = null;
            return false;
        }

        return dir.InternalEntries.TryGetValue(name!, out entry);
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

    public void CopyEntry(string sourcePath, string destinationPath, UnixCopyMode mode = UnixCopyMode.Single, bool overwrite = false)
    {
        VerifyAttached();

        UnixPath.Validate(sourcePath, nameof(sourcePath));
        UnixPath.Validate(destinationPath, nameof(destinationPath));

        if (!TryGetEntry(sourcePath, out var sourceEntry))
        {
            throw new ArgumentException($"The source path `{sourcePath}` does not exist in the directory `{Name}`");
        }

        // For the destination path, go through the root directory to allow copying an entry to any directory
        destinationPath = UnixPath.Combine(FullPath, UnixPath.Normalize(destinationPath));
        var fs = FileSystem!;

        if (!fs.RootDirectory.TryGetDirectory(destinationPath, false, true, out var destinationDirectory, out var destinationName, out var notFoundReason))
        {
            throw new ArgumentException(notFoundReason, nameof(destinationPath));
        }

        // Check if the destination entry already exists
        if (destinationName is not null && destinationDirectory.TryGetEntry(destinationName, out var destinationEntry))
        {
            // Don't do anything if the source and destination are the same
            if (destinationEntry == sourceEntry)
            {
                return;
            }

            if (destinationEntry is UnixDirectory directory)
            {
                // If the destination is a directory, we copy the source entry into it
                destinationDirectory = directory;
            }
            else
            {
                if (overwrite)
                {
                    // If the destination is a file, we delete it
                    destinationEntry.Delete();
                }
                else
                {
                    ThrowEntryAlreadyExists(destinationDirectory, destinationName);
                }
            }
        }

        // Copy the source entry to the destination directory
        bool recursive = mode == UnixCopyMode.Recursive || mode == UnixCopyMode.RecursiveWithHardLinks || mode == UnixCopyMode.Archive;
        if (sourceEntry is UnixDirectory && recursive)
        {
            bool copy = mode != UnixCopyMode.RecursiveWithHardLinks;
            var mapping = mode == UnixCopyMode.Archive ? new Dictionary<UnixInode, UnixInode>() : null;

            var subEntries = new List<UnixFileSystemEntry>();
            var stack = new Stack<(UnixFileSystemEntry Source, UnixDirectory DestinationDirectory, string DestinationName)>();
            stack.Push((sourceEntry, destinationDirectory, destinationName ?? sourceEntry.Name));

            while (stack.Count > 0)
            {
                var (source, destination, destName) = stack.Pop();

                if (destination.ContainsEntry(destName))
                {
                    ThrowEntryAlreadyExists(destination, destName);
                }

                if (source is UnixDirectory directory)
                {
                    var newDestinationDirectory = (UnixDirectory)CloneEntry(directory, true, newName: destName);
                    destination.AddEntry(newDestinationDirectory);

                    subEntries.Clear();
                    foreach (var subEntry in directory.Entries)
                    {
                        subEntries.Add(subEntry);
                    }

                    // Push the sub entries in reverse order to keep the order when popping
                    for (int i = subEntries.Count - 1; i >= 0; i--)
                    {
                        var child = subEntries[i];
                        stack.Push((child, newDestinationDirectory, child.Name));
                    }
                }
                else
                {
                    var clone = CloneEntry(source, copy, mapping, newName: destName);
                    destination.AddEntry(clone);
                }
            }
        }
        else
        {
            var copy = CloneEntry(sourceEntry, true, newName: destinationName ?? sourceEntry.Name);
            destinationDirectory.AddEntry(copy);
        }
    }

    public void MoveEntry(string sourcePath, string destinationPath, bool createIntermediateDirectories = false, bool overwrite = false)
    {
        VerifyAttached();

        UnixPath.Validate(sourcePath, nameof(sourcePath));
        UnixPath.Validate(destinationPath, nameof(destinationPath));
        
        if (!TryGetEntry(sourcePath, out var sourceEntry))
        {
            throw new ArgumentException($"The source path `{sourcePath}` does not exist in the directory `{Name}`");
        }

        // For the destination path, go through the root directory to allow moving an entry to any directory
        destinationPath = UnixPath.Combine(FullPath, UnixPath.Normalize(destinationPath));
        var fileSystem = FileSystem!;

        // Fetch the destination directory
        if (!fileSystem.RootDirectory.TryGetDirectory(destinationPath, createIntermediateDirectories, true, out var destinationDirectory, out var destinationName, out var notFoundReason))
        {
            throw new ArgumentException(notFoundReason, nameof(destinationPath));
        }

        // Check if the destination entry already exists
        if (destinationDirectory.TryGetEntry(destinationName!, out var destinationEntry))
        {
            // Don't do anything if the source and destination are the same
            if (destinationEntry == sourceEntry)
            {
                return;
            }
            
            if (destinationEntry is UnixDirectory directory)
            {
                // If the destination is a directory, we move the source entry into it
                destinationDirectory = directory;
            }
            else
            {
                if (overwrite)
                {
                    // If the destination is a file, we delete it
                    destinationEntry.Delete();
                }
                else
                {
                    ThrowEntryAlreadyExists(destinationDirectory, destinationName!);
                }
            }
        }

        sourceEntry.SetParent(destinationDirectory, destinationName!);
    }

    public void DeleteEntry(string path)
    {
        var entry = GetEntry(path);
        if (entry is UnixDirectory directory && directory.IsRoot)
        {
            throw new InvalidOperationException("Cannot delete the root folder");
        }
        entry.SetParent(null);
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
        // Make a copy (to allow adding/removing entries while iterating)
        var entries = new List<UnixFileSystemEntry>(Entries.Count);
        entries.AddRange(Entries);

        foreach (var entry in entries)
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
        // Make a copy (to allow adding/removing entries while iterating)

        var entries = new List<UnixFileSystemEntry>(Entries.Count);
        entries.AddRange(Entries);

        foreach (var entry in entries)
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
        entry.SetParent(this);
    }

    private static UnixFileSystemEntry CloneEntry(UnixFileSystemEntry entry, bool copy, Dictionary<UnixInode, UnixInode>? mapping = null, string? newName = null)
    {
        UnixInode newInode;
        newName ??= entry.Name;

        if (copy)
        {
            // When copying and we are respecting mapping, we need to check if the inode has already been copied
            if (mapping is not null && mapping.TryGetValue(entry.Inode, out var tempNode))
            {
                newInode = tempNode;
            }
            else
            {
                // Otherwise, we make a copy of the inode
                newInode = entry.Inode.CreateCopy(entry.FileSystem!);
            }
        }
        else
        {
            // Clone the inode without copying the content
            newInode = entry.Inode;
        }
        
        UnixFileSystemEntry newEntry = entry.FileKind switch
        {
            UnixFileKind.Directory => new UnixDirectory(newName, newInode),
            UnixFileKind.RegularFile => new UnixFile(newName, newInode),
            UnixFileKind.SymbolicLink => new UnixSymbolicLink(newName, newInode),
            UnixFileKind.CharacterSpecialDevice or UnixFileKind.BlockSpecialDevice => new UnixDeviceFile(newName, newInode),
            _ => throw new ArgumentOutOfRangeException()
        };

        if (copy && mapping is not null)
        {
            mapping.TryAdd(entry.Inode, newEntry.Inode);
        }

        return newEntry;
    }
    
    private bool TryGetDirectory(string path, bool createIntermediateDirectories, bool skipLastEntry, [NotNullWhen(true)] out UnixDirectory? currentDirectory, out string? lastName, [NotNullWhen(false)] out string? notFoundReason)
    {
        VerifyAttached();
        ValidatePath(path);

        var normalizedRelativePath = UnixPath.Normalize(path);
        var fullPath = FullPath;
        var targetPath = UnixPath.Normalize(UnixPath.Combine(fullPath, normalizedRelativePath)).AsSpan();

        currentDirectory = this;
        lastName = null;
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

                var name = spanName.ToString();
                lastName = name;

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
                    if (createIntermediateDirectories && (!skipLastEntry || !isLastEntry))
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
    
    private static void ValidatePath(string path)
    {
        UnixPath.Validate(path);
        if (path.Length == 0) throw new ArgumentException("Path cannot be empty", nameof(path));
    }

    private UnixInode CreateNode(UnixFileKind kind, object? data = null)
    {
        VerifyAttached();
        var fileSystem = FileSystem!;
        var node = new UnixInode(fileSystem.NextInodeIndex++, kind, data);
        node.Mode = kind switch
        {
            UnixFileKind.Directory => DefaultMode,
            UnixFileKind.RegularFile => UnixFile.DefaultMode,
            UnixFileKind.SymbolicLink => UnixSymbolicLink.DefaultMode,
            UnixFileKind.CharacterSpecialDevice => UnixFile.DefaultMode,
            UnixFileKind.BlockSpecialDevice => UnixFile.DefaultMode,
            _ => node.Mode
        };

        return node;
    }

    private (UnixDirectory directory, string name) ResolveEntryForCreate(string path, bool createIntermediateDirectories = false)
    {
        ValidatePath(path);

        UnixDirectory? dir = this;
        string? name = path;

        if (name.Contains('/'))
        {
            if (!TryGetDirectory(path, createIntermediateDirectories, true, out dir, out name, out var notFoundReason))
            {
                throw new ArgumentException(notFoundReason, nameof(path));
            }
        }

        if (dir.InternalEntries.ContainsKey(name!))
        {
            throw new ArgumentException($"An entry with the name `{name}` already exists in the directory `{dir.FullPath}`");
        }

        return (dir, name!);
    }

    [DoesNotReturn]
    private void ThrowEntryAlreadyExists(UnixDirectory directory, string name)
    {
        throw new ArgumentException($"An entry with the name `{name}` already exists in the directory `{directory.FullPath}`");
    }

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