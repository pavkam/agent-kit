// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Acquires exact drive ownership for one tenant-partitioned session execution lane.
/// </summary>
/// <remarks>
/// Ownership is per tenant, agent/session address, and lane; different lanes and
/// colliding addresses in different tenant partitions remain independent. An
/// acquired lease permits driving only the accepted operation it names. It is
/// neither a security grant nor permission to bypass the session coordinator's
/// separately authorized, serialized durable mutation line. The default
/// implementation is process-local. A distributed implementation must return
/// and enforce fencing evidence rather than infer cluster safety from this API.
/// </remarks>
public interface ISessionRunCoordinator
{
    /// <summary>
    /// Attempts to acquire drive ownership for one exact accepted lane operation.
    /// </summary>
    /// <param name="request">The lease request.</param>
    /// <param name="session">The compiled invocation capability binding the exact profile and coordinator instances.</param>
    /// <param name="cancellationToken">Cancels this caller's acquisition or wait; it does not request durable abort of the accepted operation.</param>
    /// <returns>A task producing an acquired exact lease, the validated current owner when busy, or typed conflict/unavailability.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="session"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before acquisition completes.</exception>
    public ValueTask<SessionRunLeaseResult> AcquireAsync(
        SessionRunLeaseRequest request,
        SessionExecutionCapability session,
        CancellationToken cancellationToken = default);
}
