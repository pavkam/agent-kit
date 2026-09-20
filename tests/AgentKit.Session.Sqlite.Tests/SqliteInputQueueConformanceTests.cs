// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite.Tests;

using AgentKit.Conformance;

/// <summary>Runs the shared input-queue contract against the SQLite session store.</summary>
public sealed class SqliteInputQueueConformanceTests: InputQueueConformanceTests<SqliteInputQueueConformanceFixture>
{
    /// <inheritdoc/>
    protected override SqliteInputQueueConformanceFixture CreateFixture() => new();
}
