// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.UnixTools;

/// <summary>
/// Defines the mode of a Unix <see cref="UnixDirectory.CopyEntry"/> operation.
/// </summary>
public enum UnixCopyMode
{
    /// <summary>
    /// Copy a single file or directory.
    /// </summary>
    Single,

    /// <summary>
    /// Copy a directory recursively.
    /// </summary>
    Recursive,

    /// <summary>
    /// Copy a directory recursively by creating a hard link for each file/device.
    /// </summary>
    RecursiveWithHardLinks,

    /// <summary>
    /// Copy a directory recursively while remapping existing hard links to copied files.
    /// </summary>
    Archive
}