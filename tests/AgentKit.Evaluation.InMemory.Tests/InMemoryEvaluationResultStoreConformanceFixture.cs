// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.InMemory.Tests;

/// <summary>Composes an <see cref="InMemoryEvaluationResultStore"/> through its public registration for the shared suite.</summary>
public sealed class InMemoryEvaluationResultStoreConformanceFixture: IEvaluationResultStoreConformanceFixture
{
    private readonly ServiceProvider _provider;

    /// <summary>Initializes the isolated composition.</summary>
    public InMemoryEvaluationResultStoreConformanceFixture()
    {
        _provider = new ServiceCollection().AddInMemoryEvaluationResultStore(new EvaluationResultStoreKey("memory")).BuildServiceProvider();
        Store = _provider.GetRequiredKeyedService<IEvaluationResultStore>("memory");
    }

    /// <inheritdoc/>
    public ConformanceCapabilities Capabilities { get; } = new(supportsDurability: false);

    /// <inheritdoc/>
    public IEvaluationResultStore Store { get; }

    /// <inheritdoc/>
    public ValueTask<IEvaluationResultStore> ReopenAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Store);

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
