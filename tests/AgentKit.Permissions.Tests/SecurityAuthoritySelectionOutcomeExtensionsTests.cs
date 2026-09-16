// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

/// <summary>Verifies stable diagnostic value mapping for security-authority selection outcomes.</summary>
public sealed class SecurityAuthoritySelectionOutcomeExtensionsTests
{
    [Fact]
    public void ToStableValue_WhenOutcomeIsDefined_ReturnsTheStableValue()
    {
        SecurityAuthoritySelectionOutcome.Selected.ToStableValue().ShouldBe("selected");
        SecurityAuthoritySelectionOutcome.Unavailable.ToStableValue().ShouldBe("unavailable");
        SecurityAuthoritySelectionOutcome.Cancelled.ToStableValue().ShouldBe("cancelled");
        SecurityAuthoritySelectionOutcome.Failed.ToStableValue().ShouldBe("failed");
    }

    [Fact]
    public void ToStableValue_WhenOutcomeIsUndefined_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => ((SecurityAuthoritySelectionOutcome) 99).ToStableValue());
        exception.ParamName.ShouldBe("outcome");
    }
}
