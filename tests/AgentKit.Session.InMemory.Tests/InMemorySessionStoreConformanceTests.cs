// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

using AgentKit.Conformance;

/// <summary>Runs the reusable protected session-store contract against the first-party in-memory implementation.</summary>
public sealed class InMemorySessionStoreConformanceTests:
    SessionStoreConformanceTests<InMemorySessionStoreConformanceFixture>
{
    /// <inheritdoc/>
    protected override InMemorySessionStoreConformanceFixture CreateFixture() => new();
}
