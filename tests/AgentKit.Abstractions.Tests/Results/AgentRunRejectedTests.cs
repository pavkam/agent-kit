// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Results;

using AgentKit.TestSupport;

public sealed class AgentRunRejectedTests
{
    [Fact]
    public void Constructor_WhenRejected_ExposesRequestedAddressWithoutFabricatedRunOrStream()
    {
        var error = RunResultTestData.Error(AgentErrorCodes.AuthorizationDenied);
        var rejection = new AgentRunRejected<string>(RunResultTestData.Agent, RunResultTestData.Session, error);
        rejection.Failure.ShouldBeSameAs(error);
        typeof(AgentRunRejected<string>).GetProperty("RunId").ShouldBeNull();
    }

    [Theory]
    [InlineData("agentId")]
    [InlineData("sessionId")]
    [InlineData("failure")]
    public void Constructor_WhenRejectionArgumentsAreInvalid_RejectsExactArgument(string parameter)
    {
        var exception = Should.Throw<ArgumentException>(() => new AgentRunRejected<string>(parameter == "agentId" ? default : RunResultTestData.Agent,
            parameter == "sessionId" ? default : RunResultTestData.Session, parameter == "failure" ? null! : RunResultTestData.Error(AgentErrorCodes.InvalidInput)));
        exception.ParamName.ShouldBe(parameter);
        exception.GetType().ShouldBe(parameter == "failure" ? typeof(ArgumentNullException) : typeof(ArgumentOutOfRangeException));
    }
}
