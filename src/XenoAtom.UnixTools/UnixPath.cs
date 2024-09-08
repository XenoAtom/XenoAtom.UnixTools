// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace XenoAtom.UnixTools;

public static class UnixPath
{
    public const char DirectorySeparatorChar = '/';
    public const string DirectorySeparatorCharAsString = "/";

    public static string Normalize(string path)
    {
        Validate(path);
        return NormalizeInternal(path);
    }

    /// <summary>Returns the directory information for the specified path represented by a character span.</summary>
    /// <param name="path">The path to retrieve the directory information from.</param>
    /// <returns>Directory information for <paramref name="path" />, or an empty span if <paramref name="path" /> is <see langword="null" />, an empty span, or a root (such as \, C:, or \\server\share).</returns>
    public static ReadOnlySpan<char> GetDirectoryName(ReadOnlySpan<char> path)
    {
        if (path.IsEmpty) return path;
        int directoryNameOffset = GetDirectoryNameOffset(path);
        return directoryNameOffset < 0 ? ReadOnlySpan<char>.Empty : path.Slice(0, directoryNameOffset);
    }

    internal static int GetDirectoryNameOffset(ReadOnlySpan<char> path)
    {
        int end = path.Length;
        if (end <= 0)
            return -1;

        while (end > 0 && !IsDirectorySeparator(path[--end]))
        {
        }

        // Trim off any remaining separators (to deal with C:\foo\\bar)
        while (end > 0 && IsDirectorySeparator(path[end - 1]))
        {
            end--;
        }

        return end;
    }
    
    public static ReadOnlySpan<char> GetFileName(ReadOnlySpan<char> path)
    {
        int index = path.LastIndexOf('/');
        return index < 0 ? path : path.Slice(index + 1);
    }
    
    public static ReadOnlySpan<char> GetFileNameWithoutExtension(ReadOnlySpan<char> path)
    {
        ReadOnlySpan<char> fileName = GetFileName(path);
        int length = fileName.LastIndexOf('.');
        return length < 0 ? fileName : fileName.Slice(0, length);
    }
    
    public static ReadOnlySpan<char> GetExtension(ReadOnlySpan<char> path)
    {
        int length = path.Length;
        for (int index = length - 1; index >= 0; --index)
        {
            char c = path[index];
            if (c == '.')
                return index != length - 1 ? path.Slice(index, length - index) : ReadOnlySpan<char>.Empty;
            if (IsDirectorySeparator(c))
                break;
        }
        return ReadOnlySpan<char>.Empty;
    }

    public static void Validate(string path) => Validate(path, nameof(path));

    public static void Validate(string path, string paramName)
    {
        ArgumentNullException.ThrowIfNull(path, paramName);

        if (ContainsInvalidCharacters(path))
            throw new ArgumentException("Invalid null char found in path", paramName);
    }

    public static void Validate(ReadOnlySpan<char> path, string paramName)
    {
        if (ContainsInvalidCharacters(path))
            throw new ArgumentException("Invalid null char found in path", paramName);
    }

    /// <summary>
    /// Checks if the path contains valid characters.
    /// </summary>
    /// <param name="path">The path to check</param>
    /// <returns>True if the path contains valid characters.</returns>
    public static bool ContainsInvalidCharacters(ReadOnlySpan<char> path) => path.IndexOf('\0') >= 0;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDirectorySeparator(char c) => c == DirectorySeparatorChar;

    public static string Combine(string path1, string path2)
    {
        Validate(path1);
        Validate(path2);

        if (path2.Length == 0) return path1;
        if (path1.Length == 0) return path2;

        if (IsPathRooted(path2)) return path2;

        if (path1[^1] != DirectorySeparatorChar)
        {
            return string.Create(path1.Length + path2.Length + 1, (path1, path2), static (span, state) =>
            {
                state.path1.AsSpan().CopyTo(span);
                span[state.path1.Length] = DirectorySeparatorChar;
                state.path2.AsSpan().CopyTo(span.Slice(state.path1.Length + 1));
            });
        }
        else
        {
            return string.Concat(path1, path2);
        }
    }

