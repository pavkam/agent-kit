// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.InMemory;

using System.Collections.Concurrent;

/// <summary>Retains terminal security decisions in process memory for tests and standalone hosts.</summary>
/// <remarks>Records are append-only for the lifetime of this singleton and are not durable across process loss.</remarks>
public sealed class InMemorySecurityDecisionStore: ISecurityDecisionStore
{
    private readonly ConcurrentQueue<SecurityDecision> _decisions = new();

    /// <summary>Gets every recorded decision in arrival order.</summary>
    /// <value>An immutable snapshot of decisions retained so far.</value>
    public IReadOnlyList<SecurityDecision> Decisions => [.. _decisions];

    /// <inheritdoc/>
    public ValueTask RecordAsync(SecurityDecision decision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);
        cancellationToken.ThrowIfCancellationRequested();
        _decisions.Enqueue(decision);
        return ValueTask.CompletedTask;
    }
}
