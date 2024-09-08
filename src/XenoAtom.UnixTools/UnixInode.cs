// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Diagnostics;

namespace XenoAtom.UnixTools;

/// <summary>
/// Defines a Unix Inode.
/// </summary>
public sealed class UnixInode
{
    private object? _content;
    private DeviceId _deviceId;

    internal UnixInode(long index, UnixFileKind fileKind, object? content = null)
    {
        Index = index;
        CreationTime = DateTimeOffset.UtcNow;
        LastChangedTime = CreationTime;
        LastAccessTime = CreationTime;
        LastModifiedTime = CreationTime;
        FileKind = fileKind;
        _content = content;
    }

    /// <summary>
    /// Gets the index of this inode.
    /// </summary>
    public long Index { get; internal set; }

    /// <summary>
    /// Gets the file type.
    /// </summary>
    public UnixFileKind FileKind { get; }
    
    /// <summary>
    /// Gets or sets the file mode.
    /// </summary>
    public UnixFileMode Mode { get; set; }

    /// <summary>
    /// Gets or sets the user id.
    /// </summary>
    public uint Uid { get; set; }

    /// <summary>
    /// Gets or sets the group id.
    /// </summary>
    public uint Gid { get; set; }

    /// <summary>
    /// Gets or sets the creation time of this inode.
    /// </summary>
    public DateTimeOffset CreationTime { get; set; }

    /// <summary>
    /// Gets or sets the last time the properties of this inode were changed.
    /// </summary>
    public DateTimeOffset LastChangedTime { get; set; }

    /// <summary>
    /// Gets or sets the last access time of the content of this file.
    /// </summary>
    public DateTimeOffset LastAccessTime { get; set; }
    
    /// <summary>
    /// Gets or sets the last modification time of the content of this file.
    /// </summary>
    public DateTimeOffset LastModifiedTime { get; set; }
    
    /// <summary>
    /// Gets or sets the major number of the dev.
    /// </summary>
    public DeviceId Dev { get; set; }

    /// <summary>
    /// Gets the number of hard links to this entry.
    /// </summary>
    public uint HardLinkCount { get; internal set; }

    internal string GetSymbolicLinkTarget() => (string)_content!;

    internal void SetSymbolicLinkTarget(string target) => _content = target;


    internal UnixFileContent GetFileContent()
    {
        Debug.Assert(FileKind == UnixFileKind.RegularFile);
        return new UnixFileContent(_content);
    }

    internal void SetFileContent(UnixFileContent fileContent)
    {
        Debug.Assert(FileKind == UnixFileKind.RegularFile);
        _content = fileContent.Data;
    }

    internal SortedDictionary<string, UnixFileSystemEntry> GetDictionaryContent()
    {
        Debug.Assert(FileKind == UnixFileKind.Directory);
        return (SortedDictionary<string, UnixFileSystemEntry>)_content!;
    }

    internal DeviceId GetDeviceId()
    {
        Debug.Assert(FileKind == UnixFileKind.CharacterSpecialDevice || FileKind == UnixFileKind.BlockSpecialDevice);
        return _deviceId;
    }

    internal void SetDeviceId(DeviceId id)
    {
        Debug.Assert(FileKind == UnixFileKind.CharacterSpecialDevice || FileKind == UnixFileKind.BlockSpecialDevice);
        _deviceId = id;
    }

    internal UnixInode CreateCopy(UnixInMemoryFileSystem fs)
    {
        object? content = FileKind switch
        {
            UnixFileKind.Directory => new SortedDictionary<string, UnixFileSystemEntry>(), // Create an empty directory (will be copied later)
            UnixFileKind.RegularFile => GetFileContent().CreateCopy().Data,
            UnixFileKind.SymbolicLink => GetSymbolicLinkTarget(),
            UnixFileKind.CharacterSpecialDevice => null,
            UnixFileKind.BlockSpecialDevice => null,
            _ => throw new ArgumentOutOfRangeException()
        };
        
        var copy = new UnixInode(fs.NextInodeIndex++, FileKind, content)
        {
            Mode = Mode,
            Uid = Uid,
            Gid = Gid,
            CreationTime = CreationTime,
            LastChangedTime = LastChangedTime,
            LastAccessTime = LastAccessTime,
            LastModifiedTime = LastModifiedTime,
            Dev = Dev,
            HardLinkCount = 0 // Reset the hard link count
        };

        // Copy the DeviceId if it is a device
        if (FileKind == UnixFileKind.CharacterSpecialDevice || FileKind == UnixFileKind.BlockSpecialDevice)
        {
            copy.SetDeviceId(GetDeviceId());
        }
        
        return copy;
    }
}