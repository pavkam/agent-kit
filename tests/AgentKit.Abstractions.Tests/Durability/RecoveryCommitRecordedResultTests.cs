// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies RecoveryCommitRecordedResult behavior and contracts.</summary>
public sealed class RecoveryCommitRecordedResultTests
{
    [Fact]
    public void RecoveryCommitRecordedResult_Constructor_WhenResultIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new RecoveryCommitRecordedResult(null!));
        exception.ParamName.ShouldBe("result");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsResult()
    {
        var result = DurabilityTestData.Result();
        var decision = new RecoveryCommitRecordedResult(result);
        decision.Result.ShouldBeSameAs(result);
        RecoveryDecision typed = decision;
        _ = typed.ShouldBeOfType<RecoveryCommitRecordedResult>();
    }

    [Fact]
    public void With_WhenResultIsNull_ThrowsArgumentNullException()
    {
        var decision = new RecoveryCommitRecordedResult(DurabilityTestData.Result());
        Should.Throw<ArgumentNullException>(() => _ = decision with { Result = null! }).ParamName.ShouldBe("Result");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new RecoveryCommitRecordedResult(DurabilityTestData.Result());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void With_WhenResultIsValid_UpdatesResult()
    {
        var original = new RecoveryCommitRecordedResult(DurabilityTestData.Result());
        var newResult = DurabilityTestData.Result();
        var updated = original with { Result = newResult };
        updated.Result.ShouldBeSameAs(newResult);
    }
}
