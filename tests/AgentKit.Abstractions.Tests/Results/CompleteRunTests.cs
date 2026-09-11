// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Results;

using AgentKit.TestSupport;

public sealed class CompleteRunTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_WhenCanonicalSuccessOrIdleIsProposed_ClassifiesCompletion(bool success)
    {
        AgentRunOutcome outcome = success ? new RunSucceeded() : new RunIdle();
        new CompleteRun(outcome).Outcome.ShouldBeSameAs(outcome);
    }

    [Theory]
    [InlineData("failure")]
    [InlineData("policy")]
    [InlineData("limit")]
    public void Constructor_WhenCanonicalHaltIsProposed_ClassifiesHalt(string kind)
    {
        var error = RunResultTestData.Error(AgentErrorCodes.InternalFailure);
        AgentRunOutcome outcome = kind switch
        {
            "failure" => new RunFailed(new(error)),
            "policy" => new RunPolicyHalted(new(error)),
            _ => new RunLimitReached(new(new(new BudgetScopeId(Guid.Parse("00000000-0000-0000-0000-000000000001")),
                BudgetDimensions.InputTokens, BudgetLimitKind.Hard, 10, 12, 1, new BudgetUnit("tokens"), "exhausted"),
                new ComponentId("budget"), true, SideEffectCertainty.Unknown)),
        };
        Should.Throw<ArgumentException>(() => new CompleteRun(outcome)).ParamName.ShouldBe("outcome");
    }
}
