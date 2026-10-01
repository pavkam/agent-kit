// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.FileSystem.Tests;

/// <summary>Runs the shared artifact-store contract suite against the file-system adapter over the in-memory volume.</summary>
public sealed class FileSystemArtifactStoreConformanceTests: ArtifactStoreConformanceTests<FileSystemArtifactStoreConformanceFixture>
{
    /// <inheritdoc/>
    protected override FileSystemArtifactStoreConformanceFixture CreateFixture() => new();
}
