// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Planning;



/// <summary>Verifies PlanStateFound behavior and contracts.</summary>
public sealed class PlanStateFoundTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var plan = Plan();
        var found = new PlanStateFound(plan);
        found.Plan.ShouldBe(plan);
    }

    [Fact]
    public void Constructor_WhenPlanIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new PlanStateFound(null!));
        exception.ParamName.ShouldBe("plan");
    }

    [Fact]
    public void Equals_WhenComparedThroughBaseType_UsesValueEquality()
    {
        PlanStateResult first = new PlanStateFound(Plan());
        PlanStateResult second = new PlanStateFound(Plan());
        first.Equals(second).ShouldBeTrue();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new PlanStateFound(Plan());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static PlanId PlanId() => new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
    private static WorkPlan Plan() => new(PlanId(), new PlanRevision(1), "Plan", [new WorkPlanItem(new PlanItemId("one"), "First.", PlanItemStatus.Pending)], Identity(), DateTimeOffset.UnixEpoch);
}
