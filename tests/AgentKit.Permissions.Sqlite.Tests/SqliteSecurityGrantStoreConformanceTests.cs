// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite.Tests;

using AgentKit.Conformance;

/// <summary>Runs the reusable grant-store contract against durable local SQLite storage.</summary>
public sealed class SqliteSecurityGrantStoreConformanceTests:
    SecurityGrantStoreConformanceTests<SqliteSecurityGrantStoreConformanceFixture>
{
    /// <inheritdoc/>
    protected override SqliteSecurityGrantStoreConformanceFixture CreateFixture() => new();
}
