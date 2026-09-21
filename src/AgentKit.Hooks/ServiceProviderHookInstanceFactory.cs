// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

/// <summary>Creates activation leases backed by the host <see cref="IServiceProvider"/>.</summary>
public sealed class ServiceProviderHookInstanceFactory: IHookInstanceFactory
{
    private readonly IServiceProvider _provider;
    private readonly IReadOnlyList<HookRegistrationBinding> _bindings;
    private readonly int _maximumInvocationDepth;

    /// <summary>Initializes a new instance of the <see cref="ServiceProviderHookInstanceFactory"/> class.</summary>
    /// <param name="provider">The root composition.</param>
    /// <param name="bindings">Every registration binding registered in the composition.</param>
    /// <param name="options">Validated host hook ceilings.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="provider"/>, <paramref name="bindings"/>, or <paramref name="options"/> is null.
    /// </exception>
    internal ServiceProviderHookInstanceFactory(
        IServiceProvider provider,
        IEnumerable<HookRegistrationBinding> bindings,
        IOptions<AgentHookOptions> options)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(options);

        _provider = provider;
        _bindings = [.. bindings];
        _maximumInvocationDepth = options.Value.MaximumInvocationDepth;
    }

    /// <inheritdoc/>
    public ValueTask<IHookActivationLease> CreateAsync(
        HookCatalogSnapshot catalog,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        cancellationToken.ThrowIfCancellationRequested();

        var leaseBindings = new Dictionary<HookRegistrationId, HookRegistrationBinding>(catalog.Registrations.Length);
        foreach (var registration in catalog.Registrations)
        {
            leaseBindings[registration.Id] = FindBinding(registration);
        }

        IHookActivationLease lease = new HookActivationLease(_provider, catalog, leaseBindings, _maximumInvocationDepth);
        return new ValueTask<IHookActivationLease>(lease);
    }

    private HookRegistrationBinding FindBinding(HookRegistrationDescriptor registration)
    {
        foreach (var binding in _bindings)
        {
            if (binding.Descriptor.Id.Equals(registration.Id))
            {
                return binding;
            }
        }

        throw new HookCompositionException(
            $"Hook registration '{registration.Id}' has no dependency-injection binding.");
    }
}
