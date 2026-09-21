// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Coordinates retained approval dispatch, responder authorization, and atomic resolution.</summary>
public interface IApprovalBroker
{
    /// <summary>Requests one exact approval and returns only retained terminal evidence.</summary>
    /// <param name="request">The immutable approval request.</param>
    /// <param name="cancellationToken">Cancels waiting without converting cancellation into approval.</param>
    /// <returns>A closed broker result.</returns>
    public ValueTask<ApprovalBrokerResult> RequestAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves one durable approval request with externally supplied terminal evidence.</summary>
    /// <param name="response">The authenticated terminal response to commit.</param>
    /// <param name="cancellationToken">Cancels before resolution commits.</param>
    /// <returns>A closed resolution outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is null.</exception>
    public ValueTask<ApprovalResolutionResult> ResolveAsync(
        ApprovalResponse response,
        CancellationToken cancellationToken = default);
}
