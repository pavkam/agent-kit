// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>Detects accidental invocation while testing retained per-tool invoker acquisitions.</summary>
/// <remarks>Capture and lease operations must preserve this instance without invoking or directly disposing it.</remarks>
public sealed class CaptureTestToolInvoker: IToolInvoker, IAsyncDisposable
{
    private int _invocations;
    private int _disposals;

    /// <summary>Gets the count of accidental invocation attempts.</summary>
    /// <value>A thread-safe count, normally zero in capture tests.</value>
    public int Invocations => Volatile.Read(ref _invocations);

    /// <summary>Gets the count of direct disposal calls.</summary>
    /// <value>A thread-safe count, normally zero for a borrowed invoker.</value>
    public int Disposals => Volatile.Read(ref _disposals);

    /// <summary>Fails if a capture or lease attempts to execute a tool.</summary>
    /// <param name="context">The nonnull context whose unexpected arrival is counted.</param>
    /// <param name="cancellationToken">Unused because this fake must never be invoked.</param>
    /// <returns>No result; valid calls always throw.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="InvalidOperationException">A tool invocation was attempted.</exception>
    public ValueTask<ToolInvocationResult> InvokeAsync(
        ToolInvocationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = Interlocked.Increment(ref _invocations);
        throw new InvalidOperationException("A source capture must not invoke its tools.");
    }

    /// <summary>Counts direct disposal so tests can distinguish borrowed and owned lifetimes.</summary>
    /// <returns>Synchronous completion after counting the call.</returns>
    public ValueTask DisposeAsync()
    {
        _ = Interlocked.Increment(ref _disposals);
        return ValueTask.CompletedTask;
    }
}
