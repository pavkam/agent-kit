// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Results;

public sealed class RunLimitFailureTests
{
    [Theory]
    [InlineData("limit", "null")]
    [InlineData("scope", "range")]
    [InlineData("dimension", "range")]
    [InlineData("unit", "range")]
    [InlineData("kind", "range")]
    [InlineData("ceiling", "range")]
    [InlineData("message", "argument")]
    [InlineData("null-message", "null")]
    [InlineData("enforcementBoundary", "range")]
    [InlineData("sideEffectCertainty", "range")]
    public void Constructor_WhenLimitEvidenceIsInvalid_RejectsExactArgument(string invalid, string exceptionKind)
    {
        var basis = Limit();
        var limit = invalid switch
        {
            "limit" => null,
            "scope" => basis with { ScopeId = default },
            "dimension" => basis with { Dimension = default },
            "unit" => basis with { Unit = default },
            "kind" => basis with { Kind = (BudgetLimitKind) (-1) },
            "ceiling" => basis with { ConfiguredValue = -1 },
            "message" => basis with { SafeMessage = " " },
            "null-message" => basis with { SafeMessage = null! },
            _ => basis,
        };
        var exception = Should.Throw<ArgumentException>(() => new RunLimitFailure(limit!, invalid == "enforcementBoundary" ? default : new ComponentId("test"), true,
            invalid == "sideEffectCertainty" ? (SideEffectCertainty) (-1) : SideEffectCertainty.Unknown));
        exception.ParamName.ShouldBe(invalid is "enforcementBoundary" or "sideEffectCertainty" ? invalid : "limit");
        exception.GetType().ShouldBe(exceptionKind == "range" ? typeof(ArgumentOutOfRangeException) : exceptionKind == "null" ? typeof(ArgumentNullException) : typeof(ArgumentException));
    }

    private static BudgetLimitFailure Limit() => new(new BudgetScopeId(Guid.Parse("00000000-0000-0000-0000-000000000001")),
        BudgetDimensions.InputTokens, BudgetLimitKind.Hard, 10, 12, 1, new BudgetUnit("tokens"), "exhausted");

    [Fact]
    public void Constructor_WhenLimitIsValid_RetainsExactQuantitiesAndEffectEvidence()
    {
        var limit = new RunLimitFailure(Limit(), new ComponentId("budget"), true, SideEffectCertainty.Unknown);
        limit.Limit.ObservedValue.ShouldBe(BudgetQuantity.FromDecimal(12));
        limit.EnforcementBoundary.ShouldBe(new ComponentId("budget"));
        limit.HasPartialOutput.ShouldBeTrue();
        limit.SideEffectCertainty.ShouldBe(SideEffectCertainty.Unknown);
    }
}
