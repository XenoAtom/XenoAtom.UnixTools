// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace XenoAtom.UnixTools;

public sealed class UnixMemoryFileSystem
{
    public UnixMemoryFileSystem()
    {
        NextInodeIndex = 1; // We start at 2 to avoid the root inode 1
        RootDirectory = UnixDirectory.CreateRoot(this);
    }

    internal long NextInodeIndex { get; set; }

    public UnixDirectory RootDirectory { get; }
    
    public UnixDirectory CreateDirectory(string fullPath)
    {
        ValidateFullPath(fullPath);
        return RootDirectory.CreateDirectory(fullPath, true);
    }
    
    public UnixFile CreateFile(string fullPath) => CreateFile(fullPath, UnixFileContent.Empty);

    public UnixFile CreateFile(string fullPath, UnixFileContent content)
    {
        ValidateFullPath(fullPath);
        return RootDirectory.CreateFile(fullPath, content, true);
    }

    public UnixDeviceFile CreateDevice(string fullPath, UnixFileKind kind, DeviceId id)
    {
        ValidateFullPath(fullPath);
        return RootDirectory.CreateDevice(fullPath, kind, id, true);
    }

    public UnixSymbolicLink CreateSymbolicLink(string fullPath, string target)
    {
        ValidateFullPath(fullPath);
        return RootDirectory.CreateSymbolicLink(fullPath, target, true);
    }

    public TEntry CreateHardLink<TEntry>(string fullPath, TEntry entry) where TEntry : UnixFileSystemEntry
    {
        ValidateFullPath(fullPath);
        return RootDirectory.CreateHardLink(fullPath, entry, true);
    }

    public bool TryGetEntry(string fullPath, [NotNullWhen(true)] out UnixFileSystemEntry? entry)
    {
        ValidateFullPath(fullPath);
        return RootDirectory.TryGetEntry(fullPath, out entry);
    }

    public UnixFileSystemEntry GetEntry(string fullPath)
    {
        ValidateFullPath(fullPath);
        return RootDirectory.GetEntry(fullPath);
    }
    
    private void ValidateFullPath(string fullPath)
    {
        ArgumentNullException.ThrowIfNull(fullPath);
        if (fullPath.Length == 0) throw new ArgumentException("The path cannot be empty", nameof(fullPath));
        if (!UnixPath.IsPathRooted(fullPath)) throw new ArgumentException("The path must be absolute and start with a `/`", nameof(fullPath));
    }
}