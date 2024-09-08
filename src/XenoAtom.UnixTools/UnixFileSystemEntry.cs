// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Diagnostics;
using System.Text;

namespace XenoAtom.UnixTools;

/// <summary>
/// Base class for a Unix file system entry.
/// </summary>
public abstract class UnixFileSystemEntry
{
    // ReSharper disable ConvertToAutoProperty
    private string _name;
    private readonly UnixInode _inode;
    private UnixDirectory? _parent;
    private UnixInMemoryFileSystem? _fileSystem;

    internal UnixFileSystemEntry(string name, UnixInode node)
    {
        _name = name;
        _inode = node;
    }

    /// <summary>
    /// Gets or sets the name of this entry.
    /// </summary>
    public string Name
    {
        get => _name;
    }

    /// <summary>
    /// Parent folder.
    /// </summary>
    public UnixDirectory? Parent
    {
        get => _parent;
    }

    /// <summary>
    /// Gets the full path of this entry.
    /// </summary>
    public string FullPath
    {
        get
        {
            if (!IsAttached) throw new InvalidOperationException("This entry is not attached to a file system");
            if (Parent is null)
            {
                Debug.Assert(Name.Length == 0);
                return "/";
            }
            
            var sb = new ValueStringBuilder();
            AppendFullName(ref sb);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Gets the inode of this entry.
    /// </summary>
    public UnixInode Inode => _inode;

    /// <summary>
    /// Gets the filesystem of this entry.
    /// </summary>
    /// <summary>
    /// Gets the file system associated to this entry.
    /// </summary>
    public UnixInMemoryFileSystem? FileSystem
    {
        get => _fileSystem;
        internal set => _fileSystem = value;
    }

    /// <summary>
    /// Gets the file type.
    /// </summary>
    public UnixFileKind FileKind => Inode.FileKind;

    /// <summary>
    /// Gets or sets the file mode.
    /// </summary>
    public UnixFileMode Mode
    {
        get => Inode.Mode;
        set => Inode.Mode = value;
    }

    /// <summary>
    /// Gets or sets the user id.
    /// </summary>
    public uint Uid
    {
        get => Inode.Uid;
        set => Inode.Uid = value;
    }

    /// <summary>
    /// Gets or sets the group id.
    /// </summary>
    public uint Gid
    {
        get => Inode.Gid;
        set => Inode.Gid = value;
    }

    /// <summary>
    /// Gets or sets the creation time of this inode.
    /// </summary>
    public DateTimeOffset CreationTime
    {
        get => Inode.CreationTime;
        set => Inode.CreationTime = value;
    }

    /// <summary>
    /// Gets or sets the last time the properties of this inode were changed.
    /// </summary>
    public DateTimeOffset LastChangedTime
    {
        get => Inode.LastChangedTime;
        set => Inode.LastChangedTime = value;
    }

    /// <summary>
    /// Gets or sets the last access time of the content of this file.
    /// </summary>
    public DateTimeOffset LastAccessTime
    {
        get => Inode.LastAccessTime;
        set => Inode.LastAccessTime = value;
    }

    /// <summary>
    /// Gets or sets the last modification time of the content of this file.
    /// </summary>
    public DateTimeOffset LastModifiedTime
    {
        get => Inode.LastModifiedTime;
        set => Inode.LastModifiedTime = value;
    }

    /// <summary>
    /// Gets or sets the major number of the dev.
    /// </summary>
    public DeviceId Dev
    {
        get => Inode.Dev;
        set => Inode.Dev = value;
    }
    
    /// <summary>
    /// Gets the number of hard links to this entry.
    /// </summary>
    public uint HardLinkCount
    {
        get => Inode.HardLinkCount;
        internal set => Inode.HardLinkCount = value;
    }

    /// <summary>
    /// Deletes this entry from the file system.
    /// </summary>
    /// <exception cref="InvalidOperationException">If this is a root folder or the entry has been already deleted.</exception>
    public void Delete()
    {
        VerifyAttached();
        if (this is UnixDirectory directory && directory.IsRoot) throw new InvalidOperationException("Cannot delete the root folder");
        if (!IsAttached) throw new InvalidOperationException("Cannot delete an entry already removed from a file system");
        Parent!.DeleteEntry(Name);
    }

    /// <summary>
    /// Gets a value indicating whether this entry is attached to a file system.
    /// </summary>
    public bool IsAttached => FileSystem != null;

    /// <summary>
    /// Verifies that this entry is attached to a file system.
    /// </summary>
    /// <exception cref="InvalidOperationException">If this entry is not attached to a file system</exception>
    public void VerifyAttached()
    {
        if (!IsAttached) throw new InvalidOperationException("This entry is not attached to a file system");
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return FullPath;
    }

    internal void SetParent(UnixDirectory? newParent, string? newName = null)
    {
        var previousParent = _parent;
        newName ??= Name;
        if (newParent == previousParent && newName == Name)
        {
            return;
        }

        if (newParent is null)
        {
            if (this is UnixDirectory directory)
            {
                foreach (var child in directory.Entries.ToList())
                {
                    child.SetParent(null);
                }
            }

            FileSystem = null;
        }
        else
        {
            if (newParent.InternalEntries.ContainsKey(newName))
            {
                throw new InvalidOperationException($"An entry with the name `{newName}` already exists in the directory `{newParent.FullPath}`");
            }
        }



        _parent = newParent;
        FileSystem = newParent?.FileSystem;

        if (previousParent is not null)
        {
            previousParent.InternalEntries.Remove(Name);

            // Decrement the parent hardlink count if the entry is a directory
            // because of the implicit `..` entry in the subdirectory
            if (FileKind == UnixFileKind.Directory)
            {
                previousParent.Inode.HardLinkCount--;
            }

            // Decrease the hard link count
            Inode.HardLinkCount--;

            // If the hard link count is 0 or 1 (for directory), the node is not used anymore, so we can remove the inode from the filesystem
            if (Inode.HardLinkCount == 0 || (FileKind == UnixFileKind.Directory && Inode.HardLinkCount == 1))
            {
                Inode.HardLinkCount = 0;
            }
        }

        if (newParent is not null)
        {
            Inode.HardLinkCount++;
            newParent.InternalEntries.Add(newName, this);
            _name = newName;

            // Increment the parent hardlink count if the entry is a directory
            // because of the implicit `..` entry in the subdirectory
            if (FileKind == UnixFileKind.Directory)
            {
                // For a directory, the initial hardlink count is 2 (one for the directory, one for `.`)
                if (Inode.HardLinkCount == 1)
                {
                    Inode.HardLinkCount = 2;
                }
                newParent.Inode.HardLinkCount++;
            }
        }
    }

    /// <summary>
    /// Appends the full name of this entry to the specified string builder.
    /// </summary>
    /// <param name="sb"></param>
    private void AppendFullName(ref ValueStringBuilder sb)
    {
        var parent = Parent;
        if (parent != null)
        {
            parent.AppendFullName(ref sb);

            if (!parent.IsRoot)
            {
                sb.Append('/');
            }

            sb.Append(Name);
        }
        else
        {
            if (Name.Length == 0)
            {
                sb.Append('/');
            }
        }
    }
}