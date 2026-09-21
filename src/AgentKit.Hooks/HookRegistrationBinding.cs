// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

/// <summary>One DI-registered hook implementation bound to one captured registration descriptor.</summary>
internal sealed record HookRegistrationBinding
{
    /// <summary>Initializes a new instance of the <see cref="HookRegistrationBinding"/> record.</summary>
    /// <param name="descriptor">The registration descriptor supplied at composition time.</param>
    /// <param name="implementationType">The concrete hook implementation type.</param>
    /// <param name="hookServiceType">The closed hook interface used to resolve the implementation from DI.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="descriptor"/>, <paramref name="implementationType"/>, or <paramref name="hookServiceType"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="hookServiceType"/> is not an interface, or <paramref name="implementationType"/> does not
    /// implement <paramref name="hookServiceType"/>.
    /// </exception>
    public HookRegistrationBinding(
        HookRegistrationDescriptor descriptor,
        Type implementationType,
        Type hookServiceType)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(implementationType);
        ArgumentNullException.ThrowIfNull(hookServiceType);
        if (!hookServiceType.IsInterface)
        {
            throw new ArgumentException("Hook service type must be an interface.", nameof(hookServiceType));
        }

        if (!hookServiceType.IsAssignableFrom(implementationType))
        {
            throw new ArgumentException("Implementation type must implement the hook service interface.", nameof(implementationType));
        }

        Descriptor = descriptor;
        ImplementationType = implementationType;
        HookServiceType = hookServiceType;
    }

    /// <summary>Gets the registration descriptor supplied at composition time.</summary>
    public HookRegistrationDescriptor Descriptor { get; }

    /// <summary>Gets the concrete hook implementation type.</summary>
    public Type ImplementationType { get; }

    /// <summary>Gets the closed hook interface used to resolve the implementation from DI.</summary>
    public Type HookServiceType { get; }
}
