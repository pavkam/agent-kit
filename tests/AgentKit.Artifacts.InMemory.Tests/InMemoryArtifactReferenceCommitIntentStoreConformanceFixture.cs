// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory.Tests;

/// <summary>Composes an isolated in-memory reference-commit intent store through its normal registration for the contract suite.</summary>
public sealed class InMemoryArtifactReferenceCommitIntentStoreConformanceFixture: IArtifactReferenceCommitIntentStoreConformanceFixture
{
    private ServiceProvider? _provider;

    /// <inheritdoc/>
    public ValueTask<IArtifactReferenceCommitIntentStore> CreateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _provider ??= new ServiceCollection().AddInMemoryArtifactReferenceCommitIntentStore().BuildServiceProvider();
        return ValueTask.FromResult(_provider.GetRequiredService<IArtifactReferenceCommitIntentStore>());
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _provider?.DisposeAsync() ?? ValueTask.CompletedTask;
}
