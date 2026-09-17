// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Wraps an <see cref="IModelResponseObserver"/> so one attempt's event stream always satisfies the
/// <see cref="ModelResponseEvent"/> ordering contract regardless of how many independent producers
/// (a model adapter and its wire parser, for example) contribute events to it.
/// </summary>
/// <remarks>
/// <para>
/// The wrapper renumbers every forwarded event with a contiguous, strictly increasing
/// <see cref="ModelResponseEvent.Sequence"/> starting at zero, suppresses any
/// <see cref="ModelResponseStarted"/> after the first so an attempt starts exactly once, and drops any
/// event that arrives after a terminal event. It also retains the completed parts and the latest usage
/// seen so far, so a caller that must settle an interrupted attempt can report truthful partial output.
/// </para>
/// <para>
/// Instances are not thread-safe; one attempt delivers its events sequentially from one producer at a time.
/// Exceptions thrown by the inner observer propagate unchanged after the retained state has been updated
/// only for events that were actually delivered.
/// </para>
/// </remarks>
public sealed class SequencingModelResponseObserver: IModelResponseObserver
{
    private readonly IModelResponseObserver _inner;
    private readonly ImmutableArray<ContentPart>.Builder _completedParts = ImmutableArray.CreateBuilder<ContentPart>();

    /// <summary>Initializes a new instance of the <see cref="SequencingModelResponseObserver"/> class.</summary>
    /// <param name="inner">The observer that receives each renumbered event.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is null.</exception>
    public SequencingModelResponseObserver(IModelResponseObserver inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <summary>Gets the sequence number the next forwarded event will carry.</summary>
    /// <value>Zero before any event is delivered; otherwise one more than the last delivered sequence.</value>
    public long NextSequence { get; private set; }

    /// <summary>Gets a value indicating whether <see cref="ModelResponseStarted"/> has been delivered.</summary>
    public bool HasStarted { get; private set; }

    /// <summary>Gets a value indicating whether a terminal event has been delivered.</summary>
    public bool HasTerminated { get; private set; }

    /// <summary>Gets the parts whose <see cref="ModelPartCompleted"/> events have been delivered, in order.</summary>
    public ImmutableArray<ContentPart> CompletedParts => _completedParts.ToImmutable();

    /// <summary>Gets the latest usage whose <see cref="ModelUsageUpdated"/> event has been delivered, when any.</summary>
    public ModelUsage? Usage { get; private set; }

    /// <summary>
    /// Forwards <paramref name="responseEvent"/> with the next contiguous sequence, suppressing a repeated
    /// start and anything after a terminal event.
    /// </summary>
    /// <param name="responseEvent">The event produced by the adapter or its parser.</param>
    /// <param name="cancellationToken">Cancels delivery to the inner observer.</param>
    /// <returns>An operation completing when the inner observer has accepted the event or it was suppressed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="responseEvent"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Delivery was cancelled through <paramref name="cancellationToken"/>.</exception>
    public async ValueTask OnEventAsync(ModelResponseEvent responseEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(responseEvent);
        if (HasTerminated)
        {
            return;
        }

        if (responseEvent is ModelResponseStarted && HasStarted)
        {
            return;
        }

        var renumbered = responseEvent with { Sequence = NextSequence };
        await _inner.OnEventAsync(renumbered, cancellationToken).ConfigureAwait(false);
        NextSequence++;

        switch (renumbered)
        {
            case ModelResponseStarted:
                // Set only after delivery succeeds: if the inner observer throws or the token is
                // cancelled during the first start, HasStarted must stay false so a retried
                // ModelResponseStarted is delivered rather than silently suppressed.
                HasStarted = true;
                break;
            case ModelPartCompleted partCompleted:
                _completedParts.Add(partCompleted.Part);
                break;
            case ModelUsageUpdated usageUpdated:
                Usage = usageUpdated.Usage;
                break;
            case ModelResponseCompleted or ModelResponseFailed or ModelResponseCancelled:
                HasTerminated = true;
                break;
            default:
                break;
        }
    }
}
