// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

/// <summary>
/// The in-memory <see cref="IBudgetReservation"/> implementation returned by
/// <see cref="InMemoryBudgetScope"/>.
/// </summary>
/// <remarks>
/// When its owning scope has a parent, this reservation is attached to the
/// corresponding reservation obtained from that parent through
/// <see cref="AttachParent"/> before it is ever returned to a caller.
/// Committing or disposing this reservation cascades the same operation to
/// the attached parent reservation, so a hierarchical reservation settles
/// consistently at every enforced level with one caller-visible call.
/// </remarks>
internal sealed class InMemoryBudgetReservation: IBudgetReservation
{
    private const int _stateOpen = 0;
    private const int _stateCommitted = 1;
    private const int _stateDisposed = 2;

    private int _state;
    private IBudgetReservation? _parentReservation;

    /// <summary>Initializes a new instance of the <see cref="InMemoryBudgetReservation"/> class.</summary>
    /// <param name="id">The identity of this reservation.</param>
    /// <param name="scope">The scope this reservation was made against.</param>
    /// <param name="dimension">The dimension this reservation was made for.</param>
    /// <param name="reserved">The originally reserved amount.</param>
    /// <param name="expiresAt">The instant after which this reservation is treated as released if never settled.</param>
    /// <param name="idempotencyKey">The idempotency key this reservation was created for.</param>
    public InMemoryBudgetReservation(
        BudgetReservationId id,
        InMemoryBudgetScope scope,
        BudgetDimension dimension,
        decimal reserved,
        DateTimeOffset expiresAt,
        IdempotencyKey idempotencyKey)
    {
        Id = id;
        Scope = scope;
        Dimension = dimension;
        Reserved = reserved;
        ExpiresAt = expiresAt;
        IdempotencyKey = idempotencyKey;
    }

    /// <inheritdoc/>
    public BudgetReservationId Id { get; }

    /// <summary>Gets the scope this reservation was made against.</summary>
    public InMemoryBudgetScope Scope { get; }

    /// <inheritdoc/>
    public BudgetScopeId ScopeId => Scope.Id;

    /// <inheritdoc/>
    public BudgetDimension Dimension { get; }

    /// <inheritdoc/>
    public decimal Reserved { get; }

    /// <summary>Gets the instant after which this reservation is treated as released if never settled.</summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>Gets the idempotency key this reservation was created for.</summary>
    public IdempotencyKey IdempotencyKey { get; }

    /// <summary>Gets a value indicating whether this reservation is still open (neither committed nor disposed).</summary>
    public bool IsOpen => Volatile.Read(ref _state) == _stateOpen;

    /// <summary>Attaches the corresponding reservation obtained from the owning scope's parent.</summary>
    /// <param name="parentReservation">The parent-level reservation to cascade commit and dispose to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="parentReservation"/> is null.</exception>
    public void AttachParent(IBudgetReservation parentReservation)
    {
        ArgumentNullException.ThrowIfNull(parentReservation);
        _parentReservation = parentReservation;
    }

    /// <inheritdoc/>
    public async ValueTask<BudgetCommitResult> CommitAsync(decimal actual, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(actual);
        cancellationToken.ThrowIfCancellationRequested();

        if (Interlocked.CompareExchange(ref _state, _stateCommitted, _stateOpen) != _stateOpen)
        {
            throw new InvalidOperationException("This reservation was already committed or disposed.");
        }

        var result = Scope.SettleCommit(this, actual);

        if (_parentReservation is not null)
        {
            _ = await _parentReservation.CommitAsync(actual, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _state, _stateDisposed, _stateOpen) != _stateOpen)
        {
            return;
        }

        Scope.SettleRelease(this);

        if (_parentReservation is not null)
        {
            await _parentReservation.DisposeAsync().ConfigureAwait(false);
        }
    }
}
