// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Results;

public sealed class RunFailureTests
{
    [Fact]
    public void Constructor_WhenEvidenceIsNull_RejectsExactArgument() => Should.Throw<ArgumentNullException>(() => new RunFailure(null!)).ParamName.ShouldBe("error");

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var error = TestSupport.RunResultTestData.Error(AgentErrorCodes.InternalFailure);
        var original = new RunFailure(error);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