    /// <summary>
    /// Combines a span of strings into a path.
    /// </summary>
    /// <param name="paths">A span of parts of the path.</param>
    /// <returns>The combined paths.</returns>
    public static string Combine(ReadOnlySpan<string> paths)
    {
        int maxSize = 0;
        int firstComponent = 0;

        // We have two passes, the first calculates how large a buffer to allocate and does some precondition
        // checks on the paths passed in.  The second actually does the combination.

        for (int i = 0; i < paths.Length; i++)
        {
            Validate(paths[i], nameof(paths));

            if (paths[i].Length == 0)
            {
                continue;
            }

            if (IsPathRooted(paths[i]))
            {
                firstComponent = i;
                maxSize = paths[i].Length;
            }
            else
            {
                maxSize += paths[i].Length;
            }

            char ch = paths[i][^1];
            if (!IsDirectorySeparator(ch))
                maxSize++;
        }

        var builder = new ValueStringBuilder(stackalloc char[260]); // MaxShortPath on Windows
        builder.EnsureCapacity(maxSize);

        for (int i = firstComponent; i < paths.Length; i++)
        {
            if (paths[i].Length == 0)
            {
                continue;
            }

            if (builder.Length == 0)
            {
                builder.Append(paths[i]);
            }
            else
            {
                char ch = builder[^1];
                if (!IsDirectorySeparator(ch))
                {
                    builder.Append(DirectorySeparatorChar);
                }

                builder.Append(paths[i]);
            }
        }

        return builder.ToString();
    }



    public static bool IsPathRooted(ReadOnlySpan<char> path) => path.Length > 0 && path[0] == '/';

    internal static string NormalizeInternal(string path)
    {
        Debug.Assert(path is not null);
        Debug.Assert(!path.Contains('\0'));

        var sb = new ValueStringBuilder(stackalloc char[260 /* PathInternal.MaxShortPath */]);

        if (RemoveRelativeSegments(path.AsSpan(), ref sb))
        {
            path = sb.ToString();
        }

        sb.Dispose();
        return path;
    }
    

