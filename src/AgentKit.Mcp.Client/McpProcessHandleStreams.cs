// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

using System.IO.Pipelines;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

/// <summary>Adapts one process handle to MCP stdio client transport streams.</summary>
internal sealed class McpProcessHandleStreams: IAsyncDisposable
{
    private readonly IProcessHandle _handle;
    private readonly CancellationTokenSource _lifetime;
    private readonly Task _pumpOutput;
    private int _disposed;

    private McpProcessHandleStreams(
        IProcessHandle handle,
        PipeReader stdoutReader,
        PipeWriter stdoutWriter,
        Stream clientInput,
        Stream clientOutput,
        CancellationTokenSource lifetime,
        Task pumpOutput)
    {
        _handle = handle;
        ClientInput = clientInput;
        ClientOutput = clientOutput;
        _lifetime = lifetime;
        _pumpOutput = pumpOutput;
        _ = stdoutReader;
        _ = stdoutWriter;
    }

    internal Stream ClientInput { get; }

    internal Stream ClientOutput { get; }

    internal static McpProcessHandleStreams Start(IProcessHandle handle, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handle);
        var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var stdoutPipe = new Pipe();
        var clientInput = new ProcessHandleStandardInputStream(handle, lifetime.Token);
        var clientOutput = stdoutPipe.Reader.AsStream();
        var pumpOutput = PumpOutputAsync(handle, stdoutPipe.Writer, lifetime.Token);
        return new McpProcessHandleStreams(
            handle,
            stdoutPipe.Reader,
            stdoutPipe.Writer,
            clientInput,
            clientOutput,
            lifetime,
            pumpOutput);
    }

    internal IClientTransport CreateTransport() => new StreamClientTransport(ClientInput, ClientOutput);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        await _lifetime.CancelAsync().ConfigureAwait(false);
        try
        {
            await _handle.CompleteStandardInputAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
        }

        await _handle.DisposeAsync().ConfigureAwait(false);
        try
        {
            await _pumpOutput.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        _lifetime.Dispose();
    }

    private static async Task PumpOutputAsync(
        IProcessHandle handle,
        PipeWriter writer,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var evt in handle.ReadOutputAsync(cancellationToken).ConfigureAwait(false))
            {
                if (evt is ProcessStandardOutputBytes bytes && !bytes.Bytes.IsEmpty)
                {
                    var flush = await writer.WriteAsync(bytes.Bytes, cancellationToken).ConfigureAwait(false);
                    if (flush.IsCompleted)
                    {
                        break;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            await writer.CompleteAsync().ConfigureAwait(false);
        }
    }

    private sealed class ProcessHandleStandardInputStream(IProcessHandle handle, CancellationToken lifetimeToken): Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            WriteAsync(buffer.AsMemory(offset, count), lifetimeToken).AsTask().GetAwaiter().GetResult();

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetimeToken);
            var result = await handle.WriteStandardInputAsync(buffer.AsMemory(offset, count), linked.Token).ConfigureAwait(false);
            if (result is ProcessStandardInputWriteLimitExceeded limitExceeded)
            {
                throw new IOException(limitExceeded.SafeMessage);
            }

            if (result is ProcessStandardInputWriteUnavailable unavailable)
            {
                throw new IOException(unavailable.SafeMessage);
            }
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetimeToken);
            var result = await handle.WriteStandardInputAsync(buffer, linked.Token).ConfigureAwait(false);
            if (result is ProcessStandardInputWriteLimitExceeded limitExceeded)
            {
                throw new IOException(limitExceeded.SafeMessage);
            }

            if (result is ProcessStandardInputWriteUnavailable unavailable)
            {
                throw new IOException(unavailable.SafeMessage);
            }
        }
    }
}
