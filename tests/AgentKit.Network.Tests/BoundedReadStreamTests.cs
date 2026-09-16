// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.Tests;

/// <summary>Verifies BoundedReadStream behavior and contracts.</summary>
public sealed class BoundedReadStreamTests
{
    [Fact]
    public void Constructor_WhenInnerIsNull_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new BoundedReadStream(null!, 1, CancellationToken.None));
        exception.ParamName.ShouldBe("inner");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenMaximumBytesIsNotPositive_ThrowsWithExactParameterName(long maximumBytes)
    {
        using var inner = new MemoryStream();
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BoundedReadStream(inner, maximumBytes, CancellationToken.None));
        exception.ParamName.ShouldBe("maximumBytes");
    }

    [Fact]
    public void Capabilities_ReflectInnerReadOnlyBoundedContract()
    {
        using var inner = new MemoryStream("hello"u8.ToArray());
        using var stream = new BoundedReadStream(inner, 1_024, CancellationToken.None);
        stream.CanRead.ShouldBeTrue();
        stream.CanSeek.ShouldBeFalse();
        stream.CanWrite.ShouldBeFalse();
    }

    [Fact]
    public void Length_WhenAccessed_ThrowsNotSupported() =>
        Should.Throw<NotSupportedException>(() => _ = Stream(1_024).Length);

    [Fact]
    public void Position_Set_ThrowsNotSupported() =>
        Should.Throw<NotSupportedException>(() => Stream(1_024).Position = 1);

    [Fact]
    public void Position_Get_ReflectsObservedBytesAfterRead()
    {
        var stream = Stream(1_024, "hello");
        var buffer = new byte[5];
        _ = stream.Read(buffer, 0, buffer.Length);
        stream.Position.ShouldBe(5);
    }

    [Fact]
    public void Read_ByteArray_WhenWithinBounds_ReturnsExactBytes()
    {
        var stream = Stream(1_024, "hello");
        var buffer = new byte[5];
        var read = stream.Read(buffer, 0, buffer.Length);
        read.ShouldBe(5);
        Encoding.ASCII.GetString(buffer).ShouldBe("hello");
    }

    [Fact]
    public void Read_ByteArray_WhenExceedsMaximum_ThrowsTypedBoundaryException()
    {
        var stream = Stream(4, "too-large");
        var buffer = new byte[9];
        _ = Should.Throw<NetworkResponseTooLargeException>(() => stream.Read(buffer, 0, buffer.Length));
    }

    [Fact]
    public void Read_ByteArray_WhenTimeoutTokenAlreadyCancelled_ThrowsTimedOutBeforeRead()
    {
        using var timeout = new CancellationTokenSource();
        timeout.Cancel();
        var stream = new BoundedReadStream(new MemoryStream("hello"u8.ToArray()), 1_024, timeout.Token);
        var buffer = new byte[5];
        _ = Should.Throw<NetworkResponseTimedOutException>(() => stream.Read(buffer, 0, buffer.Length));
    }

    [Fact]
    public void Read_Span_WhenWithinBounds_ReturnsExactBytes()
    {
        var stream = Stream(1_024, "hello");
        Span<byte> buffer = stackalloc byte[5];
        var read = stream.Read(buffer);
        read.ShouldBe(5);
        Encoding.ASCII.GetString(buffer).ShouldBe("hello");
    }

    [Fact]
    public void Read_Span_WhenTimeoutTokenAlreadyCancelled_ThrowsTimedOutBeforeRead()
    {
        using var timeout = new CancellationTokenSource();
        timeout.Cancel();
        var stream = new BoundedReadStream(new MemoryStream("hello"u8.ToArray()), 1_024, timeout.Token);
        var buffer = new byte[5];
        _ = Should.Throw<NetworkResponseTimedOutException>(() => stream.Read(buffer.AsSpan()));
    }

    [Fact]
    public async Task ReadAsync_Memory_WhenWithinBounds_ReturnsExactBytes()
    {
        var stream = Stream(1_024, "hello");
        var buffer = new byte[5];
        var read = await stream.ReadAsync(buffer, TestContext.Current.CancellationToken);
        read.ShouldBe(5);
        Encoding.ASCII.GetString(buffer).ShouldBe("hello");
    }

    [Fact]
    public async Task ReadAsync_Memory_WhenTimeoutTokenAlreadyCancelled_ThrowsTimedOutBeforeRead()
    {
        using var timeout = new CancellationTokenSource();
        timeout.Cancel();
        var stream = new BoundedReadStream(new MemoryStream("hello"u8.ToArray()), 1_024, timeout.Token);
        async Task ReadAsync() => _ = await stream.ReadAsync(new byte[5], TestContext.Current.CancellationToken);
        _ = await Should.ThrowAsync<NetworkResponseTimedOutException>(ReadAsync);
    }

    [Fact]
    public async Task ReadAsync_Memory_WhenTimeoutTokenCancelsDuringRead_ThrowsTypedTimeoutInsteadOfCancellation()
    {
        using var timeout = new CancellationTokenSource();
        var inner = new NeverCompletingStream();
        var stream = new BoundedReadStream(inner, 1_024, timeout.Token);
        var readTask = stream.ReadAsync(new byte[5], TestContext.Current.CancellationToken).AsTask();
        await inner.PendingRead.WaitAsync(TestContext.Current.CancellationToken);
        await timeout.CancelAsync();
        _ = await Should.ThrowAsync<NetworkResponseTimedOutException>(async () => await readTask);
    }

    [Fact]
    public async Task ReadAsync_Memory_WhenCallerCancels_PropagatesCancellationNotTimeout()
    {
        using var timeout = new CancellationTokenSource();
        using var caller = new CancellationTokenSource();
        var inner = new NeverCompletingStream();
        var stream = new BoundedReadStream(inner, 1_024, timeout.Token);
        var readTask = stream.ReadAsync(new byte[5], caller.Token).AsTask();
        await inner.PendingRead.WaitAsync(TestContext.Current.CancellationToken);
        await caller.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await readTask);
    }

    [Fact]
    public async Task ReadAsync_ByteArray_WhenWithinBounds_DelegatesThroughMemoryOverload()
    {
#pragma warning disable CA1835 // Exercising the legacy byte[]+offset+count overload is the point of this test.
        Stream stream = Stream(1_024, "hello");
        var buffer = new byte[5];
        var read = await stream.ReadAsync(buffer, 0, buffer.Length, TestContext.Current.CancellationToken);
#pragma warning restore CA1835
        read.ShouldBe(5);
        Encoding.ASCII.GetString(buffer).ShouldBe("hello");
    }

    [Fact]
    public void Flush_DoesNotThrow() => Stream(1_024).Flush();

    [Fact]
    public void Seek_ThrowsNotSupported() =>
        Should.Throw<NotSupportedException>(() => Stream(1_024).Seek(0, SeekOrigin.Begin));

    [Fact]
    public void SetLength_ThrowsNotSupported() =>
        Should.Throw<NotSupportedException>(() => Stream(1_024).SetLength(1));

    [Fact]
    public void Write_ThrowsNotSupported() =>
        Should.Throw<NotSupportedException>(() => Stream(1_024).Write([1], 0, 1));

    [Fact]
    public void Dispose_DisposesInnerStream()
    {
        var inner = new MemoryStream("hello"u8.ToArray());
        var stream = new BoundedReadStream(inner, 1_024, CancellationToken.None);
        stream.Dispose();
        _ = Should.Throw<ObjectDisposedException>(() => inner.ReadByte());
    }

    [Fact]
    public async Task DisposeAsync_DisposesInnerStream()
    {
        var inner = new MemoryStream("hello"u8.ToArray());
        var stream = new BoundedReadStream(inner, 1_024, CancellationToken.None);
        await stream.DisposeAsync();
        _ = Should.Throw<ObjectDisposedException>(() => inner.ReadByte());
    }

    private static BoundedReadStream Stream(long maximumBytes, string content = "") =>
        new(new MemoryStream(Encoding.ASCII.GetBytes(content)), maximumBytes, CancellationToken.None);

    private sealed class NeverCompletingStream: MemoryStream
    {
        internal SemaphoreSlim PendingRead { get; } = new(0);

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _ = PendingRead.Release();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return 0;
        }
    }
}
