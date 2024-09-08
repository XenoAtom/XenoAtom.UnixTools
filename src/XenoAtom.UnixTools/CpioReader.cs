// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace XenoAtom.UnixTools;

/// <summary>
/// Provides a reader for CPIO archives.
/// </summary>
public sealed unsafe class CpioReader : IDisposable
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private bool _isDisposed;
    private byte[] _tempBuffer;
    private long _positionInSuperStream;
    private SubReadStream? _previousDataStream;
    private int _nextSkipPaddingLength;

    /// <summary>
    /// Creates a new instance of <see cref="CpioReader"/>.
    /// </summary>
    /// <param name="stream">The uncompressed stream containing the CPIO archive.</param>
    /// <param name="leaveOpen">True to leave the stream open after the <see cref="CpioReader"/> object is disposed; otherwise, false.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="stream"/> is null.</exception>
    public CpioReader(Stream stream, bool leaveOpen = false)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _leaveOpen = leaveOpen;
        _tempBuffer = ArrayPool<byte>.Shared.Rent(512);
        _positionInSuperStream = stream.CanSeek ? stream.Position : 0;
    }

    /// <summary>
    /// Tries to get the next entry from the CPIO archive.
    /// </summary>
    /// <param name="entry">The output entry parsed from the archive.</param>
    /// <returns></returns>
    /// <exception cref="ObjectDisposedException">If the reader has been disposed.</exception>
    /// <exception cref="InvalidOperationException">If the previous data stream has not been fully read. This can happen if the underlying stream is not seekable.</exception>
    /// <exception cref="InvalidDataException">If the data read from the stream is invalid. (e.g Invalid hexadecimal).</exception>
    public bool TryGetNextEntry(out CpioEntry entry)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(CpioReader));
        }
        
        var position = _positionInSuperStream;

        // If we need to seek, we need to make sure that the stream is at the right position before reading the entry
        var stream = _stream;
        if (stream.CanSeek)
        {
            if (stream.Position != position)
            {
                stream.Seek(position, SeekOrigin.Begin);
            }
        }
        else if (_previousDataStream != null)
        {
            // If the stream is not seekable, we need to align on the expected position for the next entry
            var delta = position - _previousDataStream.PositionInSuperStream;
            Debug.Assert(delta >= 0);
            if (delta > 0)
            {
                if (delta > 3)
                {
                    throw new InvalidOperationException("Cannot read the next entry until the previous data stream has been fully read");
                }
                stream.ReadExactly(_tempBuffer, 0, (int)delta);
            }
        }
        else if (_nextSkipPaddingLength > 0)
        {
            stream.ReadExactly(_tempBuffer, 0, (int)_nextSkipPaddingLength);
        }

        ref var pTemp = ref MemoryMarshal.GetReference(new Span<byte>(_tempBuffer));
        ref var rawEntry = ref Unsafe.As<byte, RawCpioEntry>(ref pTemp);
        ref var rawEntry2 = ref Unsafe.As<byte, RawCpioEntry2>(ref Unsafe.Add(ref pTemp, sizeof(RawCpioEntry)));
        rawEntry.ReadFrom(stream);

        position += RawCpioEntry.SizeOf;

        var hasValidHexadecimal = rawEntry.TryConvertTo(out rawEntry2);
        if (!hasValidHexadecimal)
        {
            throw new InvalidDataException($"Invalid hexadecimal found at position {position} bytes from stream");
        }

        if (rawEntry2.c_magic != 0x070701 && rawEntry2.c_magic != 0x070702)
        {
            throw new InvalidDataException($"Invalid magic number 0x{rawEntry2.c_magic:x6} at position {position} bytes from stream");
        }

        // https://people.freebsd.org/~kientzle/libarchive/man/cpio.5.txt
        entry = new CpioEntry
        {
            EntryKind = (CpioEntryKind)rawEntry2.c_magic,
            InodeNumber = rawEntry2.c_ino,
            FileType = (CpioFileType)(rawEntry2.c_mode & ~0x1FF),
            Mode = (UnixFileMode)(rawEntry2.c_mode & 0x1FF),
            Uid = rawEntry2.c_uid,
            Gid = rawEntry2.c_gid,
            HardLinkCount = rawEntry2.c_nlink,
            InternalModificationTime = rawEntry2.c_mtime,
            Length = rawEntry2.c_filesize,
            Dev = new(rawEntry2.c_devmajor, rawEntry2.c_devminor),
            Device = new(rawEntry2.c_rdevmajor, rawEntry2.c_rdevminor),
            Checksum = rawEntry2.c_check,
            Name = string.Empty,
        };

        if (rawEntry2.c_namesize > int.MaxValue)
        {
            throw new InvalidDataException($"Invalid filename size {rawEntry2.c_namesize} cannot be larger than int.MaxValue");
        }

        int nameSize = (int)rawEntry2.c_namesize;
        uint fileSize = rawEntry2.c_filesize;
        if (nameSize == 0)
        {
            throw new InvalidOperationException("Invalid zero filename size");
        }

        // Resize the buffer if needed to process the name
        var tempBuffer = _tempBuffer;
        if (tempBuffer.Length < nameSize)
        {
            ArrayPool<byte>.Shared.Return(tempBuffer);
            _tempBuffer = tempBuffer = ArrayPool<byte>.Shared.Rent((int)nameSize);
        }

        stream.ReadExactly(tempBuffer, 0, (int)nameSize);
        position += nameSize;

        // The trailer is a special entry that is always present at the end of the archive
        bool isTrailer = nameSize - 1 == RawCpioEntry.Trailer.Length && tempBuffer.AsSpan(0, nameSize - 1).SequenceEqual(RawCpioEntry.Trailer);

        // We don't need to decode the trailer
        if (!isTrailer)
        {
            entry.Name = Encoding.UTF8.GetString(tempBuffer, 0, (int)nameSize - 1);
        }

        // Skip the padding (even for the trailer)
        long offset = AlignHelper.AlignUp(position, 4);
        if (offset != position)
        {
            stream.ReadExactly(tempBuffer, 0, (int)(offset - position));
            position = offset;
        }

        entry.Offset = position;

        _previousDataStream = null;

        if (!isTrailer)
        {
            // Decode the symbolic link
            if (entry.FileType == CpioFileType.SymbolicLink)
            {
                if (tempBuffer.Length < fileSize)
                {
                    ArrayPool<byte>.Shared.Return(tempBuffer);
                    _tempBuffer = tempBuffer = ArrayPool<byte>.Shared.Rent((int)fileSize);
                }

                stream.ReadExactly(tempBuffer, 0, (int)fileSize);

                entry.LinkName = Encoding.UTF8.GetString(tempBuffer, 0, (int)fileSize);
            }
            else if (entry.FileType == CpioFileType.RegularFile)
            {
                _previousDataStream = stream.CanSeek ? new SeekableSubReadStream(stream, position, fileSize) : new SubReadStream(stream, position, fileSize);
                entry.DataStream = _previousDataStream;
            }
            else
            {
                if (fileSize > 0)
                {
                    throw new InvalidDataException($"Invalid non-zero file size {fileSize} for entry of type {entry.FileType}");
                }
            }
        }
        else
        {
            if (fileSize > 0)
            {
                throw new InvalidDataException($"Invalid trailer entry with a non-zero file size {fileSize}");
            }
        }

        // Calculate the position for the next entry
        offset = AlignHelper.AlignUp(position + fileSize, 4);

        // Skip the padding for the symbolic link or the trailer (because this is the last expected entry)
        // For a data stream, it will be skipped on the next read
        if (isTrailer || (entry.FileType == CpioFileType.SymbolicLink && offset != position))
        {
            // Skip the padding for the symbolic link
            stream.ReadExactly(tempBuffer, 0, (int)(offset - position));
            _nextSkipPaddingLength = 0;
        }
        else
        {
            _nextSkipPaddingLength = (int)(offset - position);
        }
        _positionInSuperStream = offset;
        
        return !isTrailer;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        ArrayPool<byte>.Shared.Return(_tempBuffer);
        _tempBuffer = [];

        if (!_leaveOpen)
            _stream.Dispose();
    }
}