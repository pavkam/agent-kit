// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output.Tests;

/// <summary>Verifies argument and byte-limit behavior of the bounded schema hashing sink.</summary>
public sealed class BoundedHashStreamTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenMaximumBytesIsNotPositive_ThrowsBeforeUse(int maximumBytes)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new BoundedHashStream(maximumBytes, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("maximumBytes");
    }

    [Fact]
    public void Write_WhenBufferIsNull_ThrowsArgumentNullException()
    {
        using var stream = new BoundedHashStream(8, TestContext.Current.CancellationToken);

        var exception = Should.Throw<ArgumentNullException>(() => stream.Write(null!, 0, 0));

        exception.ParamName.ShouldBe("buffer");
    }

    [Theory]
    [InlineData(-1, 0, "offset")]
    [InlineData(2, 0, "offset")]
    [InlineData(0, -1, "count")]
    [InlineData(1, 1, "count")]
    public void Write_WhenBufferRangeIsInvalid_ThrowsArgumentOutOfRangeException(
        int offset,
        int count,
        string parameterName)
    {
        using var stream = new BoundedHashStream(8, TestContext.Current.CancellationToken);

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => stream.Write([1], offset, count));

        exception.ParamName.ShouldBe(parameterName);
    }

    [Fact]
    public void Write_WhenBytesExceedLimit_ThrowsSizeLimitException()
    {
        using var stream = new BoundedHashStream(1, TestContext.Current.CancellationToken);

        _ = Should.Throw<OutputSchemaSizeLimitException>(() => stream.Write([1, 2]));
    }
}
