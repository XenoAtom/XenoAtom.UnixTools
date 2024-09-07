// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.UnixTools.Tests;

[TestClass]
public class TestUnixPath
{
    [TestMethod]
    public void TestSpecialCases()
    {
        var result = UnixPath.Normalize("/");
        Assert.IsTrue(ReferenceEquals(result, "/"), "Should return the same reference");

        result = UnixPath.Normalize("/a/b/c");
        Assert.IsTrue(ReferenceEquals(result, "/a/b/c"), "Should return the same reference");

        result = UnixPath.Normalize("../a/b/c");
        Assert.IsTrue(ReferenceEquals(result, "../a/b/c"), "Should return the same reference");
    }

    [DataTestMethod]
    [DataRow("", ".")]
    [DataRow("/", "/")]
    [DataRow("/a/b/c", "/a/b/c")]
    [DataRow("/a/./b", "/a/b")]
    [DataRow("/a/../b", "/b")]
    [DataRow("/a/./../b", "/b")]
    [DataRow("/a/b/../../c", "/c")]
    [DataRow("a/b/c", "a/b/c")]
    [DataRow("a/./b", "a/b")]
    [DataRow("a/../b", "b")]
    [DataRow("a/./../b", "b")]
    [DataRow("a/b/../../c", "c")]
    [DataRow("a/b/../../c/..", ".")]
    [DataRow("a/b/../../c/../..", "..")]
    [DataRow("/../../..", "/")]
    [DataRow("../a/..", "..")]
    [DataRow("../..", "../..")]
    [DataRow("..", "..")]
    [DataRow("a/..", ".")]
    [DataRow("a/.", "a")]
    [DataRow("/a/b/.", "/a/b")]
    [DataRow("./", ".")]
    [DataRow("./a", "a")]
    [DataRow("/.", "/")]
    [DataRow("/./", "/")]
    [DataRow("/./a", "/a")]
    [DataRow("/./a/./b", "/a/b")]
    [DataRow("././././", ".")]
    [DataRow(".///./", ".")]
    [DataRow(".///", ".")]
    public void TestNormalize(string path, string expected)
    {
        var normalized = UnixPath.Normalize(path);
        Assert.AreEqual(expected, normalized);
    }

    [DataTestMethod]
    [DataRow("", "", "")]
    [DataRow("/", "", "/")]
    [DataRow("", "/", "/")]
    [DataRow("/", "/", "/")]
    [DataRow("/a", "b", "/a/b")]
    [DataRow("/a", "/b", "/b")]
    [DataRow("/a/", "b", "/a/b")]
    [DataRow("/a", "b/", "/a/b/")]
    [DataRow("/a/", "b/", "/a/b/")]
    public void TestCombine2(string path1, string path2, string expected)
    {
        var combined = UnixPath.Combine(path1, path2);
        Assert.AreEqual(expected, combined);
    }

    [DataTestMethod]
    [DataRow("", "", "", "")]
    [DataRow("/", "", "", "/")]
    [DataRow("", "/", "", "/")]
    [DataRow("", "", "/", "/")]
    [DataRow("/", "/", "/", "/")]
    [DataRow("/a", "b", "c", "/a/b/c")]
    [DataRow("/a", "/b", "c", "/b/c")]
    [DataRow("/a", "b", "/c", "/c")]
    [DataRow("/a", "/b", "/c", "/c")]
    [DataRow("/a/", "b", "c", "/a/b/c")]
    [DataRow("/a", "b/", "c", "/a/b/c")]
    [DataRow("/a", "b", "c/", "/a/b/c/")]
    [DataRow("/a/", "b/", "c", "/a/b/c")]
    [DataRow("/a/", "b", "c/", "/a/b/c/")]
    [DataRow("/a", "b/", "c/", "/a/b/c/")]
    [DataRow("/a/", "b/", "c/", "/a/b/c/")]
    public void TestCombine3(string path1, string path2, string path3, string expected)
    {
        var combined = UnixPath.Combine([path1, path2, path3]);
        Assert.AreEqual(expected, combined);
    }
}