// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

using AgentKit.Conformance;

/// <summary>Runs the shared input-queue contract against the in-memory session store.</summary>
public sealed class InMemoryInputQueueConformanceTests: InputQueueConformanceTests<InMemoryInputQueueConformanceFixture>
{
    /// <inheritdoc/>
    protected override InMemoryInputQueueConformanceFixture CreateFixture() => new();
}
