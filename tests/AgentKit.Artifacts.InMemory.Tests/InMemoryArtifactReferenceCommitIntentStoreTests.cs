// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory.Tests;

/// <summary>Runs the shared intent-store contract suite against the in-memory adapter.</summary>
public sealed class InMemoryArtifactReferenceCommitIntentStoreTests: ArtifactReferenceCommitIntentStoreConformanceTests<InMemoryArtifactReferenceCommitIntentStoreConformanceFixture>
{
    /// <inheritdoc/>
    protected override InMemoryArtifactReferenceCommitIntentStoreConformanceFixture CreateFixture() => new();
}
