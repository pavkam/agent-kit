// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

using Microsoft.Extensions.DependencyInjection;

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
            if (!binding.Point.Equals(registration.Point) || !binding.ProfileKey.Equals(registration.ProfileKey))
            {
                continue;
            }

            var hook = ResolveHook(binding);
            if (HookRegistrationIds.FromAuthorHookId(hook.Id).Equals(registration.Id))
            {
                return binding;
            }
        }

        throw new HookCompositionException(
            $"Hook registration '{registration.Id}' has no dependency-injection binding.");
    }

    private IHook ResolveHook(HookRegistrationBinding binding) =>
        binding.HookServiceType switch
        {
            var type when type == typeof(IRunStartedHook) => ResolveFrom<IRunStartedHook>(binding),
            var type when type == typeof(IBeforeModelRequestHook) => ResolveFrom<IBeforeModelRequestHook>(binding),
            var type when type == typeof(IBeforeToolInvocationHook) => ResolveFrom<IBeforeToolInvocationHook>(binding),
            _ => throw new HookCompositionException($"Hook service type '{binding.HookServiceType.Name}' is not supported."),
        };

    private IHook ResolveFrom<THookService>(HookRegistrationBinding binding)
        where THookService : class, IHook
    {
        foreach (var candidate in _provider.GetServices<THookService>())
        {
            if (candidate.GetType() == binding.ImplementationType)
            {
                return candidate;
            }
        }

        throw new HookCompositionException(
            $"Hook implementation '{binding.ImplementationType.Name}' is registered for point '{binding.Point}' but is not available from dependency injection.");
    }
}
