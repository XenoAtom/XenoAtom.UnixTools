// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

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
    
    public UnixDirectory CreateDirectory(string fullPath, bool createIntermediateDirectories = true)
    {
        ValidateFullPath(fullPath);
        return RootDirectory.CreateDirectory(fullPath, createIntermediateDirectories);
    }
    
    public UnixFile CreateFile(string fullPath, bool createIntermediateDirectories = true) => CreateFile(fullPath, UnixFileContent.Empty, createIntermediateDirectories);

    public UnixFile CreateFile(string fullPath, UnixFileContent content, bool createIntermediateDirectories = true)
    {
        ValidateFullPath(fullPath);
        return RootDirectory.CreateFile(fullPath, content, createIntermediateDirectories);
    }

    public UnixDeviceFile CreateDevice(string fullPath, UnixFileKind kind, DeviceId id, bool createIntermediateDirectories = true)
    {
        ValidateFullPath(fullPath);
        return RootDirectory.CreateDevice(fullPath, kind, id, createIntermediateDirectories);
    }

    public UnixSymbolicLink CreateSymbolicLink(string fullPath, string target, bool createIntermediateDirectories = true)
    {
        ValidateFullPath(fullPath);
        return RootDirectory.CreateSymbolicLink(fullPath, target, createIntermediateDirectories);
    }

    public TEntry CreateHardLink<TEntry>(string fullPath, TEntry entry, bool createIntermediateDirectories = true) where TEntry : UnixFileSystemEntry
    {
        ValidateFullPath(fullPath);
        return RootDirectory.CreateHardLink(fullPath, entry, createIntermediateDirectories);
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

    public void DeleteEntry(string fullPath)
    {
        ValidateFullPath(fullPath);
        RootDirectory.DeleteEntry(fullPath);
    }

    public void CopyEntry(string sourcePath, string destinationPath, UnixCopyMode mode = UnixCopyMode.Single)
    {
        ValidateFullPath(sourcePath, nameof(sourcePath));
        ValidateFullPath(destinationPath, nameof(destinationPath));
        RootDirectory.CopyEntry(sourcePath, destinationPath, mode);
    }

    public void MoveEntry(string sourcePath, string destinationPath, bool createIntermediateDirectories = false)
    {
        ValidateFullPath(sourcePath, nameof(sourcePath));
        ValidateFullPath(destinationPath, nameof(destinationPath));
        RootDirectory.MoveEntry(sourcePath, destinationPath, createIntermediateDirectories);
    }

    private static void ValidateFullPath(string fullPath, string? paramName = "fullPath")
    {
        ArgumentNullException.ThrowIfNull(fullPath);
        if (fullPath.Length == 0) throw new ArgumentException("The path cannot be empty", paramName);
        if (!UnixPath.IsPathRooted(fullPath)) throw new ArgumentException("The path must be absolute and start with a `/`", paramName);
    }
}