// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

/// <summary>Verifies <see cref="ContextFreshness"/> evidence.</summary>
public sealed class ContextFreshnessTests
{
    [Fact]
    public void Pinned_WhenRead_HasNoExpiry() => ContextFreshness.Pinned.ExpiresAt.ShouldBeNull();
}
