// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies RecoveryRetryOperation behavior and contracts.</summary>
public sealed class RecoveryRetryOperationTests
{
    [Fact]
    public void RecoveryRetryOperation_Constructor_WhenOptionalsOmitted_AreNull()
    {
        var decision = new RecoveryRetryOperation();
        decision.NotBefore.ShouldBeNull();
        decision.ExternalIdempotencyKey.ShouldBeNull();
    }

    [Fact]
    public void RecoveryRetryOperation_Constructor_PreservesNotBeforeAndKey()
    {
        var decision = new RecoveryRetryOperation(DurabilityTestData.Now, new IdempotencyKey("external"));
        decision.NotBefore.ShouldBe(DurabilityTestData.Now);
        decision.ExternalIdempotencyKey.ShouldBe(new IdempotencyKey("external"));
    }
}
