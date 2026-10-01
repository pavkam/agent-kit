// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationUsageSummaryTests
{
    [Fact]
    public void None_WhenRead_ReportsNoRequestsAndUnknownTokens()
    {
        EvaluationUsageSummary.None.ModelRequests.ShouldBe(0);
        EvaluationUsageSummary.None.InputTokens.ShouldBeNull();
        EvaluationUsageSummary.None.OutputTokens.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenACountIsNegative_ThrowsWithTheParameterName()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationUsageSummary(-1, null, null)).ParamName.ShouldBe("modelRequests");
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationUsageSummary(0, -1, null)).ParamName.ShouldBe("inputTokens");
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationUsageSummary(0, null, -1)).ParamName.ShouldBe("outputTokens");
    }

    [Fact]
    public void Constructor_WhenTokensAreZero_KeepsThemDistinctFromUnknown()
    {
        var summary = new EvaluationUsageSummary(1, 0, 0);

        summary.InputTokens.ShouldBe(0);
        summary.OutputTokens.ShouldBe(0);
        summary.ShouldNotBe(new EvaluationUsageSummary(1, null, null));
    }
}
