// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite.Tests;

using AgentKit.Conformance;

/// <summary>Runs the shared run-coordinator contract against the SQLite session store.</summary>
public sealed class SqliteSessionRunCoordinatorConformanceTests
    : SessionRunCoordinatorConformanceTests<SqliteSessionRunCoordinatorConformanceFixture>
{
    /// <inheritdoc/>
    protected override SqliteSessionRunCoordinatorConformanceFixture CreateFixture() => new();
}
