// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Tests.Fakes;

/// <summary>
/// An <see cref="IModelResponseObserver"/> decorator test double that
/// forwards to an inner observer and cancels a caller-supplied
/// <see cref="CancellationTokenSource"/> after a configured number of
/// events have been delivered, so a test can deterministically simulate
/// cancellation occurring mid-stream, after some output has already been
/// observed.
/// </summary>
internal sealed class CancelingModelResponseObserver: IModelResponseObserver
{
    private readonly IModelResponseObserver _inner;
    private readonly CancellationTokenSource _cancelAfterDelivery;
    private readonly int _cancelAfterEventCount;
    private int _delivered;

    /// <summary>Initializes a new instance of the <see cref="CancelingModelResponseObserver"/> class.</summary>
    /// <param name="inner">The observer to forward every event to.</param>
    /// <param name="cancelAfterDelivery">Cancelled once <paramref name="cancelAfterEventCount"/> events have been delivered.</param>
    /// <param name="cancelAfterEventCount">The number of events to deliver before cancelling.</param>
    public CancelingModelResponseObserver(
        IModelResponseObserver inner,
        CancellationTokenSource cancelAfterDelivery,
        int cancelAfterEventCount)
    {
        _inner = inner;
        _cancelAfterDelivery = cancelAfterDelivery;
        _cancelAfterEventCount = cancelAfterEventCount;
    }

    /// <inheritdoc/>
    public async ValueTask OnEventAsync(ModelResponseEvent responseEvent, CancellationToken cancellationToken = default)
    {
        await _inner.OnEventAsync(responseEvent, cancellationToken).ConfigureAwait(false);

        _delivered++;
        if (_delivered == _cancelAfterEventCount)
        {
            await _cancelAfterDelivery.CancelAsync().ConfigureAwait(false);
        }
    }
}
