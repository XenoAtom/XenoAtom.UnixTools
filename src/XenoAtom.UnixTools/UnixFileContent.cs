// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text;

namespace XenoAtom.UnixTools;

public readonly struct UnixFileContent
{
    private readonly object? _data;

    public static UnixFileContent Empty => default;

    internal UnixFileContent(object? data) => _data = data;

    public UnixFileContent(string data) => _data = data;

    public UnixFileContent(byte[] data) => _data = data;

    public UnixFileContent(Stream data) => _data = data;

    public UnixFileContent(Func<string> data) => _data = data;

    public UnixFileContent(Func<byte[]> data) => _data = data;

    public UnixFileContent(Func<Stream> data) => _data = data;

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

    public object? Data => _data;

    public string AsString() => _data switch
    {
        string s => s,
        Func<string> f => f(),
        _ => throw new InvalidOperationException("Invalid kind")
    };

    public byte[] AsByteArray() => _data switch
    {
        byte[] b => b,
        Func<byte[]> f => f(),
        _ => throw new InvalidOperationException("Invalid kind")
    };

    public Stream AsStream() => _data switch
    {
        Stream s => s,
        Func<Stream> f => f(),
        _ => throw new InvalidOperationException("Invalid kind")
    };
    
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
        }
    }

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
        }
    }

    public void Dispose()
    {
        if (_data is Stream stream)
        {
            stream.Dispose();
        }
    }

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

    public static implicit operator UnixFileContent(string data) => new(data);

    public static implicit operator UnixFileContent(byte[] data) => new(data);

    public static implicit operator UnixFileContent(Stream data) => new(data);

    public static implicit operator UnixFileContent(Func<string> data) => new(data);

    public static implicit operator UnixFileContent(Func<byte[]> data) => new(data);

    public static implicit operator UnixFileContent(Func<Stream> data) => new(data);
}