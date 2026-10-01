// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Resolves each named result store from its explicit keyed registration, never by registration order.</summary>
internal sealed class DefaultEvaluationResultStoreSelector: IEvaluationResultStoreSelector
{
    private readonly IServiceProvider _services;

    /// <summary>Initializes the selector over the composition that holds keyed stores.</summary>
    /// <param name="services">The provider that holds keyed result stores.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public DefaultEvaluationResultStoreSelector(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    /// <inheritdoc/>
    public ValueTask<EvaluationResultStoreSelection> SelectAsync(EvaluationResultStoreKey key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<EvaluationResultStoreSelection>(
            _services.GetKeyedService<IEvaluationResultStore>(key.Value) is { } store
                ? new EvaluationResultStoreSelected(key, store)
                : new EvaluationResultStoreUnavailable(key, "No evaluation result store is registered under the selected key."));
    }
}
