// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Results;

public sealed class RunLimitReachedTests
{
    [Fact]
    public void Constructor_WhenEvidenceIsNull_RejectsExactArgument() => Should.Throw<ArgumentNullException>(() => new RunLimitReached(null!)).ParamName.ShouldBe("limit");

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var limit = new RunLimitFailure(Limit(), new ComponentId("budget"), true, SideEffectCertainty.Unknown);
        var original = new RunLimitReached(limit);
        var copy = original with { };
        copy.ShouldBe(original);
        copy.Limit.ShouldBe(limit);
    }

    private static BudgetLimitFailure Limit() => new(new BudgetScopeId(Guid.Parse("00000000-0000-0000-0000-000000000001")),
        BudgetDimensions.InputTokens, BudgetLimitKind.Hard, 10, 12, 1, new BudgetUnit("tokens"), "exhausted");
}
