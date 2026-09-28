// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Activates the backend, journal, lease manager, and recovery policy named by one captured context.</summary>
public interface IDurabilityRuntimeSelector
{
    /// <summary>Activates one attempt-scoped durability runtime.</summary>
    /// <param name="context">The captured durability composition to activate.</param>
    /// <param name="cancellationToken">Cancels before activation completes.</param>
    /// <returns>A closed activation outcome.</returns>
    public ValueTask<DurabilityRuntimeActivationResult> ActivateAsync(
        DurableExecutionContext context,
        CancellationToken cancellationToken = default);
}
