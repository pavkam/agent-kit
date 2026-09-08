// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes one direct dependency declared by a registered component.</summary>
/// <remarks>An optional factory boundary identifies the narrow case where a required singular dependency is resolved in an explicitly owned operation scope. A boundary on any other cardinality is invalid graph metadata. Dependencies use declared component evidence unless they explicitly permit recursive inspection of a frozen Microsoft DI type or instance registration. This is declarative graph evidence only: it never executes a factory or proves the implementation's actual dependency behavior.</remarks>
public sealed record ComponentDependencyDescriptor
{
    /// <summary>Initializes declared metadata for one direct component dependency.</summary>
    /// <param name="reference">The non-null contract address and optional exact key declared for resolution.</param>
    /// <param name="cardinality">The defined cardinality stating whether the component requires one registration, optionally consumes at most one registration, or collects all matching registrations.</param>
    /// <param name="factoryBoundary">Optional metadata describing a separately owned operation scope, or <see langword="null"/> when normal component lifetime rules apply.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cardinality"/> is not a defined value.</exception>
    public ComponentDependencyDescriptor(ComponentContractReference reference, ComponentDependencyCardinality cardinality, ComponentFactoryBoundary? factoryBoundary = null)
        : this(reference, cardinality, factoryBoundary, ComponentDependencyValidationBoundary.DeclaredComponentsOnly)
    {
    }

    /// <summary>Initializes declared metadata for one direct component dependency with an explicit validation boundary.</summary>
    /// <param name="reference">The non-null contract address and optional exact key declared for resolution.</param>
    /// <param name="cardinality">The defined cardinality stating whether the component requires one registration, optionally consumes at most one registration, or collects all matching registrations.</param>
    /// <param name="factoryBoundary">Optional metadata describing a separately owned operation scope, or <see langword="null"/> when normal component lifetime rules apply.</param>
    /// <param name="validationBoundary">The defined evidence boundary; infrastructure evidence is inspected without constructing or resolving services.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cardinality"/> or <paramref name="validationBoundary"/> is not a defined value.</exception>
    public ComponentDependencyDescriptor(
        ComponentContractReference reference,
        ComponentDependencyCardinality cardinality,
        ComponentFactoryBoundary? factoryBoundary,
        ComponentDependencyValidationBoundary validationBoundary)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentOutOfRangeException.ThrowIfUndefined(cardinality);
        ArgumentOutOfRangeException.ThrowIfUndefined(validationBoundary);

        Reference = reference;
        Cardinality = cardinality;
        FactoryBoundary = factoryBoundary;
        ValidationBoundary = validationBoundary;
    }

    /// <summary>Gets the declared contract address consumed by this component.</summary>
    /// <value>A non-null immutable address; it describes a requested registration and is not a resolved service instance.</value>
    public ComponentContractReference Reference { get; }

    /// <summary>Gets the declared resolution cardinality.</summary>
    /// <value>A defined cardinality that determines whether graph validation requires one registration, permits zero or one registration, or consumes a matching collection.</value>
    public ComponentDependencyCardinality Cardinality { get; }

    /// <summary>Gets optional evidence of an explicitly owned operation scope.</summary>
    /// <value>A factory boundary that can isolate a disposable operation lifetime, or <see langword="null"/> when this dependency is resolved under ordinary lifetime rules.</value>
    public ComponentFactoryBoundary? FactoryBoundary { get; }

    /// <summary>Gets the evidence boundary permitted to satisfy this dependency.</summary>
    /// <value>The defined boundary. The default accepts only declared components; the infrastructure boundary permits pure recursive evidence from the frozen Microsoft DI registration snapshot.</value>
    public ComponentDependencyValidationBoundary ValidationBoundary { get; }
}
