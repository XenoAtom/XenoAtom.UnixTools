// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.UnixTools;

/// <summary>
/// A Unix symbolic link.
/// </summary>
public sealed class UnixSymbolicLink : UnixFileSystemEntry
{
    /// <summary>
    /// The default permission mode for a symbolic link.
    /// </summary>
    public const UnixFileMode DefaultMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                                            UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                                            UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
    
    internal UnixSymbolicLink(string name, UnixInode node) : base(name, node)
    {
        Mode = DefaultMode;
    }

    /// <summary>
    /// Gets or sets the relative target path of the symbolic link.
    /// </summary>
    public string Target
    {
        get => Inode.GetSymbolicLinkTarget();
        set => Inode.SetSymbolicLinkTarget(value);
    }

    /// <summary>
    /// Gets the full path of the target of this symbolic link.
    /// </summary>
    public string TargetFullPath
    {
        get => IsAttached ? UnixPath.Combine(Parent!.FullPath, Target) : "<undefined>";
    }
}