// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.UnixTools;

/// <summary>
/// Defines a CPIO file type.
/// </summary>
public enum CpioFileType
{
    /// <summary>
    /// The file type is undefined.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// File type value for sockets.
    /// </summary>
    /// <remarks>
    /// Octal value: 0140000
    /// </remarks>
    Socket = 0xE000,

    /// <summary>
    /// File type value for symbolic links.
    /// </summary>
    /// <remarks>
    /// Octal value: 0120000
    /// </remarks>
    SymbolicLink = 0xA000,

    /// <summary>
    /// File type value for regular files.
    /// </summary>
    /// <remarks>
    /// Octal value: 0100000
    /// </remarks>
    RegularFile = 0x8000,

    /// <summary>
    /// File type value for block special devices.
    /// </summary>
    /// <remarks>
    /// Octal value: 0060000
    /// </remarks>
    BlockSpecialDevice = 0x6000,

    /// <summary>
    /// File type value for directories.
    /// </summary>
    /// <remarks>
    /// Octal value: 0040000
    /// </remarks>
    Directory = 0x4000,

    /// <summary>
    /// File type value for character special devices.
    /// </summary>
    /// <remarks>
    /// Octal value: 0020000
    /// </remarks>
    CharacterSpecialDevice = 0x2000,

    /// <summary>
    /// File type value for named pipes or FIFOs.
    /// </summary>
    /// <remarks>
    /// Octal value: 0010000
    /// </remarks>
    NamedPipe = 0x1000
}