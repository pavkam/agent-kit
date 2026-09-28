// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names and versions the recoverable operation the security authority can journal.</summary>
/// <remarks>
/// A deferred approval is the one security boundary whose progress genuinely outlives a process: the decision is
/// owned by a human or an external approval system, not by the requesting run. Journaling it records what would let
/// the operation resume, so a process that dies while waiting leaves evidence naming the approval rather than
/// evidence that merely stops.
/// </remarks>
public static class PermissionsDurableOperations
{
    /// <summary>Gets the operation name for waiting on a deferred approval decision.</summary>
    /// <value>The name a durability profile must enable before approval waits are journaled.</value>
    public static DurableOperationName ApprovalWait { get; } = new("agentkit.permissions.approval_wait");

    /// <summary>Gets the published version of the approval-wait manifest shape.</summary>
    /// <value>The version recorded on every approval-wait declaration.</value>
    public static DurableOperationVersion ApprovalWaitVersion { get; } = new("v1");

    /// <summary>Gets every operation name the security authority can journal.</summary>
    /// <value>The complete additive set a profile may enable; the authority journals no other name.</value>
    public static ImmutableArray<DurableOperationName> All { get; } = [ApprovalWait];
}
