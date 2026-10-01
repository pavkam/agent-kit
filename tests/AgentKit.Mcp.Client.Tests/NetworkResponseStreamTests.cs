// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client.Tests;

/// <summary>Verifies <see cref="NetworkResponseStream"/> forwards reads and owns the network response exactly once.</summary>
public sealed class NetworkResponseStreamTests
{
    private static StreamNetworkResponse Response() =>
        new(200, new MemoryStream("hello"u8.ToArray()));

    [Fact]
    public async Task ReadAsync_WhenBodyHasBytes_ForwardsThemWithoutBuffering()
    {
        await using var stream = new NetworkResponseStream(Response());
        var buffer = new byte[16];

        var read = await stream.ReadAsync(buffer, TestContext.Current.CancellationToken);

        read.ShouldBe(5);
        System.Text.Encoding.UTF8.GetString(buffer, 0, read).ShouldBe("hello");
        stream.CanRead.ShouldBeTrue();
        stream.CanSeek.ShouldBeFalse();
        stream.CanWrite.ShouldBeFalse();
    }

    [Fact]
    public void Read_WhenSynchronous_ForwardsBytes()
    {
        using var stream = new NetworkResponseStream(Response());
        var buffer = new byte[16];

        stream.Read(buffer, 0, buffer.Length).ShouldBe(5);
        stream.Read(buffer.AsSpan()).ShouldBe(0);
    }

    [Fact]
    public void Dispose_WhenCalledRepeatedly_DisposesTheResponseExactlyOnce()
    {
        var response = Response();
        var stream = new NetworkResponseStream(response);

        stream.Dispose();
        stream.Dispose();

        response.IsDisposed.ShouldBeTrue();
        stream.CanRead.ShouldBeFalse();
        _ = Should.Throw<ObjectDisposedException>(() => stream.Read(new byte[1], 0, 1));
    }

    [Fact]
    public async Task DisposeAsync_WhenAlreadyDisposedSynchronously_DoesNotDisposeAgain()
    {
        var response = new CountingResponse();
        var stream = new NetworkResponseStream(response);

        stream.Dispose();
        await stream.DisposeAsync();

        response.Disposals.ShouldBe(1);
    }

    [Fact]
    public async Task ReadAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        var stream = new NetworkResponseStream(Response());
        await stream.DisposeAsync();

        _ = await Should.ThrowAsync<ObjectDisposedException>(
            async () => await stream.ReadExactlyAsync(new byte[1], TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Dispose_WhenResponseDisposalFaultsAsynchronously_ObservesTheFailure()
    {
        var response = new CountingResponse { DisposeAsynchronouslyFaulting = true };
        var stream = new NetworkResponseStream(response);

        stream.Dispose();
        response.Disposals.ShouldBe(1);
    }

    [Fact]
    public void ForwardOnlyMembers_WhenUsed_ThrowNotSupportedException()
    {
        using var stream = new NetworkResponseStream(Response());

        _ = Should.Throw<NotSupportedException>(() => stream.Length);
        _ = Should.Throw<NotSupportedException>(() => stream.Position);
        _ = Should.Throw<NotSupportedException>(() => stream.Position = 0);
        _ = Should.Throw<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        _ = Should.Throw<NotSupportedException>(() => stream.SetLength(0));
        _ = Should.Throw<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
        stream.Flush();
    }

    private sealed class CountingResponse: INetworkResponse
    {
        internal int Disposals { get; private set; }

        internal bool DisposeAsynchronouslyFaulting { get; init; }

        public NetworkResponseMetadata Metadata { get; } = new(200, new NetworkHeaderSet([]), null);

        public NetworkEgressEvidence? EgressEvidence => null;

        public Stream Content { get; } = new MemoryStream();

        public async ValueTask DisposeAsync()
        {
            Disposals++;
            if (DisposeAsynchronouslyFaulting)
            {
                await Task.Yield();
                throw new InvalidOperationException("dispose failed");
            }
        }
    }
}
