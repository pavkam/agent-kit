// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies LeaseRenewed behavior and contracts.</summary>
public sealed class LeaseRenewedTests
{
    [Fact]
    public void LeaseRenewed_Constructor_PreservesExtendedExpiry() => new LeaseRenewed(DurabilityTestData.Now).ExpiresAt.ShouldBe(DurabilityTestData.Now);
}
