// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Is the default wait parking: it parks nothing, because no worker occupancy exists to release.</summary>
/// <remarks>A host that executes child runs on a bounded worker replaces this with a parking that gives the waiting session's slot back; this default is correct for a composition in which no waiting run holds an executor permit a child needs.</remarks>
internal sealed class NoOpDelegationWaitParking: IDelegationWaitParking
{
    /// <inheritdoc/>
    public ValueTask<IAsyncDisposable> ParkAsync(SessionId waitingSessionId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(waitingSessionId, default);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IAsyncDisposable>(NoOpLease.Instance);
    }

    private sealed class NoOpLease: IAsyncDisposable
    {
        internal static NoOpLease Instance { get; } = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
