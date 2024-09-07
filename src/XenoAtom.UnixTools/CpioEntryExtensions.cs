// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.UnixTools;

public static class CpioEntryExtensions
{
    /// <summary>
    /// Validates the specified <see cref="CpioEntry"/>.
    /// </summary>
    /// <param name="entry">The entry to validate.</param>
    /// <exception cref="ArgumentException">If the entry is invalid</exception>
    public static void Validate(this CpioEntry entry)
    {
        if (string.IsNullOrEmpty(entry.Name))
        {
            throw new ArgumentException("File name is required", nameof(entry));
        }

        if (UnixPath.ContainsInvalidCharacters(entry.Name))
        {
            throw new ArgumentException("Invalid null char found in path", nameof(entry));
        }

        // If we try to  normalize the path, it should be the same as the original path
        var path = UnixPath.Normalize(entry.Name);
        if (!ReferenceEquals(path, entry.Name))
        {
            throw new ArgumentException("File name is not normalized", nameof(entry));
        }

        if (entry.Name.StartsWith("..", StringComparison.Ordinal))
        {
            throw new ArgumentException("File name cannot start with ..", nameof(entry));
        }

        if (entry.LinkName != null)
        {
            if (entry.LinkName.Length == 0)
            {
                throw new ArgumentException("LinkName cannot be empty", nameof(entry));
            }

            if (UnixPath.ContainsInvalidCharacters(entry.LinkName))
            {
                throw new ArgumentException("Invalid null char found in path", nameof(entry));
            }

            var linkPath = UnixPath.Normalize(entry.LinkName);
            if (!ReferenceEquals(linkPath, entry.LinkName))
            {
                throw new ArgumentException("LinkName is not normalized", nameof(entry));
            }
            
            if (entry.FileType != CpioFileType.SymbolicLink)
            {
                throw new ArgumentException("LinkName is only valid for symbolic links", nameof(entry));
            }

            if (entry.HardLinkCount != 1)
            {
                throw new ArgumentException($"HardLinkCount must be 1 for symbolic links", nameof(entry));
            }
        }
        else
        {
            switch (entry.FileType)
            {
                case CpioFileType.SymbolicLink:
                    throw new ArgumentException("LinkName is required for symbolic links", nameof(entry));
                case CpioFileType.RegularFile:
                {
                    if (entry.DataStream is null)
                    {
                        if (entry.Length != 0)
                        {
                            throw new ArgumentException("DataStream is required if length > 0 for regular files", nameof(entry));
                        }
                    }
                    else if (entry.Length != 0 && entry.DataStream.Length != entry.Length)
                    {
                        throw new ArgumentException("DataStream length is different from the specified length", nameof(entry));
                    }

                    if (entry.HardLinkCount < 1)
                    {
                        throw new ArgumentException("HardLinkCount must be at least 1 for regular files", nameof(entry));
                    }

                    break;
                }
                default:
                {
                    if (entry.DataStream is not null)
                    {
                        throw new ArgumentException($"DataStream is not allowed for the file type {entry.FileType}", nameof(entry));
                    }

                    if (entry.Length != 0)
                    {
                        throw new ArgumentException($"Length is not allowed for the file type {entry.FileType}", nameof(entry));
                    }

                    switch (entry.FileType)
                    {
                        case CpioFileType.Undefined:
                            throw new ArgumentException("FileType is required", nameof(entry));
                        case CpioFileType.Directory:
                        {
                            if (entry.HardLinkCount < 2)
                            {
                                throw new ArgumentException("HardLinkCount must be at least 2 for directories", nameof(entry));
                            }

                            break;
                        }
                        default:
                        {
                            if (entry.HardLinkCount != 1)
                            {
                                throw new ArgumentException("HardLinkCount must be 1 for non-directories", nameof(entry));
                            }

                            break;
                        }
                    }

                    break;
                }
            }
        }
    }
}