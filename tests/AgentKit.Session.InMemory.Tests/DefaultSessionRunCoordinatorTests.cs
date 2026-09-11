// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

using AgentKit.Conformance;

/// <summary>Verifies DefaultSessionRunCoordinator behavior and contracts.</summary>
public sealed class DefaultSessionRunCoordinatorTests: SessionRunCoordinatorConformanceTests<InMemorySessionRunCoordinatorConformanceFixture>
{
    /// <inheritdoc/>
    protected override InMemorySessionRunCoordinatorConformanceFixture CreateFixture() => new();
}
