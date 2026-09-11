// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports terminal handoff of deferred work to durable external ownership.</summary>
/// <remarks>Runtime-owned suspension is nonterminal and cannot be represented by this outcome. The handoff owner persists acceptance before constructing the outcome; settlement remains separate.</remarks>
public sealed record RunDeferred: AgentRunOutcome
{
    /// <summary>Captures coherent nonempty external handoff evidence for one session and run.</summary>
    /// <param name="requests">Initialized, nonnull, unique external requests with matching session and run correlation.</param>
    /// <exception cref="ArgumentNullException">A request is null.</exception>
    /// <exception cref="ArgumentException">The collection is uninitialized, empty, duplicate, runtime-owned, or has inconsistent session/run correlation.</exception>
    public RunDeferred(ImmutableArray<DeferredOperationRequest> requests)
    {
        ArgumentException.ThrowIfInvalidExternalDeferrals(requests);
        Requests = requests;
    }
    /// <summary>Gets the durable external requests accepted by the handoff owner.</summary>
    /// <value>A nonempty immutable array in the original causal order.</value>
    public ImmutableArray<DeferredOperationRequest> Requests { get; }
    /// <summary>Compares ordered external handoff evidence structurally.</summary>
    /// <param name="other">The candidate outcome, or null.</param>
    /// <returns>True for equivalent ordered deferred requests.</returns>
    public bool Equals(RunDeferred? other) => other is not null && Requests.SequenceEqual(other.Requests);
    /// <summary>Hashes ordered handoff evidence consistently with equality.</summary>
    /// <returns>A structural hash over all deferred requests.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode(); foreach (var request in Requests) { hash.Add(request); }
        return hash.ToHashCode();
    }
}
