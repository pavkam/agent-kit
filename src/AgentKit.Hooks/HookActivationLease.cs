// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Owns scoped hook instances and the invocation tracker for one captured catalog.</summary>
internal sealed class HookActivationLease: IHookActivationLease
{
    private readonly IServiceProvider _rootProvider;
    private readonly IServiceScope? _scope;
    private readonly Dictionary<HookRegistrationId, HookRegistrationBinding> _bindings;
    private readonly Dictionary<HookRegistrationId, object> _resolved = [];
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="HookActivationLease"/> class.</summary>
    /// <param name="rootProvider">The root composition.</param>
    /// <param name="catalog">The captured catalog this lease is scoped to.</param>
    /// <param name="bindings">The registration bindings indexed by registration identity.</param>
    /// <param name="maximumInvocationDepth">The host reentrancy ceiling for this lease.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="rootProvider"/>, <paramref name="catalog"/>, or <paramref name="bindings"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumInvocationDepth"/> is less than 1.</exception>
    public HookActivationLease(
        IServiceProvider rootProvider,
        HookCatalogSnapshot catalog,
        IReadOnlyDictionary<HookRegistrationId, HookRegistrationBinding> bindings,
        int maximumInvocationDepth)
    {
        ArgumentNullException.ThrowIfNull(rootProvider);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumInvocationDepth);

        _rootProvider = rootProvider;
        _scope = rootProvider.CreateScope();
        _bindings = bindings
            .Where(pair => catalog.Registrations.Any(registration => registration.Id.Equals(pair.Key)))
            .ToDictionary(static pair => pair.Key, static pair => pair.Value);
        InvocationTracker = new HookInvocationTracker(maximumInvocationDepth);
    }

    /// <inheritdoc/>
    public IHookInvocationTracker InvocationTracker { get; }

    /// <inheritdoc/>
    public ValueTask<HookInstanceResolution<THook>> ResolveAsync<THook>(
        HookRegistrationId registrationId,
        CancellationToken cancellationToken)
        where THook : class
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        ArgumentOutOfRangeException.ThrowIfEqual(registrationId, default);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_bindings.TryGetValue(registrationId, out var binding))
        {
            return new ValueTask<HookInstanceResolution<THook>>(
                new HookInstanceUnavailable<THook>($"Registration '{registrationId}' is not in this lease's captured catalog."));
        }

        if (!typeof(THook).IsAssignableFrom(binding.ImplementationType))
        {
            return new ValueTask<HookInstanceResolution<THook>>(
                new HookInstanceUnavailable<THook>(
                    $"Registration '{registrationId}' activates '{binding.ImplementationType.Name}', not '{typeof(THook).Name}'."));
        }

        if (binding.Descriptor.Lifetime != HookLifetime.Transient && _resolved.TryGetValue(registrationId, out var cached))
        {
            return new ValueTask<HookInstanceResolution<THook>>((HookInstanceResolved<THook>) cached);
        }

        var provider = binding.Descriptor.Lifetime == HookLifetime.Singleton ? _rootProvider : _scope!.ServiceProvider;
        var services = provider.GetServices(binding.HookServiceType);
        foreach (var service in services)
        {
            if (service is not THook hook || service.GetType() != binding.ImplementationType)
            {
                continue;
            }

            var resolved = new HookInstanceResolved<THook>(hook);
            if (binding.Descriptor.Lifetime != HookLifetime.Transient)
            {
                _resolved[registrationId] = resolved;
            }

            return new ValueTask<HookInstanceResolution<THook>>(resolved);
        }

        return new ValueTask<HookInstanceResolution<THook>>(
            new HookInstanceUnavailable<THook>(
                $"Hook implementation '{binding.ImplementationType.Name}' is not available from dependency injection."));
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _resolved.Clear();
        _scope?.Dispose();

        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
