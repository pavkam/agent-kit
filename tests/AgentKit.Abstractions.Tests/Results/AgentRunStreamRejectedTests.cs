// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Results;

using AgentKit.TestSupport;

public sealed class AgentRunStreamRejectedTests
{
    [Fact]
    public void Constructor_WhenEvidenceIsNull_RejectsExactArgument() => Should.Throw<ArgumentNullException>(() => new AgentRunStreamRejected<string>(null!)).ParamName.ShouldBe("rejection");

    [Fact]
    public void Constructor_WhenAdmissionIsRejected_PreservesRejectionWithoutStream()
    {
        var rejection = new AgentRunRejected<string>(RunResultTestData.Agent, RunResultTestData.Session, RunResultTestData.Error(AgentErrorCodes.InvalidInput));
        var start = new AgentRunStreamRejected<string>(rejection);
        start.Rejection.ShouldBeSameAs(rejection);
        typeof(AgentRunStreamRejected<string>).GetProperty("Stream").ShouldBeNull();
    }
}
