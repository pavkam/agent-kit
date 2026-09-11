// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics;

/// <summary>Provides complete immutable catalog contributions and collision evidence to one merge decision.</summary>
/// <remarks>The catalog constructs this context after validating every selected source, retains deterministic order, and revalidates the decision against these exact candidates. This value performs no discovery or authorization.</remarks>
public sealed record ToolCatalogMergeContext
{
    /// <summary>Captures already validated discovery and merge evidence.</summary>
    /// <param name="request">The nonnull coherent run-bound discovery request.</param>
    /// <param name="candidates">Initialized, distinct, nonnull contributions in toolset/source/descriptor order.</param>
    /// <param name="collisions">Initialized, nonnull complete collision evidence in deterministic order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentException">An array is default or contains null, candidates repeat, or a collision references an absent candidate.</exception>
    /// <remarks>The catalog owns proving completeness and correspondence to selected publications; construction checks local structural integrity only.</remarks>
    public ToolCatalogMergeContext(ToolDiscoveryRequest request, ImmutableArray<ToolCatalogCandidate> candidates, ImmutableArray<ToolCatalogCollision> collisions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfContainsNull(candidates);
        ArgumentException.ThrowIfContainsNull(collisions);
        var available = candidates.ToHashSet();
        ArgumentException.ThrowIfNotEqual(available.Count, candidates.Length, nameof(candidates));
        foreach (var collision in collisions)
        {
            var members = collision switch
            {
                ToolCatalogIdentityCollision identity => identity.Candidates,
                ToolCatalogAliasCollision alias => alias.Candidates,
                ToolCatalogMissingAliasTarget => [],
                _ => throw new UnreachableException(),
            };
            ArgumentException.ThrowIfNotEqual(members.All(available.Contains), true, nameof(collisions));
        }
        Request = request;
        Candidates = candidates;
        Collisions = collisions;
    }

    /// <summary>Gets the coherent identity and run evidence for the decision.</summary>
    /// <value>The original discovery request; it grants no authority.</value>
    public ToolDiscoveryRequest Request { get; }

    /// <summary>Gets the only descriptor, source, alias, and policy contributions a decision may select.</summary>
    /// <value>An initialized immutable sequence in deterministic contribution order.</value>
    public ImmutableArray<ToolCatalogCandidate> Candidates { get; }

    /// <summary>Gets all ambiguities and missing targets identified before policy invocation.</summary>
    /// <value>Identity groups first, alias groups second, missing assignments last, preserving first authored occurrence within each category.</value>
    public ImmutableArray<ToolCatalogCollision> Collisions { get; }

    /// <summary>Compares the complete run, contribution, and ordered collision evidence.</summary>
    /// <param name="other">The context to compare, or null.</param>
    /// <returns>True when the request and both ordered arrays are structurally equal.</returns>
    public bool Equals(ToolCatalogMergeContext? other) => other is not null && Request == other.Request && Candidates.SequenceEqual(other.Candidates) && Collisions.SequenceEqual(other.Collisions);

    /// <summary>Hashes the complete ordered context evidence.</summary>
    /// <returns>A structural hash consistent with equality.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Request);
        foreach (var candidate in Candidates) { hash.Add(candidate); }
        foreach (var collision in Collisions) { hash.Add(collision); }
        return hash.ToHashCode();
    }
}
