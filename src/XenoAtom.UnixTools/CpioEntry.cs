// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.UnixTools;

/// <summary>
/// Defines a CPIO entry.
/// </summary>
public struct CpioEntry
{
    /// <summary>
    /// Creates a new instance of <see cref="CpioEntry"/>.
    /// </summary>
    public CpioEntry()
    {
        EntryKind = CpioEntryKind.NewAscii;
        Name = string.Empty;
        HardLinkCount = 1;
    }

    /// <summary>
    /// Gets or sets the offset of this entry in the CPIO archive.
    /// </summary>
    /// <remarks>
    /// This value is set by the <see cref="CpioReader"/> and is not used by the <see cref="CpioWriter"/>.
    /// </remarks>
    public long Offset { get; set; }

    /// <summary>
    /// Gets or sets the kind of this entry. Default is <see cref="CpioEntryKind.NewAscii"/>
    /// </summary>
    public CpioEntryKind EntryKind { get; set; }

    /// <summary>
    /// Gets or sets the inode number.
    /// </summary>
    public uint InodeNumber { get; set; }

    /// <summary>
    /// Gets or sets the file type.
    /// </summary>
    public CpioFileType FileType { get; set; }

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
    /// Gets or sets the number of hard links.
    /// </summary>
    /// <remarks>
    /// The value must be at least 1 for any kind of files and at least 2 for directories.
    /// </remarks>
    public uint HardLinkCount { get; set; }

    internal uint InternalModificationTime;

    /// <summary>
    /// Gets or sets the modification time of this file
    /// </summary>
    public DateTimeOffset ModificationTime
    {
        get => DateTimeOffset.FromUnixTimeSeconds(InternalModificationTime);
        set => InternalModificationTime = (uint)value.ToUnixTimeSeconds();
    }

    /// <summary>
    /// Gets or sets the length of the file data.
    /// </summary>
    /// <remarks>
    /// This value is set by the <see cref="CpioReader"/>, for when <see cref="LinkName"/> is not null or if <see cref="DataStream"/> is not null.
    /// This value must be set to 0 or equal to DataStream.Length when writing a CPIO archive.
    /// This value must be set to 0 for other cases (e.g. symbolic link, directory, etc...)
    /// </remarks>
    public uint Length { get; set; }

    /// <summary>
    /// Gets or sets the major/minor number of the dev.
    /// </summary>
    public DeviceId Dev { get; set; }

    /// <summary>
    /// Gets or sets the major/minor number for the real device.
    /// </summary>
    public DeviceId Device { get; set; }

    /// <summary>
    /// Gets or sets the checksum of the file.
    /// </summary>
    /// <remarks>
    /// This value must contain the cumulative byte-by-byte uint checksum of the stream (LinkName + DataStream).
    /// </remarks>
    public uint Checksum { get; set; }

    /// <summary>
    /// Gets or sets the name of the file.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the link name.
    /// </summary>
    /// <remarks>
    /// This value requires that the <see cref="FileType"/> is set to <see cref="CpioFileType.SymbolicLink"/>.
    /// If this value is not null, the <see cref="DataStream"/> must be null.
    /// </remarks>
    public string? LinkName { get; set; }

    /// <summary>
    /// Gets or sets the data stream.
    /// </summary>
    public Stream? DataStream { get; set; }

    /// <inheritdoc />
    public override string? ToString()
    {
        return Name;
    }
}