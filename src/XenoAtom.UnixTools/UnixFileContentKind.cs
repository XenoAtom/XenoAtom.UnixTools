// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.UnixTools;

public enum UnixFileContentKind
{
    Empty,
    String,
    ByteArray,
    Stream,
    FuncString,
    FuncByteArray,
    FuncStream
}