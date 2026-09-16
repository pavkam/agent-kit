// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Results;

public sealed class RunSettlementRecoveryRequiredTests
{
    [Fact]
    public void Constructor_WhenEvidenceIsNull_RejectsExactArgument() => Should.Throw<ArgumentNullException>(() => new RunSettlementRecoveryRequired(null!)).ParamName.ShouldBe("failure");

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var error = TestSupport.RunResultTestData.Error(AgentErrorCodes.InternalFailure);
        var original = new RunSettlementRecoveryRequired(error);
        var copy = original with { };
        copy.ShouldBe(original);
        copy.Failure.ShouldBe(error);
    }
}
