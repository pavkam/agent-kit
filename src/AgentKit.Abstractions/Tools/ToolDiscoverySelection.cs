// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures a complete run-bound authored toolset selection and its exact discovery providers before source I/O.</summary>
/// <remarks>Toolsets retain authored order, while each source appears once in first-use order. This value borrows providers, grants no authority, and neither discovers nor owns source captures.</remarks>
public sealed record ToolDiscoverySelection
{
    /// <summary>Validates complete toolset, policy-family, and source membership evidence without querying providers.</summary>
    /// <param name="request">The nonnull coherent originating discovery request.</param>
    /// <param name="toolsets">Initialized nonnull exact publications matching every authored toolset and policy family in order.</param>
    /// <param name="providers">Initialized nonnull source bindings matching the distinct selected sources in first-use order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentException">An array is default or contains null, publication selection differs from the request, or source membership/order differs.</exception>
    public ToolDiscoverySelection(ToolDiscoveryRequest request, ImmutableArray<ToolsetPublication> toolsets, ImmutableArray<ToolProviderBinding> providers)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfContainsNull(toolsets);
        ArgumentException.ThrowIfContainsNull(providers);
        ArgumentException.ThrowIfNotEqual(toolsets.Length, request.Toolsets.Length, nameof(toolsets));
        for (var index = 0; index < toolsets.Length; index++)
        {
            ArgumentException.ThrowIfNotEqual(toolsets[index].Key, request.Toolsets[index].Key, nameof(toolsets));
            ArgumentException.ThrowIfNotEqual(toolsets[index].ExecutionPolicy.Key, request.Toolsets[index].ExecutionPolicyKey, nameof(toolsets));
        }
        var sources = toolsets.SelectMany(static toolset => toolset.Sources).Select(static source => source.SourceId).Distinct().ToArray();
        ArgumentException.ThrowIfNotEqual(providers.Length, sources.Length, nameof(providers));
        for (var index = 0; index < sources.Length; index++)
        {
            ArgumentException.ThrowIfNotEqual(providers[index].SourceId, sources[index], nameof(providers));
        }
        Request = request;
        Toolsets = toolsets;
        Providers = providers;
    }

    /// <summary>Gets the immutable identity, authorization, definition, and configuration evidence supplied by the caller.</summary>
    /// <value>The original request, without substituting or widening its selection.</value>
    public ToolDiscoveryRequest Request { get; }

    /// <summary>Gets exact publications in authored request order.</summary>
    /// <value>An initialized immutable array retaining each toolset version and execution-policy reference.</value>
    public ImmutableArray<ToolsetPublication> Toolsets { get; }

    /// <summary>Gets each exact borrowed source provider once, in first selected use order.</summary>
    /// <value>An initialized immutable array; an empty selection exposes no source provider.</value>
    public ImmutableArray<ToolProviderBinding> Providers { get; }

    /// <summary>Compares full request/publication evidence and exact borrowed binding identities in order.</summary>
    /// <param name="other">The selection to compare, or null.</param>
    /// <returns>True when the request and ordered publications match and provider bindings are the same retained instances.</returns>
    public bool Equals(ToolDiscoverySelection? other) => other is not null && Request == other.Request && Toolsets.SequenceEqual(other.Toolsets) && Providers.SequenceEqual(other.Providers);

    /// <summary>Hashes complete request/publication evidence and borrowed binding identities.</summary>
    /// <returns>A hash consistent with ordered structural equality.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Request);
        foreach (var toolset in Toolsets) { hash.Add(toolset); }
        foreach (var provider in Providers) { hash.Add(provider); }
        return hash.ToHashCode();
    }
}
