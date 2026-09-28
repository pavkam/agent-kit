// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Performs one version-checked compaction activation append.</summary>
public interface ICompactionActivationCoordinator
{
    /// <summary>Activates one validated compaction candidate against the session.</summary>
    /// <param name="request">The activation request.</param>
    /// <param name="session">The session capability for this invocation.</param>
    /// <param name="activationGrant">The grant authorizing the append.</param>
    /// <param name="cancellationToken">A token used to cancel activation.</param>
    /// <returns>A task that resolves to the closed activation outcome.</returns>
    /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
    public Task<CompactionActivationResult> ActivateAsync(
        CompactionActivationRequest request,
        SessionExecutionCapability session,
        SecurityGrant activationGrant,
        CancellationToken cancellationToken = default);
}
