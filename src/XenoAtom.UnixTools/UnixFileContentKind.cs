// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.UnixTools;

/// <summary>
/// Defines the kind of content for a Unix file.
/// </summary>
public enum UnixFileContentKind
{
    /// <summary>
    /// The content is empty.
    /// </summary>
    Empty,

    /// <summary>
    /// The content is a string.
    /// </summary>
    String,

    /// <summary>
    /// The content is a byte array.
    /// </summary>
    ByteArray,

    /// <summary>
    /// The content is a stream.
    /// </summary>
    Stream,

    /// <summary>
    /// The content is a function returning a string (<see cref="Func{TResult}"/>).
    /// </summary>
    FuncString,

    /// <summary>
    /// The content is a function returning a byte array (<see cref="Func{TResult}"/>).
    /// </summary>
    FuncByteArray,

    /// <summary>
    /// The content is a <see cref="Func{Stream}"/> (<see cref="Func{TResult}"/>).
    /// </summary>
    FuncStream
}