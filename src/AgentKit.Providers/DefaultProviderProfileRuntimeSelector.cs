// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// The first-party profile runtime selector. It maps captured profile references to registered snapshots and keyed
/// credential sources without performing provider I/O.
/// </summary>
internal sealed class DefaultProviderProfileRuntimeSelector(
    ProviderProfileRegistry registry,
    IServiceProvider serviceProvider): IProviderProfileRuntimeSelector
{
    private readonly ProviderProfileRegistry _registry =
        registry ?? throw new ArgumentNullException(nameof(registry));

    private readonly IServiceProvider _serviceProvider =
        serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <inheritdoc/>
    public ValueTask<ProviderProfileRuntimeSelectionResult> SelectAsync(
        ProviderOperationBinding binding,
        ProtectedSemanticOperationContext operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_registry.Endpoints.TryGetValue(binding.Endpoint, out var endpoint))
        {
            return ValueTask.FromResult<ProviderProfileRuntimeSelectionResult>(
                Unavailable(binding, ProviderFailureKind.InvalidRequest, "The endpoint profile is not registered."));
        }

        if (!_registry.Credentials.TryGetValue(binding.Credential, out var credential))
        {
            return ValueTask.FromResult<ProviderProfileRuntimeSelectionResult>(
                Unavailable(binding, ProviderFailureKind.InvalidRequest, "The credential profile is not registered."));
        }

        if (endpoint.ProviderId != credential.ProviderId || endpoint.ServiceSurface != credential.ServiceSurface)
        {
            return ValueTask.FromResult<ProviderProfileRuntimeSelectionResult>(
                Unavailable(binding, ProviderFailureKind.InvalidRequest, "The endpoint and credential profiles do not describe the same provider surface."));
        }

        var credentialSource = _serviceProvider.GetKeyedService<IProviderCredentialSource>(credential.SourceKey);
        if (credentialSource is null)
        {
            return ValueTask.FromResult<ProviderProfileRuntimeSelectionResult>(
                Unavailable(binding, ProviderFailureKind.InvalidRequest, "No credential source is registered for the selected credential profile."));
        }

        IProviderProfileRuntimeLease lease = new ProviderProfileRuntimeLease(endpoint, credential, credentialSource);
        return ValueTask.FromResult<ProviderProfileRuntimeSelectionResult>(new ProviderProfileRuntimeSelected(lease));
    }

    private static ProviderProfileRuntimeUnavailable Unavailable(
        ProviderOperationBinding binding,
        ProviderFailureKind kind,
        string safeMessage) =>
        new(
            binding,
            new ProviderFailure(
                kind,
                binding.Endpoint.Key.Value is { Length: > 0 }
                    ? new ProviderId(binding.Endpoint.Key.Value)
                    : new ProviderId("provider"),
                null,
                null,
                null,
                null,
                safeMessage,
                null,
                ExtensionData.Empty));
}
