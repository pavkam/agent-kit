// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Owns atomic persistence and idempotency for approval requests and terminal responses.</summary>
public interface IApprovalStore
{
    /// <summary>Gets the persistence guarantees of this implementation.</summary>
    public ApprovalStoreCapabilities Capabilities { get; }

    /// <summary>Creates a request without replacing evidence already bound to its identity.</summary>
    /// <param name="request">The immutable approval request.</param>
    /// <param name="cancellationToken">Cancels waiting for storage without changing an already committed result.</param>
    /// <returns>The atomic create outcome.</returns>
    public ValueTask<ApprovalStoreCreateResult> CreateAsync(ApprovalRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Atomically records one terminal response without replacing an earlier decision.</summary>
    /// <param name="response">The authenticated terminal response.</param>
    /// <param name="cancellationToken">Cancels waiting for storage without changing an already committed result.</param>
    /// <returns>The atomic resolution outcome.</returns>
    public ValueTask<ApprovalStoreResolveResult> ResolveAsync(ApprovalResponse response,
        CancellationToken cancellationToken = default);

    /// <summary>Reads retained request and resolution evidence.</summary>
    /// <param name="requestId">The request identity.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The retained state, or an empty result when absent.</returns>
    public ValueTask<ApprovalStoreReadResult> ReadAsync(ApprovalRequestId requestId,
        CancellationToken cancellationToken = default);
}
