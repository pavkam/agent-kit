// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

using AgentKit.Conformance;

/// <summary>Runs reusable exact lane-ownership conformance against the default coordinator and protected in-memory store.</summary>
public sealed class InMemorySessionRunCoordinatorConformanceTests:
    SessionRunCoordinatorConformanceTests<InMemorySessionRunCoordinatorConformanceFixture>
{
    /// <inheritdoc/>
    protected override InMemorySessionRunCoordinatorConformanceFixture CreateFixture() => new();
}
