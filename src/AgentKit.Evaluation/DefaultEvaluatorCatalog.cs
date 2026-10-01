// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Resolves evaluators from their explicit keyed registrations in the owning composition.</summary>
internal sealed class DefaultEvaluatorCatalog: IEvaluatorCatalog
{
    private readonly IServiceProvider _services;

    /// <summary>Initializes the catalog over the composition that holds keyed evaluators.</summary>
    /// <param name="services">The provider that holds keyed evaluators.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public DefaultEvaluatorCatalog(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The registered evaluator reports a descriptor key other than the key it was registered under.</exception>
    public IEvaluator? Find(EvaluatorKey key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        var evaluator = _services.GetKeyedService<IEvaluator>(key.Value);
        return evaluator is not null && evaluator.Descriptor.Key != key
            ? throw new InvalidOperationException(
                $"The evaluator registered under key '{key}' declares descriptor key '{evaluator.Descriptor.Key}'. A registration key must equal the stable descriptor key.")
            : evaluator;
    }
}
