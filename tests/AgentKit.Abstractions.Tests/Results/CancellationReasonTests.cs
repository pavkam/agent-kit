// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Results;

using AgentKit.TestSupport;

public sealed class CancellationReasonTests
{
    [Fact]
    public void Constructor_WhenTimeoutIsPresentedAsCancellation_RejectsMisclassification() =>
        Should.Throw<ArgumentException>(() => new CancellationReason(RunResultTestData.Error(AgentErrorCodes.Timeout))).ParamName.ShouldBe("error");

    [Fact]
    public void Constructor_WhenEvidenceIsNull_RejectsExactArgument() => Should.Throw<ArgumentNullException>(() => new CancellationReason(null!)).ParamName.ShouldBe("error");
}
