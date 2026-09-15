// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>
/// A read-only <see cref="Stream"/> test double that serves an optional
/// prefix of bytes and then fails every subsequent read with a
/// caller-supplied exception, simulating a connection reset or truncated
/// response body that surfaces from a transport stream mid-read.
/// </summary>
/// <remarks>
/// The fault is raised from <see cref="ReadAsync(Memory{byte}, CancellationToken)"/>
/// and its overloads exactly as a socket-backed HTTP content stream would
/// raise it, so an adapter under test cannot distinguish this double from a
/// real transport failure. The stream is not thread-safe; tests drive it from
/// a single reader.
/// </remarks>
public sealed class FaultingReadStream: Stream
{
    private readonly byte[] _prefix;
    private readonly Func<Exception> _faultFactory;
    private int _position;

    /// <summary>Initializes a new instance of the <see cref="FaultingReadStream"/> class.</summary>
    /// <param name="faultFactory">Produces the exception thrown once <paramref name="prefix"/> is exhausted.</param>
    /// <param name="prefix">Bytes served successfully before the fault; empty by default so the very first read faults.</param>
    /// <exception cref="ArgumentNullException"><paramref name="faultFactory"/> is null.</exception>
    public FaultingReadStream(Func<Exception> faultFactory, byte[]? prefix = null)
    {
        ArgumentNullException.ThrowIfNull(faultFactory);
        _faultFactory = faultFactory;
        _prefix = prefix ?? [];
    }

    /// <summary>Creates a stream whose reads fail with an <see cref="IOException"/> after the optional prefix.</summary>
    /// <param name="prefix">Bytes served successfully before the fault.</param>
    /// <returns>A stream that raises an <see cref="IOException"/> mid-read.</returns>
    public static FaultingReadStream ConnectionReset(byte[]? prefix = null) =>
        new(static () => new IOException("Connection reset by peer while reading the response body."), prefix);

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ReadCore(buffer.Span));
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => ReadCore(buffer.AsSpan(offset, count));

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer) => ReadCore(buffer);

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private int ReadCore(Span<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            return 0;
        }

        var remaining = _prefix.Length - _position;
        if (remaining <= 0)
        {
            throw _faultFactory();
        }

        var toCopy = Math.Min(buffer.Length, remaining);
        _prefix.AsSpan(_position, toCopy).CopyTo(buffer);
        _position += toCopy;
        return toCopy;
    }
}
