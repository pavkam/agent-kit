// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Releases a waiting session's worker occupancy while its run waits on its children.</summary>
/// <remarks>
/// A parent waiting for children must not hold an executor permit its own children need. The coordinator parks the
/// waiting run's session for the lifetime of the returned lease; a host whose worker executes a run in that session gives its
/// slot back and reacquires it when the lease is disposed. The session, not the run, names the occupancy because a host
/// knows the session it provisioned before the engine assigns any run identity. The default implementation parks nothing.
/// Parking releases occupancy only: it never releases a session lock decision, spent budget, or reserved child budget.
/// </remarks>
public interface IDelegationWaitParking
{
    /// <summary>Parks the occupancy held by one session's run until the lease is disposed.</summary>
    /// <param name="waitingSessionId">The session whose run is waiting on its children.</param>
    /// <param name="cancellationToken">Cancels the parking request.</param>
    /// <returns>A lease whose disposal reacquires the occupancy; disposal is idempotent.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="waitingSessionId"/> is default.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<IAsyncDisposable> ParkAsync(SessionId waitingSessionId, CancellationToken cancellationToken = default);
}
