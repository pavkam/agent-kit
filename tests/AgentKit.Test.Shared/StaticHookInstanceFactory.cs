// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using AgentKit.Hooks;

/// <summary>Creates activation leases that resolve hook instances from a fixed registration map.</summary>
public sealed class StaticHookInstanceFactory: IHookInstanceFactory
{
    private readonly IReadOnlyDictionary<HookRegistrationId, object> _hooks;
    private readonly int _maximumInvocationDepth;

    /// <summary>Initializes a new instance of the <see cref="StaticHookInstanceFactory"/> class.</summary>
    /// <param name="hooks">Hook instances keyed by the registration identities in the paired catalog snapshot.</param>
    /// <param name="maximumInvocationDepth">The reentrancy ceiling for created leases.</param>
    /// <exception cref="ArgumentNullException"><paramref name="hooks"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumInvocationDepth"/> is less than 1.</exception>
    public StaticHookInstanceFactory(IReadOnlyDictionary<HookRegistrationId, object> hooks, int maximumInvocationDepth = 8)
    {
        ArgumentNullException.ThrowIfNull(hooks);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumInvocationDepth, 1);

        _hooks = hooks;
        _maximumInvocationDepth = maximumInvocationDepth;
    }

    /// <inheritdoc/>
    public ValueTask<IHookActivationLease> CreateAsync(HookCatalogSnapshot catalog, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        cancellationToken.ThrowIfCancellationRequested();

        IHookActivationLease lease = new StaticHookActivationLease(catalog, _hooks, _maximumInvocationDepth);
        return new ValueTask<IHookActivationLease>(lease);
    }

    private sealed class StaticHookActivationLease: IHookActivationLease
    {
        private readonly IReadOnlyDictionary<HookRegistrationId, object> _hooks;
        private int _disposed;

        public StaticHookActivationLease(
            HookCatalogSnapshot catalog,
            IReadOnlyDictionary<HookRegistrationId, object> hooks,
            int maximumInvocationDepth)
        {
            _ = catalog;
            _hooks = hooks;
            InvocationTracker = new HookInvocationTracker(maximumInvocationDepth);
        }

        public IHookInvocationTracker InvocationTracker { get; }

        public ValueTask<HookInstanceResolution<THook>> ResolveAsync<THook>(
            HookRegistrationId registrationId,
            CancellationToken cancellationToken)
            where THook : class
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            ArgumentOutOfRangeException.ThrowIfEqual(registrationId, default);
            cancellationToken.ThrowIfCancellationRequested();

            return _hooks.TryGetValue(registrationId, out var hook) && hook is THook typed
                ? new ValueTask<HookInstanceResolution<THook>>(new HookInstanceResolved<THook>(typed))
                : new ValueTask<HookInstanceResolution<THook>>(
                    new HookInstanceUnavailable<THook>($"Registration '{registrationId}' is not available on this lease."));
        }

        public ValueTask DisposeAsync()
        {
            _disposed = 1;
            return ValueTask.CompletedTask;
        }
    }
}
