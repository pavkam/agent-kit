// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Results;

using AgentKit.TestSupport;

public sealed class RunPolicyHaltedTests
{
    [Fact]
    public void Constructor_WhenEvidenceIsNull_RejectsExactArgument() => Should.Throw<ArgumentNullException>(() => new RunPolicyHalted(null!)).ParamName.ShouldBe("reason");

    [Fact]
    public void Constructor_WhenFailureIsValid_RetainsOriginalEvidence()
    {
        var error = RunResultTestData.Error(AgentErrorCodes.InternalFailure);
        var outcome = new RunPolicyHalted(new(error));
        outcome.Reason.Error.ShouldBeSameAs(error);
    }
}
