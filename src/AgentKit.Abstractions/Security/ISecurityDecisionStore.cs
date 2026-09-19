// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Durably records every terminal security decision independent of grant, approval, and audit-sink storage.</summary>
/// <remarks>This store retains decision history for retention, replay, and diagnostic reconstruction. It observes decisions after the authority makes them; it never decides, grants, or denies on its own.</remarks>
public interface ISecurityDecisionStore
{
    /// <summary>Records one terminal decision.</summary>
    /// <param name="decision">The closed allow, deny, or approval-required decision.</param>
    /// <param name="cancellationToken">Cancels recording before it commits.</param>
    /// <returns>A task that completes after the record is durable.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="decision"/> is null.</exception>
    public ValueTask RecordAsync(SecurityDecision decision, CancellationToken cancellationToken = default);
}
