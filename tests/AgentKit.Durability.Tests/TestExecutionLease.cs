// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Tests;

/// <summary>A controllable execution lease whose generation and renewal answer are set by the test.</summary>
/// <remarks>
/// The lease models the one property that matters to the components under test: the generation a write must present,
/// and whether a renewal attempt still succeeds. Nothing here coordinates ownership, because the components under test
/// must never treat a lease handle as proof that ownership is still held.
/// </remarks>
internal sealed class TestExecutionLease: IExecutionLease
{
    private int _renewals;

    /// <summary>Initializes a lease over one address at one generation.</summary>
    /// <param name="address">The non-null operation this lease claims.</param>
    /// <param name="fencingToken">The generation every write under this lease must present.</param>
    internal TestExecutionLease(DurableOperationAddress address, FencingToken fencingToken)
    {
        ArgumentNullException.ThrowIfNull(address);
        Address = address;
        FencingToken = fencingToken;
    }

    /// <inheritdoc/>
    public ExecutionLeaseId LeaseId { get; } = new(Guid.Parse("90000000-0000-0000-0000-000000000001"));

    /// <inheritdoc/>
    public WorkerId OwnerWorkerId { get; } = new(Guid.Parse("91000000-0000-0000-0000-000000000001"));

    /// <inheritdoc/>
    public DurableOperationAddress Address { get; }

    /// <inheritdoc/>
    public FencingToken FencingToken { get; }

    /// <inheritdoc/>
    public DateTimeOffset ExpiresAt { get; } = DurableJournalTestData.Now.AddMinutes(1);

    /// <summary>Gets or sets whether the next renewal reports that ownership has passed on.</summary>
    /// <value>True to answer <see cref="LeaseLost"/>; false to answer <see cref="LeaseRenewed"/>.</value>
    internal bool ReportLost { get; set; }

    /// <summary>Gets or sets whether renewal throws instead of answering.</summary>
    /// <value>True to model a lease manager that has become unreachable.</value>
    internal bool FailRenewal { get; set; }

    /// <summary>Gets how many renewal attempts this lease observed.</summary>
    /// <value>The count of completed <see cref="RenewAsync"/> calls, including refusals.</value>
    internal int Renewals => Volatile.Read(ref _renewals);

    /// <summary>Gets whether the lease was disposed.</summary>
    /// <value>True once <see cref="DisposeAsync"/> ran.</value>
    internal bool Disposed { get; private set; }

    /// <inheritdoc/>
    public ValueTask<LeaseRenewalResult> RenewAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = Interlocked.Increment(ref _renewals);
        return FailRenewal
            ? throw new InvalidOperationException("The test lease manager is unavailable.")
            : ValueTask.FromResult<LeaseRenewalResult>(ReportLost
                ? new LeaseLost(new FencingToken(FencingToken.Value + 1))
                : new LeaseRenewed(ExpiresAt.AddMinutes(1)));
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}
