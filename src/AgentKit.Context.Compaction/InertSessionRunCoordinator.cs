// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>Placeholder run coordinator used only to construct legacy session capabilities.</summary>
internal sealed class InertSessionRunCoordinator: ISessionRunCoordinator
{
    /// <summary>Gets the shared inert instance.</summary>
    public static InertSessionRunCoordinator Instance { get; } = new();

    private InertSessionRunCoordinator()
    {
    }

    /// <inheritdoc/>
    public ValueTask<SessionRunLeaseResult> AcquireAsync(
        SessionRunLeaseRequest request,
        SessionExecutionCapability session,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Compaction activation does not acquire run leases through this placeholder.");
}
