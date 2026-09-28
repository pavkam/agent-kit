// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

/// <summary>An already-acquired lease whose ownership never changes, for handler tests that perform no durable write.</summary>
/// <remarks>Fencing behavior is covered by the journal and lease-manager conformance suites, so this stub only has to present a stable generation.</remarks>
internal sealed class StubExecutionLease(DurableOperationAddress address, FencingToken fencingToken): IExecutionLease
{
    /// <inheritdoc/>
    public ExecutionLeaseId LeaseId { get; } = new(Guid.Parse("5b2c3d4e-0000-4000-8000-000000000001"));

    /// <inheritdoc/>
    public WorkerId OwnerWorkerId { get; } = new(Guid.Parse("5b2c3d4e-0000-4000-8000-000000000002"));

    /// <inheritdoc/>
    public DurableOperationAddress Address { get; } = address;

    /// <inheritdoc/>
    public FencingToken FencingToken { get; } = fencingToken;

    /// <inheritdoc/>
    public DateTimeOffset ExpiresAt { get; } = DateTimeOffset.UnixEpoch.AddYears(100);

    /// <inheritdoc/>
    public ValueTask<LeaseRenewalResult> RenewAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<LeaseRenewalResult>(new LeaseRenewed(ExpiresAt));

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
