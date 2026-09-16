// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies FileRead behavior and contracts.</summary>
public sealed class FileReadTests
{
    [Fact]
    public void FileRead_Constructor_WhenContentNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new FileRead(null!, 0));
        exception.ParamName.ShouldBe("content");
    }

    [Fact]
    public void FileRead_Constructor_WhenBytesNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new FileRead("text", -1));
        exception.ParamName.ShouldBe("bytes");
    }

    [Fact]
    public void FileRead_Constructor_WhenValid_RoundTripsProperties()
    {
        var read = new FileRead("text", 4);
        read.Content.ShouldBe("text");
        read.Bytes.ShouldBe(4);
    }

    [Fact]
    public void FileRead_Equality_WhenSameValues_InstancesAreEqual() => new FileRead("text", 4).ShouldBe(new FileRead("text", 4));
    [Fact]
    public void FileRead_Equality_WhenDifferentContent_InstancesAreNotEqual() => new FileRead("text", 4).ShouldNotBe(new FileRead("other", 4));

    [Fact]
    public void FileRead_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new FileRead("text", 4);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
