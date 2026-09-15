// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>
/// An <see cref="IModelResponseObserver"/> test double that records every delivered event but, like a
/// well-behaved observer, refuses delivery attempted with an already-cancelled token by throwing
/// <see cref="OperationCanceledException"/>. It can also cancel a caller-supplied
/// <see cref="CancellationTokenSource"/> after a configured number of deliveries so a test can drive
/// cancellation mid-stream and then prove the adapter still lands its terminal event.
/// </summary>
/// <remarks>
/// An adapter that delivers its terminal <see cref="ModelResponseCancelled"/> with the caller's cancelled
/// token loses that event against this observer; an adapter that delivers it with
/// <see cref="CancellationToken.None"/> does not. The double is not thread-safe; one attempt delivers its
/// events sequentially.
/// </remarks>
public sealed class TokenHonouringModelResponseObserver: IModelResponseObserver
{
    private readonly List<ModelResponseEvent> _events = [];
    private readonly CancellationTokenSource? _cancelAfterDelivery;
    private readonly int _cancelAfterEventCount;

    /// <summary>Initializes a new instance of the <see cref="TokenHonouringModelResponseObserver"/> class that never cancels the caller itself.</summary>
    public TokenHonouringModelResponseObserver()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenHonouringModelResponseObserver"/> class that cancels
    /// <paramref name="cancelAfterDelivery"/> once <paramref name="cancelAfterEventCount"/> events have been recorded.
    /// </summary>
    /// <param name="cancelAfterDelivery">The caller cancellation source to cancel after the configured delivery count.</param>
    /// <param name="cancelAfterEventCount">The number of successfully recorded events after which cancellation is requested; at least one.</param>
    /// <exception cref="ArgumentNullException"><paramref name="cancelAfterDelivery"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cancelAfterEventCount"/> is less than one.</exception>
    public TokenHonouringModelResponseObserver(CancellationTokenSource cancelAfterDelivery, int cancelAfterEventCount)
    {
        ArgumentNullException.ThrowIfNull(cancelAfterDelivery);
        ArgumentOutOfRangeException.ThrowIfLessThan(cancelAfterEventCount, 1);

        _cancelAfterDelivery = cancelAfterDelivery;
        _cancelAfterEventCount = cancelAfterEventCount;
    }

    /// <summary>Gets the events recorded so far, in delivery order; rejected deliveries are not included.</summary>
    public IReadOnlyList<ModelResponseEvent> Events => _events;

    /// <summary>Gets the number of deliveries rejected because their token was already cancelled.</summary>
    public int RejectedDeliveries { get; private set; }

    /// <summary>
    /// Records <paramref name="responseEvent"/> unless <paramref name="cancellationToken"/> is already cancelled,
    /// in which case the delivery is rejected and counted in <see cref="RejectedDeliveries"/>.
    /// </summary>
    /// <param name="responseEvent">The event being delivered.</param>
    /// <param name="cancellationToken">The delivery token; a cancelled token rejects the delivery.</param>
    /// <returns>A completed operation, or a faulted one carrying <see cref="OperationCanceledException"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="responseEvent"/> is null.</exception>
    public async ValueTask OnEventAsync(ModelResponseEvent responseEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(responseEvent);

        if (cancellationToken.IsCancellationRequested)
        {
            RejectedDeliveries++;
            throw new OperationCanceledException(cancellationToken);
        }

        _events.Add(responseEvent);

        if (_cancelAfterDelivery is not null && _events.Count == _cancelAfterEventCount)
        {
            await _cancelAfterDelivery.CancelAsync().ConfigureAwait(false);
        }
    }
}
