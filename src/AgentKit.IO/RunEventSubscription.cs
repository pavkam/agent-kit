// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

using System.Runtime.CompilerServices;

/// <summary>Owns one bounded, single-enumerator, non-owning view of a run's live event delivery.</summary>
/// <remarks>Registration precedes reading, so events buffer before enumeration starts. Normal completion permits draining; abandonment, cancellation and delivery failure release all queued references. None of those operations cancels the run.</remarks>
internal sealed class RunEventSubscription: IAsyncDisposable
{
    private readonly Lock _gate = new();
    private readonly Queue<RunEvent> _events = new();
    private readonly int _capacity;
    private readonly Func<RunEventHubOperation, RunEventHubObservation> _observe;
    private readonly Action<RunEventSubscription> _release;
    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private RunEventSubscriptionState _state;
    private long? _firstUnavailableSequence;
    private int _readerStarted;

    /// <summary>Creates one empty queue with an explicit registration-release owner.</summary>
    /// <param name="capacity">The positive maximum number of queued events.</param>
    /// <param name="release">The nonnull callback removing this recipient from its hub; called outside the queue lock.</param>
    /// <param name="observe">The nonnull factory for isolated observation using this run's immutable correlation.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is not positive.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="release"/> or <paramref name="observe"/> is null.</exception>
    internal RunEventSubscription(int capacity, Action<RunEventSubscription> release, Func<RunEventHubOperation, RunEventHubObservation> observe)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(observe);
        _capacity = capacity;
        _release = release;
        _observe = observe;
    }

    /// <summary>Gets a synchronized snapshot of the subscriber's current delivery state.</summary>
    /// <value>Normal producer completion may later become explicit consumer disposal or cancellation.</value>
    internal RunEventSubscriptionState State { get { lock (_gate) { return _state; } } }

    /// <summary>Offers one already validated and ordered immutable event without blocking the producer.</summary>
    /// <param name="runEvent">The nonnull presequenced event.</param>
    /// <returns>True when queued; false when already closed or disconnected by saturation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="runEvent"/> is null.</exception>
    internal bool Offer(RunEvent runEvent)
    {
        ArgumentNullException.ThrowIfNull(runEvent);
        lock (_gate)
        {
            if (_state != RunEventSubscriptionState.Active) { return false; }
            if (_events.Count == _capacity)
            {
                _firstUnavailableSequence = _events.Peek().Sequence;
                _events.Clear();
                _state = RunEventSubscriptionState.SlowConsumer;
                _ = _changed.TrySetResult();
                return false;
            }
            _events.Enqueue(runEvent);
            _ = _changed.TrySetResult();
            return true;
        }
    }

    /// <summary>Ends producer delivery, retaining buffered events only for normal completion.</summary>
    /// <param name="completed">True for normal event completion; false for premature hub disposal.</param>
    internal void End(bool completed)
    {
        lock (_gate)
        {
            if (_state != RunEventSubscriptionState.Active) { return; }
            _state = completed ? RunEventSubscriptionState.Completed : RunEventSubscriptionState.HubDisposed;
            if (!completed)
            {
                _firstUnavailableSequence = _events.TryPeek(out var first) ? first.Sequence : null;
                _events.Clear();
            }
            _ = _changed.TrySetResult();
        }
    }

    /// <summary>Reads buffered and subsequent events in publication order through exactly one enumeration.</summary>
    /// <param name="cancellationToken">Cancels this subscription only, releases its buffer, and never cancels run work.</param>
    /// <returns>An incremental event stream; normal completion drains the accepted prefix.</returns>
    /// <exception cref="InvalidOperationException">Enumeration has already been started for this subscription.</exception>
    /// <exception cref="RunEventSubscriptionClosedException">The subscriber was disconnected or its active hub was disposed; the exception identifies the first unavailable event.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    internal async IAsyncEnumerable<RunEvent> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _readerStarted, 1) != 0)
        {
            throw new InvalidOperationException("A run-event subscription supports exactly one enumeration.");
        }
        using var observation = _observe(RunEventHubOperation.Read);
        var drained = false;
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RunEvent? next;
                Task? wait;
                lock (_gate)
                {
                    if (_state is RunEventSubscriptionState.SlowConsumer or RunEventSubscriptionState.HubDisposed)
                    {
                        throw new RunEventSubscriptionClosedException(_state, _firstUnavailableSequence);
                    }
                    if (_events.TryDequeue(out next))
                    {
                        if (_events.Count == 0 && _state == RunEventSubscriptionState.Active)
                        {
                            _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
                        }
                        wait = null;
                    }
                    else
                    {
                        wait = _state == RunEventSubscriptionState.Active ? _changed.Task : null;
                    }
                }
                if (next is not null) { yield return next; }
                else if (wait is not null) { await wait.WaitAsync(cancellationToken).ConfigureAwait(false); }
                else { drained = true; yield break; }
            }
        }
        finally
        {
            var state = State;
            Release(cancellationToken.IsCancellationRequested);
            observation.Finish(cancellationToken.IsCancellationRequested ? RunEventHubOutcome.Cancelled
                : state == RunEventSubscriptionState.SlowConsumer ? RunEventHubOutcome.SlowConsumer
                : state == RunEventSubscriptionState.HubDisposed ? RunEventHubOutcome.HubDisposed
                : state == RunEventSubscriptionState.Disposed ? RunEventHubOutcome.Abandoned
                : drained ? RunEventHubOutcome.Succeeded : RunEventHubOutcome.Abandoned);
        }
    }

    /// <summary>Explicitly abandons this subscriber, releasing its registration and all pending event references.</summary>
    /// <returns>A synchronously completed operation; repeated disposal is harmless and never affects other subscribers or the run.</returns>
    public ValueTask DisposeAsync()
    {
        using var observation = _observe(RunEventHubOperation.Unsubscribe);
        Release(cancelled: false);
        observation.Finish(RunEventHubOutcome.Succeeded);
        return ValueTask.CompletedTask;
    }

    /// <summary>Releases local queue state before notifying the hub, preserving lock ordering and abnormal delivery evidence.</summary>
    /// <param name="cancelled">Whether the owning enumeration was cancelled.</param>
    private void Release(bool cancelled)
    {
        lock (_gate)
        {
            if (_state is RunEventSubscriptionState.Active or RunEventSubscriptionState.Completed)
            {
                _state = cancelled ? RunEventSubscriptionState.Cancelled : RunEventSubscriptionState.Disposed;
            }
            _events.Clear();
            _ = _changed.TrySetResult();
        }
        _release(this);
    }
}
