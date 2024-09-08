# XenoAtom.UnixTools [![ci](https://github.com/xoofx/XenoAtom.UnixTools/actions/workflows/ci.yml/badge.svg)](https://github.com/xoofx/XenoAtom.UnixTools/actions/workflows/ci.yml) [![NuGet](https://img.shields.io/nuget/v/XenoAtom.UnixTools.svg)](https://www.nuget.org/packages/XenoAtom.UnixTools/)

<img align="right" width="160px" height="160px" src="https://raw.githubusercontent.com/xoofx/XenoAtom.UnixTools/main/img/XenoAtom.UnixTools.png">

This project provides a set of Unix tools for .NET 8.0+.

> **Note**: This project is still in early development and the API is subject to change.

## ✨ Features

- **CPIO Archive**: Read and write CPIO archives (Only the newc format is supported)
- **UnixInMemoryFileSystem**: A simple in-memory file system to manipulate files and directories
  - This in memory filesystem can be used to easily manipulate in and out CPIO archives
- .NET 8.0+ compatible and NativeAOT ready

## 📖 Usage

Reading a CPIO archive:

```csharp
var cpioReader = new CpioReader(File.OpenRead("archive.cpio"));
while (cpioReader.TryGetNextEntry(out var entry))
{
    Console.WriteLine($"Entry: {entry.Name} {entry.FileType} ({entry.Mode})");
})
```

Writing a CPIO archive with a `UnixInMemoryFileSystem`:

```csharp
var stream = new MemoryStream();
var fs = new UnixInMemoryFileSystem();
fs.CreateDirectory("/dir1");
fs.CreateDirectory("/dir1/dir2");
fs.CreateFile("/dir1/file1.txt", "Hello World");
{
    using var writer = new CpioWriter(File.Create("archive.cpio"));
    fs.WriteTo(writer);
}
```

## 🪪 License

This software is released under the [BSD-2-Clause license](https://opensource.org/licenses/BSD-2-Clause). 

## 🤗 Author

Alexandre Mutel aka [xoofx](https://xoofx.github.io).
