// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

/// <summary>Verifies stable diagnostic value mapping for security-audit dispatch outcomes.</summary>
public sealed class SecurityAuditDispatchOutcomeExtensionsTests
{
    [Fact]
    public void ToStableValue_WhenOutcomeIsDefined_ReturnsTheStableValue()
    {
        SecurityAuditDispatchOutcome.Accepted.ToStableValue().ShouldBe("accepted");
        SecurityAuditDispatchOutcome.Unavailable.ToStableValue().ShouldBe("unavailable");
        SecurityAuditDispatchOutcome.Failed.ToStableValue().ShouldBe("failed");
        SecurityAuditDispatchOutcome.TimedOut.ToStableValue().ShouldBe("timed_out");
        SecurityAuditDispatchOutcome.Cancelled.ToStableValue().ShouldBe("cancelled");
    }

    [Fact]
    public void ToStableValue_WhenOutcomeIsUndefined_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => ((SecurityAuditDispatchOutcome) 99).ToStableValue());
        exception.ParamName.ShouldBe("outcome");
    }
}
