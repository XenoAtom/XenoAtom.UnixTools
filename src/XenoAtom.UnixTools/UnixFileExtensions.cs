// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text;

namespace XenoAtom.UnixTools;

/// <summary>
/// Defines extension methods for <see cref="UnixFile"/>.
/// </summary>
public static class UnixFileExtensions
{
    /// <summary>
    /// Reads all the bytes from the content of this file.
    /// </summary>
    /// <param name="content">The Unix content of this file.</param>
    /// <param name="encoding">The encoding to use to convert string content.</param>
    /// <returns>The content as a byte array.</returns>
    public static byte[] ReadAllBytes(this UnixFileContent content, Encoding? encoding = null)
    {
        var stream = new MemoryStream();
        encoding ??= Encoding.UTF8;
        content.CopyTo(stream, encoding);
        return stream.ToArray();
    }

    /// <summary>
    /// Reads all the text from the content of this file.
    /// </summary>
    /// <param name="content">The Unix content of this file.</param>
    /// <param name="encoding">The encoding to use to convert string content.</param>
    /// <returns>The content as a text.</returns>
    public static string ReadAllText(this UnixFileContent content, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        var stream = new MemoryStream();
        content.CopyTo(stream);
        return encoding.GetString(stream.ToArray());
    }

    /// <summary>
    /// Reads all the bytes from the content of this file.
    /// </summary>
    /// <param name="file">The Unix file.</param>
    /// <param name="encoding">The encoding to use to convert string content.</param>
    /// <returns>The content as a byte array.</returns>
    public static byte[] ReadAllBytes(this UnixFile file, Encoding? encoding = null) => file.Content.ReadAllBytes(encoding);

    /// <summary>
    /// Reads all the text from the content of this file.
    /// </summary>
    /// <param name="file">The Unix file.</param>
    /// <param name="encoding">The encoding to use to convert string content.</param>
    /// <returns>The content as a text.</returns>
    public static string ReadAllText(this UnixFile file, Encoding? encoding = null) => file.Content.ReadAllText(encoding);
}