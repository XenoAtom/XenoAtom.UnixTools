// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.UnixTools;

public sealed class UnixFile : UnixFileSystemEntry
{
    public const UnixFileMode DefaultMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

    internal UnixFile(string name, UnixInode node) : base(name, node)
    {
    }
    
    /// <summary>
    /// Can be byte[], or a Stream or a string or a Func&lt;Stream&gt;
    /// </summary>
    public UnixFileContent Content
    {
        get => Inode.GetFileContent();
        set => Inode.SetFileContent(value);
    }
}