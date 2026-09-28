// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Activates the backend, journal, lease manager, and recovery policy named by a captured context.</summary>
/// <remarks>
/// Activation resolves the exact keys persisted in <see cref="DurableExecutionContext"/>, never the agent's current
/// profile, so resume cannot silently adopt a different composition. When any one component is missing the selector
/// fails closed with <see cref="DurabilityRuntimeActivationFailed"/> instead of degrading to a partial runtime. The
/// returned lease is scoped to one execution or recovery attempt and the caller owns its disposal.
/// </remarks>
/// <param name="services">The container holding the keyed durability component registrations.</param>
internal sealed class DefaultDurabilityRuntimeSelector(IServiceProvider services): IDurabilityRuntimeSelector
{
    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was already cancelled.</exception>
    public ValueTask<DurabilityRuntimeActivationResult> ActivateAsync(
        DurableExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var scope = services.CreateScope();
        try
        {
            var provider = scope.ServiceProvider;
            var backend = provider.GetKeyedService<IDurableExecutionBackend>(context.BackendKey.Value);
            var journal = provider.GetKeyedService<IDurableOperationJournal>(context.JournalKey.Value);
            var leaseManager = provider.GetKeyedService<IDurableLeaseManager>(context.LeaseManagerKey.Value);
            var recoveryPolicy = provider.GetKeyedService<IRecoveryPolicy>(context.RecoveryPolicyKey.Value);
            if (backend is null || journal is null || leaseManager is null || recoveryPolicy is null)
            {
                scope.Dispose();
                return ValueTask.FromResult<DurabilityRuntimeActivationResult>(
                    new DurabilityRuntimeActivationFailed(
                        "One or more durability runtime components are not registered for the captured context."));
            }

            return ValueTask.FromResult<DurabilityRuntimeActivationResult>(
                new DurabilityRuntimeActivated(
                    new DurabilityRuntimeLease(scope, context, backend, journal, leaseManager, recoveryPolicy)));
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }
}
