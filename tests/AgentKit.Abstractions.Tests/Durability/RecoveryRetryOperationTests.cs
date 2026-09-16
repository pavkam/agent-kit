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

    [Fact]
    public void RecoveryRetryOperation_WhenAssignedToBaseType_IsOfExactType()
    {
        RecoveryDecision decision = new RecoveryRetryOperation();
        _ = decision.ShouldBeOfType<RecoveryRetryOperation>();
    }

    [Fact]
    public void Equals_WhenComparedAcrossDifferentDecisionTypes_AreNotEqual()
    {
        RecoveryDecision retry = new RecoveryRetryOperation();
        RecoveryDecision notPossible = new RecoveryNotPossible("reason");
        retry.ShouldNotBe(notPossible);
        notPossible.ShouldNotBe(retry);
    }

    [Fact]
    public void Equals_WhenSameValues_AreEqual()
    {
        var first = new RecoveryRetryOperation(DurabilityTestData.Now, new IdempotencyKey("external"));
        var second = new RecoveryRetryOperation(DurabilityTestData.Now, new IdempotencyKey("external"));
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new RecoveryRetryOperation(DurabilityTestData.Now, new IdempotencyKey("external"));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
