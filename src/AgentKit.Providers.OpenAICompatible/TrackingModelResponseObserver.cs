// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible;

/// <summary>
/// Forwards one attempt's response events while retaining the next sequence,
/// completed parts, and latest usage needed to produce a matching terminal
/// outcome when transport or parsing is interrupted.
/// </summary>
internal sealed class TrackingModelResponseObserver: IModelResponseObserver
{
    private readonly IModelResponseObserver _inner;
    private readonly ImmutableArray<ContentPart>.Builder _completedParts = ImmutableArray.CreateBuilder<ContentPart>();

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackingModelResponseObserver"/> class.
    /// </summary>
    /// <param name="inner">The observer that receives each validated event.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is null.</exception>
    internal TrackingModelResponseObserver(IModelResponseObserver inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <summary>Gets the sequence number required for the next event.</summary>
    internal long NextSequence { get; private set; }

    /// <summary>Gets the parts whose completion events have been delivered.</summary>
    internal ImmutableArray<ContentPart> CompletedParts => _completedParts.ToImmutable();

    /// <summary>Gets the latest usage whose update event has been delivered.</summary>
    internal ModelUsage? Usage { get; private set; }

    /// <inheritdoc/>
    public async ValueTask OnEventAsync(
        ModelResponseEvent responseEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(responseEvent);

        if (responseEvent.Sequence != NextSequence)
        {
            throw new InvalidOperationException(
                $"Expected model response event sequence {NextSequence}, but received {responseEvent.Sequence}.");
        }

        await _inner.OnEventAsync(responseEvent, cancellationToken).ConfigureAwait(false);

        NextSequence++;

        if (responseEvent is ModelPartCompleted partCompleted)
        {
            _completedParts.Add(partCompleted.Part);
        }
        else if (responseEvent is ModelUsageUpdated usageUpdated)
        {
            Usage = usageUpdated.Usage;
        }
    }
}
