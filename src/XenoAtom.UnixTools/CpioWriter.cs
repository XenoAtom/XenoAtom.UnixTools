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
/// Provides a raw writer for CPIO archives. For a higher level API, use <see cref="UnixInMemoryFileSystem"/> and <see cref="UnixMemoryFileSystemExtensions.WriteTo(XenoAtom.UnixTools.UnixInMemoryFileSystem,XenoAtom.UnixTools.CpioWriter)"/>.
/// </summary>
public sealed unsafe class CpioWriter : IDisposable
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private bool _isDisposed;
    private byte[] _tempBuffer;
    private long _positionInSuperStream;

    private const int PaddingSize = 4; // Padding is 4 bytes at the end of file data / entry
    private const int MaxPadding = PaddingSize - 1; // The maximum padding is 3 bytes
    private const int NullTerminated1Byte = 1; // Null terminated string is 1 byte

    /// <summary>
    /// Creates a new instance of <see cref="CpioWriter"/>.
    /// </summary>
    /// <param name="stream">The uncompressed stream to write the CPIO archive.</param>
    /// <param name="leaveOpen">True to leave the stream open after the <see cref="CpioWriter"/> object is disposed; otherwise, false.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="stream"/> is null.</exception>
    public CpioWriter(Stream stream, bool leaveOpen = false)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _leaveOpen = leaveOpen;
        _tempBuffer = ArrayPool<byte>.Shared.Rent(1024); // Allocate enough space to fit by default the headers, the UTF8 name and the UTF8 link name with padding.
        _positionInSuperStream = stream.CanSeek ? stream.Position : 0;
    }

    /// <summary>
    /// Adds a new entry to the CPIO archive.
    /// </summary>
    /// <param name="entry">The entry to add.</param>
    /// <exception cref="ObjectDisposedException">If the writer has been disposed.</exception>
    /// <exception cref="ArgumentException">If <paramref name="entry"/> is invalid.</exception>
    public void AddEntry(in CpioEntry entry)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(CpioWriter));
        }

        // Validate the entry
        entry.Validate();

        var headerSize = sizeof(RawCpioEntry) + sizeof(RawCpioEntry2);
        var requiredLength = headerSize + Encoding.UTF8.GetByteCount(entry.Name) + 1 + MaxPadding + (entry.LinkName != null ? Encoding.UTF8.GetByteCount(entry.LinkName) + MaxPadding : 0);

        var tempBuffer = _tempBuffer;
        if (requiredLength > tempBuffer.Length)
        {
            ArrayPool<byte>.Shared.Return(tempBuffer);
            _tempBuffer = tempBuffer = ArrayPool<byte>.Shared.Rent(requiredLength);
        }

        ref var pTemp = ref MemoryMarshal.GetReference(new Span<byte>(tempBuffer));
        ref var rawEntry2 = ref Unsafe.As<byte, RawCpioEntry2>(ref pTemp);
        ref var rawEntry = ref Unsafe.As<byte, RawCpioEntry>(ref Unsafe.Add(ref pTemp, sizeof(RawCpioEntry2)));

        rawEntry2.c_magic = (uint)entry.EntryKind;
        rawEntry2.c_ino = entry.InodeNumber;
        rawEntry2.c_mode = (uint)entry.FileType | ((uint)entry.Mode & 0x1FF);
        rawEntry2.c_uid = entry.Uid;
        rawEntry2.c_gid = entry.Gid;
        rawEntry2.c_nlink = entry.HardLinkCount;
        rawEntry2.c_mtime = entry.InternalModificationTime;
        rawEntry2.c_filesize = entry.Length;
        rawEntry2.c_devmajor = entry.Dev.Major;
        rawEntry2.c_devminor = entry.Dev.Minor;
        rawEntry2.c_rdevmajor = entry.Device.Major;
        rawEntry2.c_rdevminor = entry.Device.Minor;
        rawEntry2.c_check = entry.Checksum;

        // Make sure we have a trailing null byte
        var spanData = new Span<byte>(tempBuffer, headerSize, tempBuffer.Length - headerSize);
        var encoded = Encoding.UTF8.GetBytes(entry.Name.AsSpan(), spanData);
        rawEntry2.c_namesize = (uint)encoded + NullTerminated1Byte;

        spanData[encoded] = 0;
        spanData = spanData.Slice(encoded + NullTerminated1Byte);

        var dataSize = RawCpioEntry.SizeOf + encoded + NullTerminated1Byte;
        var position = _positionInSuperStream + dataSize;
        // Pad the name to be aligned on 4 bytes
        var newPosition = AlignHelper.AlignUp(position, PaddingSize);
        if (newPosition != position)
        {
            var length = (int)(newPosition - position);
            for (int i = 0; i < length; i++)
            {
                Unsafe.Add(ref MemoryMarshal.GetReference(spanData), i) = 0;
            }

            spanData = spanData.Slice(length);
            dataSize += length;
            position = newPosition;
        }

        if (entry.LinkName != null)
        {
            encoded = Encoding.UTF8.GetBytes(entry.LinkName.AsSpan(), spanData);
            rawEntry2.c_filesize = (uint)encoded;
            dataSize += encoded;
            position += encoded;
        }
        else
        {
            if (entry.FileType == CpioFileType.RegularFile)
            {
                // DataStream has been validated to be not null for regular files
                var length = entry.DataStream?.Length ?? 0;
                rawEntry2.c_filesize = (uint)length;
                position += length;
            }
            else
            {
                rawEntry2.c_filesize = 0;
            }
        }

        // Convert the raw entry to hexadecimal
        rawEntry2.ConvertTo(out rawEntry);

        // Write the raw entry to the stream
        // We start at 2 bytes because the magic number is only 6 bytes while the struct is using 8 bytes to SIMD convert things more easily.
        _stream.Write(tempBuffer, sizeof(RawCpioEntry2) + 2, dataSize);

        if (entry.DataStream is not null)
        {
            entry.DataStream.CopyTo(_stream);
        }

        newPosition = AlignHelper.AlignUp(position, PaddingSize);
        if (newPosition != position)
        {
            uint zero = 0;
            var padding = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref zero, 1)).Slice(0, (int)(newPosition - position));
            _stream.Write(padding);
        }

        _positionInSuperStream = newPosition;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        ArrayPool<byte>.Shared.Return(_tempBuffer);
        _tempBuffer = [];

        WriteTrailer();

        if (!_leaveOpen)
            _stream.Dispose();
    }

    private void WriteTrailer()
    {
        // The 4 zero bytes at the end are:
        // - 1 byte for the null terminated string TRAILER!!!
        // - 3 bytes for the trailer padding
        // Total is 124 bytes = 120 bytes for the header + 1 byte for the null after the terminated string + 3 bytes for the trailer padding
        var trailer = "07070100000000000000000000000000000000000000010000000000000000000000000000000000000000000000000000000B00000000TRAILER!!!\x00\x00\x00\x00"u8;
        Debug.Assert(trailer.Length == 124);
        _stream.Write(trailer);
    }
}