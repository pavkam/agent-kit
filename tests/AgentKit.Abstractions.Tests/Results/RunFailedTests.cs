// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Results;

using AgentKit.TestSupport;

public sealed class RunFailedTests
{
    [Fact]
    public void Constructor_WhenEvidenceIsNull_RejectsExactArgument() => Should.Throw<ArgumentNullException>(() => new RunFailed(null!)).ParamName.ShouldBe("failure");

    [Fact]
    public void Constructor_WhenFailureIsValid_RetainsOriginalEvidence()
    {
        var error = RunResultTestData.Error(AgentErrorCodes.InternalFailure);
        var outcome = new RunFailed(new(error));
        outcome.Failure.Error.ShouldBeSameAs(error);
    }
}
