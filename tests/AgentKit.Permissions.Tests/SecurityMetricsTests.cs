// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

public sealed class SecurityMetricsTests
{
    [Fact]
    public void RecordAuthoritySelection_WhenOutcomeIsUndefinedOrDurationIsNegative_ThrowsWithExactParameterNames()
    {
        var invalidOutcome = Should.Throw<ArgumentOutOfRangeException>(() => SecurityMetrics.RecordAuthoritySelection((SecurityAuthoritySelectionOutcome) 99, null));
        var invalidDuration = Should.Throw<ArgumentOutOfRangeException>(() => SecurityMetrics.RecordAuthoritySelection(SecurityAuthoritySelectionOutcome.Selected, TimeSpan.FromTicks(-1)));
        invalidOutcome.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        invalidOutcome.ParamName.ShouldBe("outcome");
        invalidDuration.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        invalidDuration.ParamName.ShouldBe("elapsed");
    }
}
