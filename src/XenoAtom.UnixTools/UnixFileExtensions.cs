// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text;

namespace XenoAtom.UnixTools;

public static class UnixFileExtensions
{
    public static byte[] ReadAllBytes(this UnixFileContent content)
    {
        var stream = new MemoryStream();
        content.CopyTo(stream);
        return stream.ToArray();
    }
    public static string ReadAllText(this UnixFileContent content, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        var stream = new MemoryStream();
        content.CopyTo(stream);
        return encoding.GetString(stream.ToArray());
    }

    public static byte[] ReadAllBytes(this UnixFile file) => file.Content.ReadAllBytes();

    public static string ReadAllText(this UnixFile file, Encoding? encoding = null) => file.Content.ReadAllText(encoding);
}