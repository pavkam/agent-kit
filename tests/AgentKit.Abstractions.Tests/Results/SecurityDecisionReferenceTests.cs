// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Results;

using AgentKit.TestSupport;

public sealed class SecurityDecisionReferenceTests
{
    [Theory]
    [InlineData("requestId")]
    [InlineData("policySnapshot")]
    [InlineData("auditRecordId")]
    public void Constructor_WhenDecisionReferenceIsInvalid_RejectsExactArgument(string parameter)
    {
        var basis = RunResultTestData.Decision();
        var exception = Should.Throw<ArgumentException>(() => new SecurityDecisionReference(
            parameter == "requestId" ? default : basis.RequestId, parameter == "policySnapshot" ? null! : basis.PolicySnapshot,
            parameter == "auditRecordId" ? default : basis.AuditRecordId));
        exception.ParamName.ShouldBe(parameter);
        exception.GetType().ShouldBe(parameter == "policySnapshot" ? typeof(ArgumentNullException) : typeof(ArgumentOutOfRangeException));
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = RunResultTestData.Decision();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
