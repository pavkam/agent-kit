// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Discovers hook registrations emitted by first-party <c>Add*Hook&lt;T&gt;</c> registration bindings.</summary>
internal sealed class HookRegistrationBindingSource: IHookRegistrationSource
{
    private readonly IServiceProvider _provider;
    private readonly HookRegistrationBindingRegistry _bindings;
    private readonly Dictionary<HookPointId, HookPointDefinitionRegistration> _points;

    /// <summary>Initializes a new instance of the <see cref="HookRegistrationBindingSource"/> class.</summary>
    /// <param name="provider">The composition used to resolve hook instances.</param>
    /// <param name="bindings">Every registration binding registered in the composition.</param>
    /// <param name="points">Every closed point definition registered in the composition.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="provider"/>, <paramref name="bindings"/>, or <paramref name="points"/> is null.
    /// </exception>
    public HookRegistrationBindingSource(
        IServiceProvider provider,
        IEnumerable<HookRegistrationBinding> bindings,
        IReadOnlyList<HookPointDefinitionRegistration> points)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(points);

        _provider = provider;
        _bindings = [.. bindings];
        _points = points.ToDictionary(static point => point.Point);
    }

    /// <inheritdoc/>
    public ValueTask<HookRegistrationSnapshot> DiscoverAsync(
        HookCatalogRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (_bindings.Count == 0)
        {
            return new ValueTask<HookRegistrationSnapshot>(new HookRegistrationSnapshot([]));
        }

        var registrations = ImmutableArray.CreateBuilder<HookRegistrationDescriptor>();
        var seenImplementationTypes = new HashSet<Type>();
        foreach (var binding in _bindings)
        {
            if (!binding.ProfileKey.Equals(request.ProfileKey) || !seenImplementationTypes.Add(binding.ImplementationType))
            {
                continue;
            }

            if (!_points.TryGetValue(binding.Point, out var pointRegistration))
            {
                throw new HookCompositionException(
                    $"Hook point '{binding.Point}' is not registered in the point-definition catalog.");
            }

            var hook = ResolveHook(binding);
            registrations.Add(binding.ToDescriptor(hook, pointRegistration));
        }

        return new ValueTask<HookRegistrationSnapshot>(new HookRegistrationSnapshot(registrations.ToImmutable()));
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
