// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Sqlite.Tests;

/// <summary>Runs the shared artifact-store contract suite against the SQLite adapter.</summary>
public sealed class SqliteArtifactStoreConformanceTests: ArtifactStoreConformanceTests<SqliteArtifactStoreConformanceFixture>
{
    /// <inheritdoc/>
    protected override SqliteArtifactStoreConformanceFixture CreateFixture() => new();
}
