// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory.Tests;

/// <summary>Composes an isolated in-memory artifact store over the shared deterministic grant authority for the contract suite.</summary>
public sealed class InMemoryArtifactStoreConformanceFixture: ArtifactStoreConformanceFixtureBase
{
    /// <inheritdoc/>
    protected override IArtifactStore CreateStore()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<TimeProvider>(Clock);
        _ = services.AddSingleton<ISecurityGrantStore>(Grants);
        _ = services.AddInMemoryArtifactStore(new ArtifactBackendKey("conformance"));
        return services.BuildServiceProvider().GetRequiredKeyedService<IArtifactStore>("conformance");
    }

    /// <inheritdoc/>
    public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
