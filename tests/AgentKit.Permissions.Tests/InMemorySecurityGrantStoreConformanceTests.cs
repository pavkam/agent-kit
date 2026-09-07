// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

using AgentKit.Conformance;

/// <summary>Runs the reusable grant-store contract suite against the first-party in-memory implementation.</summary>
public sealed class InMemorySecurityGrantStoreConformanceTests:
    SecurityGrantStoreConformanceTests<InMemorySecurityGrantStoreConformanceFixture>
{
    /// <summary>Creates isolated composition for each inherited contract case.</summary>
    /// <returns>The fixture that resolves the first-party store through dependency injection.</returns>
    protected override InMemorySecurityGrantStoreConformanceFixture CreateFixture() => new();
}
