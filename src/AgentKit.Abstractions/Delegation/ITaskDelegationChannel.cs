// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Creates, runs, joins, and terminally settles already-authorized durable child goals.</summary>
/// <remarks>Implementations must resolve an active parent goal, narrow authority and catalogs, create child state idempotently, and never mutate parent history.</remarks>
public interface ITaskDelegationChannel
{
    /// <summary>Dispatches one grant-free child envelope after broker enforcement.</summary>
    /// <param name="prompt">The exact authorized envelope.</param>
    /// <param name="cancellationToken">Cancels waiting under the captured cancellation relationship.</param>
    /// <returns>A rejection without invented child identities or a terminal child result.</returns>
    public ValueTask<TaskDelegationResult> DelegateAsync(TaskDelegationPrompt prompt, CancellationToken cancellationToken = default);
}
