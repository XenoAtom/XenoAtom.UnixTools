// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.
//
// License note: This file is a modified version of the System.Formats.Tar.SubReadStream class from .NET Core runtime with MIT License.

using System.Diagnostics;

namespace XenoAtom.UnixTools;

// Stream that allows wrapping a super stream and specify the lower and upper limits that can be read from it.
// It is meant to be used when the super stream is unseekable.
// Does not support writing.
internal class SubReadStream : Stream
{
    private protected bool _hasReachedEnd;
    private protected readonly long _startInSuperStream;
    internal long PositionInSuperStream;
    private protected readonly long _endInSuperStream;
    private protected readonly Stream _superStream;
    private protected bool _isDisposed;

    public SubReadStream(Stream superStream, long startPosition, long maxLength)
    {
        if (!superStream.CanRead)
        {
            throw new ArgumentException(SR.IO_NotSupported_UnreadableStream, nameof(superStream));
        }
        _startInSuperStream = startPosition;
        PositionInSuperStream = startPosition;
        _endInSuperStream = startPosition + maxLength;
        _superStream = superStream;
        _isDisposed = false;
        _hasReachedEnd = false;
    }

    public override long Length
    {
        get
        {
            ThrowIfDisposed();
            return _endInSuperStream - _startInSuperStream;
        }
    }

    public override long Position
    {
        get
        {
            ThrowIfDisposed();
            return PositionInSuperStream - _startInSuperStream;
        }
        set
        {
            ThrowIfDisposed();
            throw new InvalidOperationException(SR.IO_NotSupported_UnseekableStream);
        }
    }

    public override bool CanRead => !_isDisposed;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    internal bool HasReachedEnd
    {
        get
        {
            if (!_hasReachedEnd && PositionInSuperStream > _endInSuperStream)
            {
                _hasReachedEnd = true;
            }
            return _hasReachedEnd;
        }
        set
        {
            if (value) // Don't allow revert to false
            {
                _hasReachedEnd = true;
            }
        }
    }

    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    private void ThrowIfBeyondEndOfStream()
    {
        if (HasReachedEnd)
        {
            throw new EndOfStreamException();
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> destination)
    {
        ThrowIfDisposed();
        ThrowIfBeyondEndOfStream();

        // parameter validation sent to _superStream.Read
        int origCount = destination.Length;
        int count = destination.Length;

        if (PositionInSuperStream + count > _endInSuperStream)
        {
            count = (int)(_endInSuperStream - PositionInSuperStream);
        }

        Debug.Assert(count >= 0);
        Debug.Assert(count <= origCount);

        int ret = _superStream.Read(destination.Slice(0, count));

        PositionInSuperStream += ret;
        return ret;
    }

    public override int ReadByte()
    {
        byte b = default;
        return Read(new Span<byte>(ref b)) == 1 ? b : -1;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<int>(cancellationToken);
        }
        ValidateBufferArguments(buffer, offset, count);
        return ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<int>(cancellationToken);
        }
        ThrowIfDisposed();
        ThrowIfBeyondEndOfStream();
        return ReadAsyncCore(buffer, cancellationToken);
    }

    protected async ValueTask<int> ReadAsyncCore(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        Debug.Assert(!_hasReachedEnd);

        cancellationToken.ThrowIfCancellationRequested();

        if (PositionInSuperStream > _endInSuperStream - buffer.Length)
        {
            buffer = buffer.Slice(0, (int)(_endInSuperStream - PositionInSuperStream));
        }

        int ret = await _superStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

        PositionInSuperStream += ret;
        return ret;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(SR.IO_NotSupported_UnseekableStream);

    public override void SetLength(long value) => throw new NotSupportedException(SR.IO_NotSupported_UnseekableStream);

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException(SR.IO_NotSupported_UnwritableStream);

    public override void Flush() { }

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        cancellationToken.IsCancellationRequested ? Task.FromCanceled(cancellationToken) :
        Task.CompletedTask;

    // Close the stream for reading.  Note that this does NOT close the superStream (since
    // the substream is just 'a chunk' of the super-stream
    protected override void Dispose(bool disposing)
    {
        _isDisposed = true;
        base.Dispose(disposing);
    }
}

// Stream that allows wrapping a super stream and specify the lower and upper limits that can be read from it.
// It is meant to be used when the super stream is seekable.
// Does not support writing.
internal sealed class SeekableSubReadStream : SubReadStream
{
    public SeekableSubReadStream(Stream superStream, long startPosition, long maxLength)
        : base(superStream, startPosition, maxLength)
    {
        if (!superStream.CanSeek)
        {
            throw new ArgumentException(SR.IO_NotSupported_UnseekableStream, nameof(superStream));
        }
    }

    public override bool CanSeek => !_isDisposed;

    public override long Position
    {
        get
        {
            ThrowIfDisposed();
            return PositionInSuperStream - _startInSuperStream;
        }
        set
        {
            ThrowIfDisposed();
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(value, _endInSuperStream);
            PositionInSuperStream = _startInSuperStream + value;
        }
    }

    public override int Read(Span<byte> destination)
    {
        ThrowIfDisposed();
        VerifyPositionInSuperStream();

        // parameter validation sent to _superStream.Read
        int origCount = destination.Length;
        int count = destination.Length;

        if ((ulong)(PositionInSuperStream + count) > (ulong)_endInSuperStream)
        {
            count = Math.Max(0, (int)(_endInSuperStream - PositionInSuperStream));
        }

        Debug.Assert(count >= 0);
        Debug.Assert(count <= origCount);

        if (count > 0)
        {
            int bytesRead = _superStream.Read(destination.Slice(0, count));
            PositionInSuperStream += bytesRead;
            return bytesRead;
        }

        return 0;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<int>(cancellationToken);
        }
        ThrowIfDisposed();
        VerifyPositionInSuperStream();
        return ReadAsyncCore(buffer, cancellationToken);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ThrowIfDisposed();

        long newPositionInSuperStream = origin switch
        {
            SeekOrigin.Begin => _startInSuperStream + offset,
            SeekOrigin.Current => PositionInSuperStream + offset,
            SeekOrigin.End => _endInSuperStream + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };

        if (newPositionInSuperStream < _startInSuperStream)
        {
            throw new IOException(SR.IO_SeekBeforeBegin);
        }

        PositionInSuperStream = newPositionInSuperStream;

        return PositionInSuperStream - _startInSuperStream;
    }

    private void VerifyPositionInSuperStream()
    {
        if (PositionInSuperStream != _superStream.Position)
        {
            // Since we can seek, if the stream had its position pointer moved externally,
            // we must bring it back to the last read location on this stream
            _superStream.Seek(PositionInSuperStream, SeekOrigin.Begin);
        }
    }
}