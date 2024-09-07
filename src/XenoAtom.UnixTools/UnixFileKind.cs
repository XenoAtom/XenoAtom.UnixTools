// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.UnixTools;

/// <summary>
/// Unix file type.
/// </summary>
public enum UnixFileKind
{
    /// <summary>
    /// The file type is undefined.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// File type value for directories.
    /// </summary>
    Directory,

    /// <summary>
    /// File type value for regular files.
    /// </summary>
    RegularFile,

    /// <summary>
    /// File type value for symbolic links.
    /// </summary>
    SymbolicLink,
    
    /// <summary>
    /// File type value for block special devices.
    /// </summary>
    BlockSpecialDevice,

    /// <summary>
    /// File type value for character special devices.
    /// </summary>
    CharacterSpecialDevice,
}