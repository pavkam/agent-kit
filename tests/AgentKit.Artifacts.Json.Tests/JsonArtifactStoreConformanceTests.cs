// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Json.Tests;

/// <summary>Runs the shared artifact-store contract suite against the JSON adapter.</summary>
public sealed class JsonArtifactStoreConformanceTests: ArtifactStoreConformanceTests<JsonArtifactStoreConformanceFixture>
{
    /// <inheritdoc/>
    protected override JsonArtifactStoreConformanceFixture CreateFixture() => new();
}
