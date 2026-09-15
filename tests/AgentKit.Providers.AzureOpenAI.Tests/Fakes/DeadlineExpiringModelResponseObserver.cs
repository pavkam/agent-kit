// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AzureOpenAI.Tests.Fakes;

/// <summary>
/// An <see cref="IModelResponseObserver"/> test double that records every
/// delivered event, advances a <see cref="FakeTimeProvider"/> past the
/// request deadline as soon as the first <see cref="ModelPartCompleted"/>
/// is recorded, and, like a well-behaved observer, refuses any later
/// delivery attempted with an already-cancelled token.
/// </summary>
/// <remarks>
/// This drives the adapter's deadline path after a part has already been
/// completed, so a test can prove the resulting timeout failure carries
/// that part instead of an empty partial-output list. The double is not
/// thread-safe; one attempt delivers its events sequentially.
/// </remarks>
internal sealed class DeadlineExpiringModelResponseObserver: IModelResponseObserver
{
    private readonly List<ModelResponseEvent> _events = [];
    private readonly FakeTimeProvider _timeProvider;
    private readonly TimeSpan _advanceBy;

    /// <summary>Initializes a new instance of the <see cref="DeadlineExpiringModelResponseObserver"/> class.</summary>
    /// <param name="timeProvider">The clock the adapter under test evaluates its deadline against.</param>
    /// <param name="advanceBy">How far to advance <paramref name="timeProvider"/> once a part completes; must exceed the remaining deadline.</param>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="advanceBy"/> is not positive.</exception>
    public DeadlineExpiringModelResponseObserver(FakeTimeProvider timeProvider, TimeSpan advanceBy)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(advanceBy, TimeSpan.Zero);

        _timeProvider = timeProvider;
        _advanceBy = advanceBy;
    }

    /// <summary>Gets the events recorded so far, in delivery order; rejected deliveries are not included.</summary>
    public IReadOnlyList<ModelResponseEvent> Events => _events;

    /// <inheritdoc/>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was already cancelled.</exception>
    public ValueTask OnEventAsync(ModelResponseEvent responseEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(responseEvent);
        cancellationToken.ThrowIfCancellationRequested();

        _events.Add(responseEvent);
        if (responseEvent is ModelPartCompleted && _events.OfType<ModelPartCompleted>().Count() == 1)
        {
            _timeProvider.Advance(_advanceBy);
        }

        return ValueTask.CompletedTask;
    }
}
