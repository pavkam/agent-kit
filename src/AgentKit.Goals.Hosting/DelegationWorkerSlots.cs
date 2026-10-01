// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Hosting;

/// <summary>Bounds concurrent child attempts and releases a slot while its run waits on its own child.</summary>
/// <remarks>
/// <para>
/// A child holds one slot for as long as it runs. When that child delegates in turn and waits, the delegation coordinator
/// parks the waiting session here: the slot returns to the pool for the duration of the wait, so a parent never holds the
/// permit its own child needs and a one-slot worker cannot deadlock on nested delegation. When the wait ends the run
/// reacquires a slot before it continues.
/// </para>
/// <para>A session that is not a worker child's, such as a root run's, holds no slot and parks as a no-op. The class is thread-safe.</para>
/// </remarks>
internal sealed class DelegationWorkerSlots: IDelegationWaitParking, IDisposable
{
    private readonly SemaphoreSlim _permits;
    private readonly ConcurrentDictionary<SessionId, Lease> _bound = new();

    /// <summary>Initializes the slot pool.</summary>
    /// <param name="options">The worker options carrying the concurrency bound.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public DelegationWorkerSlots(IOptions<GoalWorkerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var slots = options.Value.MaximumConcurrentChildren;
        _permits = new SemaphoreSlim(slots, slots);
        Capacity = slots;
    }

    /// <summary>Gets the configured number of slots.</summary>
    internal int Capacity { get; }

    /// <summary>Gets the number of slots currently free.</summary>
    internal int Available => _permits.CurrentCount;

    /// <summary>Waits for one free slot.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A lease that returns the slot when disposed.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    internal async ValueTask<Lease> AcquireAsync(CancellationToken cancellationToken)
    {
        await _permits.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Lease(this);
    }

    /// <inheritdoc/>
    public ValueTask<IAsyncDisposable> ParkAsync(SessionId waitingSessionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_bound.TryGetValue(waitingSessionId, out var lease) ? lease.Park() : NoPark.Instance);
    }

    /// <inheritdoc/>
    public void Dispose() => _permits.Dispose();

    /// <summary>Owns one slot and the run bound to it.</summary>
    internal sealed class Lease(DelegationWorkerSlots owner): IAsyncDisposable
    {
        private readonly Lock _gate = new();
        private bool _held = true;
        private bool _disposed;
        private SessionId? _session;

        /// <summary>Binds the session the attempt executes in to this slot so a wait by a run in that session can release it.</summary>
        /// <param name="sessionId">The provisioned child session.</param>
        internal void Bind(SessionId sessionId)
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _session = sessionId;
            }

            _ = owner._bound.TryAdd(sessionId, this);
        }

        /// <summary>Releases the slot for the duration of a wait.</summary>
        /// <returns>A disposable that reacquires a slot when the wait ends.</returns>
        internal IAsyncDisposable Park()
        {
            lock (_gate)
            {
                if (_disposed || !_held)
                {
                    return NoPark.Instance;
                }

                _held = false;
            }

            _ = owner._permits.Release();
            return new Resume(this);
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            bool release;
            SessionId? session;
            lock (_gate)
            {
                if (_disposed)
                {
                    return ValueTask.CompletedTask;
                }

                _disposed = true;
                release = _held;
                _held = false;
                session = _session;
            }

            if (session is { } bound)
            {
                _ = owner._bound.TryRemove(bound, out _);
            }

            if (release)
            {
                _ = owner._permits.Release();
            }

            return ValueTask.CompletedTask;
        }

        private async ValueTask ReacquireAsync()
        {
            await owner._permits.WaitAsync().ConfigureAwait(false);
            lock (_gate)
            {
                if (!_disposed)
                {
                    _held = true;
                    return;
                }
            }

            _ = owner._permits.Release();
        }

        private sealed class Resume(Lease lease): IAsyncDisposable
        {
            public ValueTask DisposeAsync() => lease.ReacquireAsync();
        }
    }

    private sealed class NoPark: IAsyncDisposable
    {
        internal static NoPark Instance { get; } = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
