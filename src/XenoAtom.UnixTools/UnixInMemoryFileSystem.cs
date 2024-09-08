// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace XenoAtom.UnixTools;

/// <summary>
/// An in-memory Unix file system.
/// </summary>
public sealed class UnixInMemoryFileSystem
{
    /// <summary>
    /// Creates a new instance of <see cref="UnixInMemoryFileSystem"/>.
    /// </summary>
    public UnixInMemoryFileSystem()
    {
        NextInodeIndex = 1; // We start at 2 to avoid the root inode 1
        RootDirectory = UnixDirectory.CreateRoot(this);
    }

    internal long NextInodeIndex { get; set; }

    /// <summary>
    /// Gets the root directory of this file system.
    /// </summary>
    public UnixDirectory RootDirectory { get; }
    
    /// <summary>
    /// Creates a new file at the specified path.
    /// </summary>
    /// <param name="fullPath">An absolute path.</param>
    /// <param name="createIntermediateDirectories">A boolean indicating whether intermediate directories should be created.</param>
    /// <returns>The created file.</returns>
    public UnixFile CreateFile(string fullPath, bool createIntermediateDirectories = true) => CreateFile(fullPath, UnixFileContent.Empty, createIntermediateDirectories);

    /// <summary>
    /// Creates a new file at the specified path.
    /// </summary>
    /// <param name="fullPath">An absolute path.</param>
    /// <param name="content"></param>
    /// <param name="createIntermediateDirectories">A boolean indicating whether intermediate directories should be created.</param>
    /// <returns>The created file.</returns>
    public UnixFile CreateFile(string fullPath, UnixFileContent content, bool createIntermediateDirectories = true)
    {
        ValidateFullPath(fullPath);
        return RootDirectory.CreateFile(fullPath, content, createIntermediateDirectories);
    }

    /// <summary>
    /// Creates a new directory at the specified path.
    /// </summary>
    /// <param name="fullPath">An absolute path.</param>
    /// <param name="createIntermediateDirectories">A boolean indicating whether intermediate directories should be created.</param>
    /// <returns>The created directory.</returns>
    public UnixDirectory CreateDirectory(string fullPath, bool createIntermediateDirectories = true)
    {
        ValidateFullPath(fullPath);
        return RootDirectory.CreateDirectory(fullPath, createIntermediateDirectories);
    }

    /// <summary>
    /// Creates a new device at the specified path.
    /// </summary>
    /// <param name="fullPath">An absolute path.</param>
    /// <param name="kind">The kind of device file. The value must be <see cref="UnixFileKind.CharacterSpecialDevice"/> or <see cref="UnixFileKind.BlockSpecialDevice"/></param>
    /// <param name="id">The device id.</param>
    /// <param name="createIntermediateDirectories">A boolean indicating whether intermediate directories should be created.</param>
    /// <returns>The created device.</returns>
    public UnixDeviceFile CreateDevice(string fullPath, UnixFileKind kind, DeviceId id, bool createIntermediateDirectories = true)
    {
        ValidateFullPath(fullPath);
        return RootDirectory.CreateDevice(fullPath, kind, id, createIntermediateDirectories);
    }

    /// <summary>
    /// Creates a new symbolic link at the specified path.
    /// </summary>
    /// <param name="fullPath">An absolute path.</param>
    /// <param name="target">The target of the symbolic link.</param>
    /// <param name="createIntermediateDirectories">A boolean indicating whether intermediate directories should be created.</param>
    /// <returns>The created device.</returns>
    public UnixSymbolicLink CreateSymbolicLink(string fullPath, string target, bool createIntermediateDirectories = true)
    {
        ValidateFullPath(fullPath);
        return RootDirectory.CreateSymbolicLink(fullPath, target, createIntermediateDirectories);
    }

    /// <summary>
    /// Creates a new hard link at the specified path.
    /// </summary>
    /// <typeparam name="TEntry">The type of the target entry.</typeparam>
    /// <param name="fullPath">An absolute path.</param>
    /// <param name="target">The target of the hardlink.</param>
    /// <param name="createIntermediateDirectories">A boolean indicating whether intermediate directories should be created.</param>
    /// <returns>The created entry.</returns>
    public TEntry CreateHardLink<TEntry>(string fullPath, TEntry target, bool createIntermediateDirectories = true) where TEntry : UnixFileSystemEntry
    {
        ValidateFullPath(fullPath);
        return RootDirectory.CreateHardLink(fullPath, target, createIntermediateDirectories);
    }

    /// <summary>
    /// Tries to get an entry at the specified path.
    /// </summary>
    /// <param name="fullPath">An absolute path.</param>
    /// <param name="entry">The entry if found.</param>
    /// <returns>True if the entry is found, false otherwise.</returns>
    public bool TryGetEntry(string fullPath, [NotNullWhen(true)] out UnixFileSystemEntry? entry)
    {
        ValidateFullPath(fullPath);
        return RootDirectory.TryGetEntry(fullPath, out entry);
    }

    /// <summary>
    /// Gets an entry at the specified path.
    /// </summary>
    /// <param name="fullPath">An absolute path.</param>
    /// <returns>The entry.</returns>
    public UnixFileSystemEntry GetEntry(string fullPath)
    {
        ValidateFullPath(fullPath);
        return RootDirectory.GetEntry(fullPath);
    }

    /// <summary>
    /// Deletes an entry at the specified path.
    /// </summary>
    /// <param name="fullPath">An absolute path.</param>
    public void DeleteEntry(string fullPath)
    {
        ValidateFullPath(fullPath);
        RootDirectory.DeleteEntry(fullPath);
    }

    /// <summary>
    /// Copies an entry from the source path to the destination path.
    /// </summary>
    /// <param name="sourcePath">The source path.</param>
    /// <param name="destinationPath">The destination path.</param>
    /// <param name="mode">The copy mode.</param>
    /// <remarks>
    /// If the destination path is a directory, the source entry is coped into it.
    /// </remarks>
    public void CopyEntry(string sourcePath, string destinationPath, UnixCopyMode mode = UnixCopyMode.Single)
    {
        ValidateFullPath(sourcePath, nameof(sourcePath));
        ValidateFullPath(destinationPath, nameof(destinationPath));
        RootDirectory.CopyEntry(sourcePath, destinationPath, mode);
    }

    /// <summary>
    /// Moves an entry from the source path to the destination path.
    /// </summary>
    /// <param name="sourcePath">The source path.</param>
    /// <param name="destinationPath">The destination path.</param>
    /// <param name="createIntermediateDirectories">A boolean indicating whether intermediate directories should be created.</param>
    /// <remarks>
    /// If the destination path is a directory, the source entry is moved into it.
    /// </remarks>
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