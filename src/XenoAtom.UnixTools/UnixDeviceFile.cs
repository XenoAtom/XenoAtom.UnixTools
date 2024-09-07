// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.UnixTools;

public sealed class UnixDeviceFile : UnixFileSystemEntry
{
    internal UnixDeviceFile(string name, UnixInode node) : base(name, node)
    {
    }

    /// <summary>
    /// Gets or sets the major number of the associated device.
    /// </summary>
    public DeviceId Device
    {
        get => Inode.GetDeviceId();
        set => Inode.SetDeviceId(value);
    }

    internal override UnixFileSystemEntry CloneWithName(string name) => new UnixDeviceFile(name, Inode);
}