// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Describes one closed dependency-injection component registration without constructing its implementation.</summary>
/// <remarks>The descriptor supplies declared graph metadata for validation: service address, implementation, lifetime, and direct dependencies. Its immutable collection preserves declared dependency order. It does not resolve the component or prove that a factory's actual dependencies and disposal behavior obey the declaration.</remarks>
public sealed record ComponentRegistrationDescriptor
{
    /// <summary>Initializes declared metadata for one concrete component registration.</summary>
    /// <param name="service">The non-null closed service contract and optional key exposed by the registration.</param>
    /// <param name="implementationType">The non-null closed concrete reference type declared to implement <paramref name="service"/>.</param>
    /// <param name="lifetime">The defined Microsoft dependency-injection lifetime declared for the implementation.</param>
    /// <param name="dependencies">The non-default immutable direct dependency declarations in author-specified order; an empty collection describes a graph leaf.</param>
    /// <exception cref="ArgumentNullException"><paramref name="service"/>, <paramref name="implementationType"/>, or an element of <paramref name="dependencies"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="implementationType"/> is not a concrete closed reference type assignable to <paramref name="service"/>, or <paramref name="dependencies"/> is default.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lifetime"/> is not a defined <see cref="ServiceLifetime"/> value.</exception>
    public ComponentRegistrationDescriptor(ComponentContractReference service, Type implementationType, ServiceLifetime lifetime, ImmutableArray<ComponentDependencyDescriptor> dependencies)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentException.ThrowIfNotComponentImplementationType(implementationType);
        ArgumentOutOfRangeException.ThrowIfUndefined(lifetime);
        ArgumentException.ThrowIfDefault(dependencies);
        foreach (var dependency in dependencies)
        {
            ArgumentNullException.ThrowIfNull(dependency, nameof(dependencies));
        }
        ArgumentException.ThrowIfNotAssignableTo(implementationType, service.ContractType, nameof(implementationType));

        Service = service;
        ImplementationType = implementationType;
        Lifetime = lifetime;
        Dependencies = dependencies;
    }

    /// <summary>Gets the declared contract address exposed by this registration.</summary>
    /// <value>A non-null immutable service address containing the closed contract type and its keyed or unkeyed selector.</value>
    public ComponentContractReference Service { get; }

    /// <summary>Gets the concrete implementation type declared for <see cref="Service"/>.</summary>
    /// <value>A non-null closed concrete reference type assignable to <see cref="Service"/>'s contract; it is not a constructed instance.</value>
    public Type ImplementationType { get; }

    /// <summary>Gets the declared dependency-injection lifetime for this component.</summary>
    /// <value>A defined lifetime validated against declared dependencies for captive-lifetime violations; the value alone does not create ownership.</value>
    public ServiceLifetime Lifetime { get; }

    /// <summary>Gets the immutable direct dependency declarations in author-specified order.</summary>
    /// <value>A non-default collection with no null elements. An empty collection declares a graph leaf, and a factory boundary may declare a separately owned operation scope.</value>
    public ImmutableArray<ComponentDependencyDescriptor> Dependencies { get; }
}
