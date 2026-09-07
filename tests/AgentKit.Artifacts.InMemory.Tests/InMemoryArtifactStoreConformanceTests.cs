// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory.Tests;

/// <summary>Runs the reusable artifact-store contract against the in-memory implementation.</summary>
public sealed class InMemoryArtifactStoreConformanceTests: ArtifactStoreConformanceTests<InMemoryArtifactStoreConformanceFixture>
{
    /// <inheritdoc/>
    protected override InMemoryArtifactStoreConformanceFixture CreateFixture() => new();
}
