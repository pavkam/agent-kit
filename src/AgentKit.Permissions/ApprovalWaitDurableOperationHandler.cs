// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Owns the deferred-approval wait boundary for the durability coordinator.</summary>
/// <remarks>
/// A distinct type per boundary keeps additive registration idempotent: repeating this package's registration adds
/// no second handler for the same operation name, which the coordinator rejects outright. The handler grants,
/// widens, and consumes nothing: it bridges the coordinator to the live continuation the approval-wait recorder published while
/// recording the wait, and a recovering process that holds none refuses rather than deciding an approval itself.
/// </remarks>
public sealed class ApprovalWaitDurableOperationHandler
    : DurableBoundaryHandler
{
    /// <summary>Initializes the approval-wait handler over the engine-wide continuation registry.</summary>
    /// <param name="registry">The non-null registry the approval-wait recorder publishes its live attempt into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is <see langword="null"/>.</exception>
    public ApprovalWaitDurableOperationHandler(DurableBoundaryRegistry registry)
        : base(PermissionsDurableOperations.ApprovalWait, registry)
    {
    }
}
