// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes one direct dependency consumed by a registered component.</summary>
/// <remarks>An optional factory boundary identifies the narrow case where resolution occurs in an owned operation scope. It is declarative evidence only and never executes a factory.</remarks>
public sealed record ComponentDependencyDescriptor
{
    /// <summary>Initializes a direct component dependency description.</summary>
    /// <param name="reference">The contract and optional key to resolve.</param>
    /// <param name="cardinality">Whether resolution requires one registration or collects all matching registrations.</param>
    /// <param name="factoryBoundary">Optional evidence for a separately owned operation scope.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cardinality"/> is not a defined value.</exception>
    public ComponentDependencyDescriptor(ComponentContractReference reference, ComponentDependencyCardinality cardinality, ComponentFactoryBoundary? factoryBoundary = null)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentOutOfRangeException.ThrowIfUndefined(cardinality);

        Reference = reference;
        Cardinality = cardinality;
        FactoryBoundary = factoryBoundary;
    }

    /// <summary>Gets the contract address this component consumes.</summary>
    public ComponentContractReference Reference { get; }

    /// <summary>Gets the resolution cardinality.</summary>
    public ComponentDependencyCardinality Cardinality { get; }

    /// <summary>Gets optional operation-scope ownership evidence.</summary>
    public ComponentFactoryBoundary? FactoryBoundary { get; }
}
