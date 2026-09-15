// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>
/// A read-only <see cref="Stream"/> test double that blocks the first read
/// behind deterministic entry and release signals while honoring
/// cancellation, so a test can interrupt an adapter precisely while it is
/// reading a response body without any wall-clock waiting.
/// </summary>
/// <remarks>
/// <see cref="Entered"/> completes when a reader reaches the gate; the test
/// then cancels the reader's token, advances an injected clock, or calls
/// <see cref="Release"/> to let the read complete as an empty body. The
/// stream is not thread-safe beyond the signal handoff; tests drive it from a
/// single reader.
/// </remarks>
public sealed class GatedReadStream: Stream
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets a signal that completes when a read reaches the gate.</summary>
    /// <value>A task that completes once the first read is blocked and waiting.</value>
    public Task Entered => _entered.Task;

    /// <summary>Releases a blocked read to complete as an empty body.</summary>
    public void Release() => _release.TrySetResult();

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => 0;

    /// <inheritdoc/>
    public override long Position
    {
        get => 0;
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        _ = _entered.TrySetResult();
        await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        return 0;
    }

    /// <inheritdoc/>
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        await ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
