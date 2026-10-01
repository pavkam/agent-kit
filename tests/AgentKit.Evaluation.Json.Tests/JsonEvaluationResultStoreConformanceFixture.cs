// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Json.Tests;

/// <summary>Composes a <see cref="JsonEvaluationResultStore"/> over a temporary root through its public registration for the shared suite.</summary>
public sealed class JsonEvaluationResultStoreConformanceFixture: IEvaluationResultStoreConformanceFixture
{
    private readonly JsonEvaluationTestRoot _root = new();
    private ServiceProvider _provider;

    /// <summary>Initializes the isolated composition.</summary>
    public JsonEvaluationResultStoreConformanceFixture()
    {
        _provider = Compose();
        Store = Resolve();
    }

    /// <inheritdoc/>
    public ConformanceCapabilities Capabilities { get; } = new(supportsDurability: true);

    /// <inheritdoc/>
    public IEvaluationResultStore Store { get; private set; }

    /// <inheritdoc/>
    public async ValueTask<IEvaluationResultStore> ReopenAsync(CancellationToken cancellationToken = default)
    {
        await _provider.DisposeAsync();
        _provider = Compose();
        Store = Resolve();
        return Store;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        _root.Dispose();
    }

    private ServiceProvider Compose() =>
        new ServiceCollection().AddJsonEvaluationResultStore(new EvaluationResultStoreKey("json"), _root.Target()).BuildServiceProvider();

    private IEvaluationResultStore Resolve() => _provider.GetRequiredKeyedService<IEvaluationResultStore>("json");
}
