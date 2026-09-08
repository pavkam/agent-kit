// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Describes one closed DI component registration without constructing its implementation.</summary>
/// <remarks>The descriptor captures only information composition validation needs: service address, implementation, lifetime, and direct dependencies. Its immutable collection preserves the registration author's declared dependency order while preventing later mutation from changing the graph being validated.</remarks>
public sealed record ComponentRegistrationDescriptor
{
    /// <summary>Initializes one concrete component registration description.</summary>
    /// <param name="service">The closed service contract and optional key exposed by the registration.</param>
    /// <param name="implementationType">The closed, concrete type constructed for <paramref name="service"/>.</param>
    /// <param name="lifetime">The Microsoft DI lifetime with which the implementation is registered.</param>
    /// <param name="dependencies">The initialized, non-null direct dependency descriptions; an empty array describes a leaf.</param>
    /// <exception cref="ArgumentNullException"><paramref name="service"/>, <paramref name="implementationType"/>, or an element of <paramref name="dependencies"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="implementationType"/> is not a concrete closed type assignable to <paramref name="service"/>, or <paramref name="dependencies"/> is default.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lifetime"/> is not a defined <see cref="ServiceLifetime"/> value.</exception>
    public ComponentRegistrationDescriptor(ComponentContractReference service, Type implementationType, ServiceLifetime lifetime, ImmutableArray<ComponentDependencyDescriptor> dependencies)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentException.ThrowIfNotConcreteClosedType(implementationType);
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

    /// <summary>Gets the contract and optional key exposed by this registration.</summary>
    public ComponentContractReference Service { get; }

    /// <summary>Gets the concrete implementation type registered for <see cref="Service"/>.</summary>
    public Type ImplementationType { get; }

    /// <summary>Gets the DI lifetime used when resolving this component.</summary>
    public ServiceLifetime Lifetime { get; }

    /// <summary>Gets the immutable direct dependency declarations, which may be empty for a leaf.</summary>
    public ImmutableArray<ComponentDependencyDescriptor> Dependencies { get; }
}
