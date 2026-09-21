// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.InMemory.Tests;

using AgentKit.Conformance;

/// <summary>Runs the shared security-decision-store contract against the in-memory adapter.</summary>
public sealed class InMemorySecurityDecisionStoreConformanceTests
    : SecurityDecisionStoreConformanceTests<InMemorySecurityDecisionStoreConformanceFixture>
{
    /// <inheritdoc/>
    protected override InMemorySecurityDecisionStoreConformanceFixture CreateFixture() => new();
}
