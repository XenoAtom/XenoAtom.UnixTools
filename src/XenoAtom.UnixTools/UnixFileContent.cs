// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text;

namespace XenoAtom.UnixTools;

/// <summary>
/// Represents the content of a Unix file.
/// </summary>
public readonly struct UnixFileContent
{
    private readonly object? _data;

    /// <summary>
    /// An empty content.
    /// </summary>
    public static UnixFileContent Empty => default;

    internal UnixFileContent(object? data) => _data = data;

    /// <summary>
    /// Initializes a new instance of <see cref="UnixFileContent"/> with a string.
    /// </summary>
    /// <param name="data">The string as text representing the data.</param>
    public UnixFileContent(string data) => _data = data;

    /// <summary>
    /// Initializes a new instance of <see cref="UnixFileContent"/> with a byte array.
    /// </summary>
    /// <param name="data">The byte array representing the data.</param>
    public UnixFileContent(byte[] data) => _data = data;

    /// <summary>
    /// Initializes a new instance of <see cref="UnixFileContent"/> with a stream.
    /// </summary>
    /// <param name="data">The stream representing the data.</param>
    public UnixFileContent(Stream data) => _data = data;

    /// <summary>
    /// Initializes a new instance of <see cref="UnixFileContent"/> with a function returning a string.
    /// </summary>
    /// <param name="data">The function returning a string representing the data.</param>
    public UnixFileContent(Func<string> data) => _data = data;

    /// <summary>
    /// Initializes a new instance of <see cref="UnixFileContent"/> with a function returning a byte array.
    /// </summary>
    /// <param name="data">The function returning a byte array representing the data.</param>
    public UnixFileContent(Func<byte[]> data) => _data = data;

    /// <summary>
    /// Initializes a new instance of <see cref="UnixFileContent"/> with a function returning a stream.
    /// </summary>
    /// <param name="data">The function returning a stream representing the data.</param>
    public UnixFileContent(Func<Stream> data) => _data = data;

    /// <summary>
    /// Gets the kind of content.
    /// </summary>
    public UnixFileContentKind Kind
    {
        get
        {
            return _data switch
            {
                string _ => UnixFileContentKind.String,
                byte[] _ => UnixFileContentKind.ByteArray,
                Stream _ => UnixFileContentKind.Stream,
                Func<string> _ => UnixFileContentKind.FuncString,
                Func<byte[]> _ => UnixFileContentKind.FuncByteArray,
                Func<Stream> _ => UnixFileContentKind.FuncStream,
                _ => UnixFileContentKind.Empty,
            };
        }
    }

    /// <summary>
    /// Gets the raw content data.
    /// </summary>
    public object? Data => _data;

    /// <summary>
    /// Copies the content to a stream.
    /// </summary>
    /// <param name="stream">The output stream</param>
    /// <param name="encoding">The encoding to use when copying a string content.</param>
    /// <exception cref="InvalidOperationException">If the content kind is invalid.</exception>
    public void CopyTo(Stream stream, Encoding? encoding = null)
    {
        switch (_data)
        {
            case string data:
            {
                encoding ??= Encoding.UTF8;
                var bytes = encoding.GetBytes(data);
                stream.Write(bytes, 0, bytes.Length);
                break;
            }
            case byte[] data:
            {
                stream.Write(data, 0, data.Length);
                break;
            }
            case Stream data:
            {
                data.Position = 0;
                data.CopyTo(stream);
                data.Position = 0;
                break;
            }
            case Func<string> func:
            {
                var s = func();
                encoding ??= Encoding.UTF8;
                var bytes = encoding.GetBytes(s);
                stream.Write(bytes, 0, bytes.Length);
                break;
            }
            case Func<byte[]> func:
            {
                var b = func();
                stream.Write(b, 0, b.Length);
                break;
            }
            case Func<Stream> func:
            {
                using var s = func();
                s.CopyTo(stream);
                break;
            }
            case null:
                break;
            default:
                throw new InvalidOperationException($"Invalid content kind: {Kind}");
        }
    }

    /// <summary>
    /// Copies the content to a stream.
    /// </summary>
    /// <param name="stream">The output stream</param>
    /// <param name="encoding">The encoding to use when copying a string content.</param>
    /// <exception cref="InvalidOperationException">If the content kind is invalid.</exception>
    public async ValueTask CopyToAsync(Stream stream, Encoding? encoding = null)
    {
        switch (_data)
        {
            case string data:
            {
                encoding ??= Encoding.UTF8;
                var bytes = encoding.GetBytes(data);
                await stream.WriteAsync(bytes, 0, bytes.Length);
                break;
            }
            case byte[] data:
            {
                await stream.WriteAsync(data, 0, data.Length);
                break;
            }
            case Stream data:
            {
                data.Position = 0;
                await data.CopyToAsync(stream);
                data.Position = 0;
                break;
            }
            case Func<string> func:
            {
                var s = func();
                encoding ??= Encoding.UTF8;
                var bytes = encoding.GetBytes(s);
                await stream.WriteAsync(bytes, 0, bytes.Length);
                break;
            }
            case Func<byte[]> func:
            {
                var b = func();
                await stream.WriteAsync(b, 0, b.Length);
                break;
            }
            case Func<Stream> func:
            {
                await using var s = func();
                await s.CopyToAsync(stream);
                break;
            }
            case null:
                break;
            default:
                throw new InvalidOperationException($"Invalid content kind: {Kind}");
        }
    }

    internal UnixFileContent CreateCopy()
    {
        return _data switch
        {
            byte[] b => new UnixFileContent((byte[])b.Clone()),
            _ => new UnixFileContent(_data)
        };
    }

    /// <summary>
    /// Disposes the content (only if the content is a stream).
    /// </summary>
    public void Dispose()
    {
        if (_data is Stream stream)
        {
            stream.Dispose();
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return _data switch
        {
            string s => $"String Content",
            byte[] b => $"byte[] Content",
            Stream s => $"Stream Content",
            Func<string> f => $"Func<String> Content",
            Func<byte[]> f => $"Func<byte[]> Content",
            Func<Stream> f => $"Func<Stream> Content",
            _ => $"Empty Content"
        };
    }

    /// <summary>
    /// Implicit conversion from a string to <see cref="UnixFileContent"/>.
    /// </summary>
    /// <param name="data">The string to convert.</param>
    public static implicit operator UnixFileContent(string data) => new(data);

    /// <summary>
    /// Implicit conversion from a byte array to <see cref="UnixFileContent"/>.
    /// </summary>
    /// <param name="data">The byte array to convert.</param>
    public static implicit operator UnixFileContent(byte[] data) => new(data);

    /// <summary>
    /// Implicit conversion from a stream to <see cref="UnixFileContent"/>.
    /// </summary>
    /// <param name="data">The stream to convert.</param>
    public static implicit operator UnixFileContent(Stream data) => new(data);

    /// <summary>
    /// Implicit conversion from a function returning a string to <see cref="UnixFileContent"/>.
    /// </summary>
    /// <param name="data">The function returning a string to convert.</param>
    public static implicit operator UnixFileContent(Func<string> data) => new(data);

    /// <summary>
    /// Implicit conversion from a function returning a byte array to <see cref="UnixFileContent"/>.
    /// </summary>
    /// <param name="data">The function returning a byte array to convert.</param>
    public static implicit operator UnixFileContent(Func<byte[]> data) => new(data);

    /// <summary>
    /// Implicit conversion from a function returning a stream to <see cref="UnixFileContent"/>.
    /// </summary>
    /// <param name="data">The function returning a stream to convert.</param>
    public static implicit operator UnixFileContent(Func<Stream> data) => new(data);
}