    /// <summary>
    /// Try to remove relative segments from the given path (without combining with a root).
    /// </summary>
    /// <param name="path">Input path</param>
    /// <param name="sb">String builder that will store the result</param>
    /// <returns>"true" if the path was modified</returns>
    internal static bool RemoveRelativeSegments(ReadOnlySpan<char> path, ref ValueStringBuilder sb)
    {
        int initialLength = path.Length;
        bool isAbsolute = path.Length > 0 && IsDirectorySeparator(path[0]);

        // Remove any leading ./
        while (path.StartsWith("./"))
        {
            path = path.Slice(2);

            // Remove consecutive slashes after removing ./
            while (path.Length > 0 && path[0] == '/')
            {
                path = path.Slice(1);
            }
        }

        int skip = isAbsolute ? 1 : 0;
        // We treat "\.." , "\." and "\\" as a relative segment. We want to collapse the first separator past the root presuming
        // the root actually ends in a separator. Otherwise the first segment for RemoveRelativeSegments
        // in cases like "\\?\C:\.\" and "\\?\C:\..\", the first segment after the root will be ".\" and "..\" which is not considered as a relative segment and hence not be removed.
        if (skip > 0 && IsDirectorySeparator(path[skip - 1]))
            skip--;

        // Remove "//", "/./", and "/../" from the path by copying each character to the output,
        // except the ones we're removing, such that the builder contains the normalized path
        // at the end.
        if (skip > 0)
        {
            sb.Append(path.Slice(0, skip));
        }
        
        for (int i = skip; i < path.Length; i++)
        {
            char c = path[i];

            if (IsDirectorySeparator(c) && i + 1 < path.Length)
            {
                // Skip this character if it's a directory separator and if the next character is, too,
                // e.g. "parent//child" => "parent/child"
                if (IsDirectorySeparator(path[i + 1]))
                {
                    continue;
                }

                // Skip this character and the next if it's referring to the current directory,
                // e.g. "parent/./child" => "parent/child"
                if ((i + 2 == path.Length || IsDirectorySeparator(path[i + 2])) &&
                    path[i + 1] == '.')
                {
                    i++;
                    continue;
                }

                // Skip this character and the next two if it's referring to the parent directory,
                // e.g. "parent/child/../grandchild" => "parent/grandchild"
                if (i + 2 < path.Length &&
                    (i + 3 == path.Length || IsDirectorySeparator(path[i + 3])) &&
                    path[i + 1] == '.' && path[i + 2] == '.')
                {
                    if (sb.Length >= 2 && sb[^1] == '.' && sb[^2] == '.' && (sb.Length == 2 || IsDirectorySeparator(sb[^3])))
                    {
                        // if we have already .. and it hasn't been skipped, that means that we have relatives path that cannot be removed
                    }
                    else
                    {
                        // Unwind back to the last slash (and if there isn't one, clear out everything).
                        int s;
                        for (s = sb.Length - 1; s >= skip; s--)
                        {
                            if (IsDirectorySeparator(sb[s]))
                            {
                                sb.Length = (i + 3 >= path.Length && s == skip) ? s + 1 : s; // to avoid removing the complete "\tmp\" segment in cases like \\?\C:\tmp\..\, C:\tmp\..
                                break;
                            }
                        }

                        if (s < skip)
                        {
                            sb.Length = skip;
                        }

                        i += 2 + (sb.Length == 0 && !isAbsolute ? 1 : 0);
                        continue;
                    }
                }
            }

            sb.Append(c);
        }

        // If we haven't changed the source path, return the original
        if (sb.Length == initialLength && initialLength > 0)
        {
            return false;
        }

        // We may have eaten the trailing separator from the root when we started and not replaced it
        if (sb.Length == 0)
        {
            sb.Append(isAbsolute ? '/' : '.');
        }

        return true;
    }

    public static string GetRelativePath(string sourcePath, string childFullPath)
    {
        Validate(sourcePath, nameof(sourcePath));
        Validate(childFullPath, nameof(childFullPath));

        if (sourcePath.Length == 0 || childFullPath.Length == 0)
        {
            return childFullPath;
        }

        if (sourcePath == childFullPath)
        {
            return ".";
        }

        if (sourcePath.Length == 1 && IsDirectorySeparator(sourcePath[0]))
        {
            return childFullPath;
        }

        if (childFullPath.Length == 1 && IsDirectorySeparator(childFullPath[0]))
        {
            return childFullPath;
        }

        int commonLength = 0;
        int lastSeparator = -1;
        for (int i = 0; i < sourcePath.Length; i++)
        {
            if (i >= childFullPath.Length)
            {
                break;
            }

            if (sourcePath[i] != childFullPath[i])
            {
                break;
            }

            if (IsDirectorySeparator(sourcePath[i]))
            {
                lastSeparator = i;
            }

            commonLength++;
        }

        if (commonLength == 0)
        {
            return childFullPath;
        }

        if (commonLength == sourcePath.Length && commonLength == childFullPath.Length)
        {
            return ".";
        }

        if (commonLength == sourcePath.Length && IsDirectorySeparator(childFullPath[commonLength]))
        {
            return childFullPath.Substring(commonLength + 1);
        }

        var sb = new ValueStringBuilder(stackalloc char[260 /* PathInternal.MaxShortPath */]);
        if (lastSeparator == -1)
        {
            sb.Append(childFullPath);
        }
        else
        {
            if (lastSeparator + 1 < sourcePath.Length)
            {
                sb.Append("..");
                sb.Append(DirectorySeparatorChar);
            }

            sb.Append(childFullPath.AsSpan().Slice(lastSeparator + 1));
        }

        return sb.ToString();

    }
}