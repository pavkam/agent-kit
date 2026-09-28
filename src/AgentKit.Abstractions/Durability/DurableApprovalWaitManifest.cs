// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The journaled manifest of one deferred approval this operation is waiting on.</summary>
/// <remarks>
/// <para>
/// The manifest names the pending decision, not the operation it would authorize: the requested resources, the
/// approval prompt, and the intersected constraints are security content and stay in the decision and audit stores
/// that already own them.
/// </para>
/// <para>
/// This type is an immutable value object with structural equality over its fields. It carries no mutable state and
/// is safe to share across threads without synchronization.
/// </para>
/// </remarks>
public sealed record DurableApprovalWaitManifest
{
    /// <summary>Initializes one complete approval-wait manifest.</summary>
    /// <param name="securityRequestId">The nonempty identity of the security request whose decision was deferred.</param>
    /// <param name="approvalRequestId">The nonempty identity of the pending approval request.</param>
    /// <param name="operationKind">
    /// The nonblank defined <see cref="SecurityOperationKind"/> name the request describes. It is a classification,
    /// not a resource.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="securityRequestId"/> or <paramref name="approvalRequestId"/> is the empty identity.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="operationKind"/> is null, empty, or whitespace.</exception>
    public DurableApprovalWaitManifest(Guid securityRequestId, Guid approvalRequestId, string operationKind)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(securityRequestId, Guid.Empty, nameof(securityRequestId));
        ArgumentOutOfRangeException.ThrowIfEqual(approvalRequestId, Guid.Empty, nameof(approvalRequestId));
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKind);
        SecurityRequestId = securityRequestId;
        ApprovalRequestId = approvalRequestId;
        OperationKind = operationKind;
    }

    /// <summary>Gets the security request whose decision was deferred.</summary>
    /// <value>The nonempty request identity the eventual terminal decision is recorded against.</value>
    public Guid SecurityRequestId { get; }

    /// <summary>Gets the pending approval request.</summary>
    /// <value>
    /// The nonempty approval identity. It is also the handle a durable wait condition names, because it is the one
    /// value that lets recovery ask whether the decision has since been made.
    /// </value>
    public Guid ApprovalRequestId { get; }

    /// <summary>Gets the protected operation kind the request describes.</summary>
    /// <value>The nonblank defined operation-kind name; never a resource, path, or argument.</value>
    public string OperationKind { get; }
}
