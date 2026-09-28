// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Sqlite.Tests;

/// <summary>Runs the shared <see cref="IDurableLeaseManager"/> contract suite against the SQLite adapter.</summary>
public sealed class SqliteDurableLeaseManagerConformanceTests
    : DurableLeaseManagerConformanceTests<SqliteDurableLeaseManagerConformanceFixture>;
