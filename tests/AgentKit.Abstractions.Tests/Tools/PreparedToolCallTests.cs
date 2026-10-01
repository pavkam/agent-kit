// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

using static ToolRuntimeTestFixture;

/// <summary>Verifies <see cref="PreparedToolCall"/> validation.</summary>
public sealed class PreparedToolCallTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var call = ValidatedCall();
        var plan = ExecutionPlan();
        var prepared = new PreparedToolCall(call, plan);

        prepared.Call.ShouldBeSameAs(call);
        prepared.ExecutionPlan.ShouldBeSameAs(plan);
    }

    [Fact]
    public void Constructor_WhenCallIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new PreparedToolCall(null!, ExecutionPlan())).ParamName.ShouldBe("call");

    [Fact]
    public void Constructor_WhenPlanIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new PreparedToolCall(ValidatedCall(), null!)).ParamName.ShouldBe("executionPlan");

    [Fact]
    public void Constructor_WhenPlanNamesAnotherPolicy_ThrowsExactParameter()
    {
        var other = new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("other"), new ToolExecutionPolicyVersion(1));

        Should.Throw<ArgumentException>(() => new PreparedToolCall(ValidatedCall(), ExecutionPlan(executionPolicy: other)))
            .ParamName.ShouldBe("executionPlan");
    }
}
