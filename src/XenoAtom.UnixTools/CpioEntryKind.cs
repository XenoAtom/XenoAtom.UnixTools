// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.UnixTools;

/// <summary>
/// Supported CPIO entry kinds.
/// </summary>
public enum CpioEntryKind : uint
{
    /// <summary>
    /// The entry is invalid.
    /// </summary>
    Invalid = 0,

    /// <summary>
    /// The entry is a new ASCII entry. This is the default entry kind.
    /// </summary>
    NewAscii = 0x070701,

    /// <summary>
    /// The entry is a new ASCII entry with a checksum.
    /// </summary>
    NewAsciiChecksum = 0x070702,
}