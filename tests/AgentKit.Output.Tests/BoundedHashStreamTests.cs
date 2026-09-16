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

    [Fact]
    public void Write_ByteArrayOverload_WhenWithinBounds_DelegatesToSpanOverload()
    {
        using var stream = new BoundedHashStream(8, TestContext.Current.CancellationToken);

        stream.Write([1, 2, 3, 4], 1, 2);

        stream.Length.ShouldBe(2L);
    }

    [Fact]
    public void Capabilities_ReflectWriteOnlyBoundedContract()
    {
        using var stream = new BoundedHashStream(8, TestContext.Current.CancellationToken);

        stream.CanRead.ShouldBeFalse();
        stream.CanSeek.ShouldBeFalse();
        stream.CanWrite.ShouldBeTrue();
    }

    [Fact]
    public void Position_Get_ReflectsCumulativeBytesWritten()
    {
        using var stream = new BoundedHashStream(8, TestContext.Current.CancellationToken);

        stream.Write([1, 2, 3]);

        stream.Position.ShouldBe(3L);
        stream.Length.ShouldBe(3L);
    }

    [Fact]
    public void Position_Set_ThrowsNotSupportedException() =>
        Should.Throw<NotSupportedException>(() =>
        {
            using var stream = new BoundedHashStream(8, TestContext.Current.CancellationToken);
            stream.Position = 1;
        });

    [Fact]
    public void Read_ThrowsNotSupportedException()
    {
        using var stream = new BoundedHashStream(8, TestContext.Current.CancellationToken);

        _ = Should.Throw<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
    }

    [Fact]
    public void Seek_ThrowsNotSupportedException()
    {
        using var stream = new BoundedHashStream(8, TestContext.Current.CancellationToken);

        _ = Should.Throw<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
    }

    [Fact]
    public void SetLength_ThrowsNotSupportedException()
    {
        using var stream = new BoundedHashStream(8, TestContext.Current.CancellationToken);

        _ = Should.Throw<NotSupportedException>(() => stream.SetLength(1));
    }

    [Fact]
    public void Flush_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        using var stream = new BoundedHashStream(8, cancellation.Token);
        cancellation.Cancel();

        _ = Should.Throw<OperationCanceledException>(stream.Flush);
    }

    [Fact]
    public void CompleteHash_WhenCalled_ReturnsStableLowercaseSha256Identity()
    {
        using var stream = new BoundedHashStream(8, TestContext.Current.CancellationToken);
        stream.Write("abc"u8);

        var hash = stream.CompleteHash();

        hash.Value.ShouldBe("sha256:ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }
}
