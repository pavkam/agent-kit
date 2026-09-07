// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

public sealed class FileSystemPathTests
{
    [Fact]
    public void Constructor_WhenValueNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new FileSystemPath(null!));

        exception.ParamName.ShouldBe("value");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenValueEmptyOrWhitespace_ThrowsArgumentException(string value)
    {
        var exception = Should.Throw<ArgumentException>(() => new FileSystemPath(value));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_WhenValueContainsOnlyCurrentDirectorySegments_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new FileSystemPath("./."));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_WhenValueUsesMixedSeparatorsAndCurrentSegments_NormalizesValue()
    {
        var path = new FileSystemPath("./sub\\dir/./file.txt");

        path.Value.ShouldBe("sub/dir/file.txt");
    }

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("/tmp/file.txt")]
    public void Constructor_WhenValueRooted_ThrowsArgumentException(string value)
    {
        var exception = Should.Throw<ArgumentException>(() => new FileSystemPath(value));

        exception.ParamName.ShouldBe("value");
        exception.Message.ShouldContain("relative");
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("a/../../escape.txt")]
    [InlineData("a/b/..")]
    public void Constructor_WhenValueContainsTraversalSegment_ThrowsArgumentException(string value)
    {
        var exception = Should.Throw<ArgumentException>(() => new FileSystemPath(value));

        exception.ParamName.ShouldBe("value");
        exception.Message.ShouldContain("traversal");
    }

    [Fact]
    public void Constructor_WhenValueValid_RoundTripsThroughValue()
    {
        var path = new FileSystemPath("sub/dir/file.txt");

        path.Value.ShouldBe("sub/dir/file.txt");
    }

    [Fact]
    public void ToString_WhenCalled_ReturnsUnderlyingValue()
    {
        var path = new FileSystemPath("a.txt");

        path.ToString().ShouldBe("a.txt");
    }

    [Fact]
    public void Equality_WhenSameValue_InstancesAreEqual()
    {
        var first = new FileSystemPath("a.txt");
        var second = new FileSystemPath("a.txt");

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equality_WhenDifferentValue_InstancesAreNotEqual()
    {
        var first = new FileSystemPath("a.txt");
        var second = new FileSystemPath("b.txt");

        first.ShouldNotBe(second);
    }
}
