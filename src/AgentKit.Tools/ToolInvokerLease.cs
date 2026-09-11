// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Retains one borrowed invoker and releases its owning capture's acquisition exactly once.</summary>
/// <remarks>Metadata remains available after release; invoker access closes as soon as release starts.</remarks>
internal sealed class ToolInvokerLease: IToolInvokerLease
{
    private readonly Func<ValueTask> _release;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _released;

    /// <summary>Creates one independently releasable acquisition over an already retained binding.</summary>
    /// <param name="tool">The nonnull exact captured descriptor.</param>
    /// <param name="sourceVersion">The nondefault captured source publication.</param>
    /// <param name="invoker">The nonnull borrowed invoker, never disposed directly by this lease.</param>
    /// <param name="release">The nonnull owning capture's observed release operation, invoked once.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sourceVersion"/> is default.</exception>
    internal ToolInvokerLease(ToolDescriptor tool, ToolSourceVersion sourceVersion, IToolInvoker invoker, Func<ValueTask> release)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentOutOfRangeException.ThrowIfEqual(sourceVersion, default);
        ArgumentNullException.ThrowIfNull(invoker);
        ArgumentNullException.ThrowIfNull(release);
        Tool = tool;
        SourceVersion = sourceVersion;
        Invoker = invoker;
        _release = release;
    }

    /// <inheritdoc/>
    public ToolDescriptor Tool { get; }

    /// <inheritdoc/>
    public ToolSourceVersion SourceVersion { get; }

    /// <inheritdoc/>
    public IToolInvoker Invoker
    {
        get
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _released) != 0, this);
            return field;
        }
    }

    /// <summary>Closes invoker access and invokes the owning release callback once.</summary>
    /// <returns>The shared release completion, including the original callback failure when cleanup fails.</returns>
    /// <remarks>Concurrent and repeated calls observe the same completion without retrying or disposing the borrowed invoker.</remarks>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _released, 1) == 0)
        {
            _ = ReleaseOnceAsync();
        }
        return new ValueTask(_completion.Task);
    }

    private async Task ReleaseOnceAsync()
    {
        Debug.Assert(Volatile.Read(ref _released) == 1, "Only the caller that closes the lease starts its release.");
        try
        {
            await _release().ConfigureAwait(false);
            _ = _completion.TrySetResult();
        }
        catch (Exception error)
        {
            _ = _completion.TrySetException(error);
        }
    }
}
