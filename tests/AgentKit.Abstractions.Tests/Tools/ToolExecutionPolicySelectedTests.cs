// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

using static ToolRuntimeTestFixture;

/// <summary>Verifies <see cref="ToolExecutionPolicySelected"/>.</summary>
public sealed class ToolExecutionPolicySelectedTests
{
    [Fact]
    public void Constructor_WhenPolicyIsValid_RoundTripsPolicy()
    {
        var policy = new FakePolicy();

        new ToolExecutionPolicySelected(policy).Policy.ShouldBeSameAs(policy);
    }

    [Fact]
    public void Constructor_WhenPolicyIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolExecutionPolicySelected(null!)).ParamName.ShouldBe("policy");

    private sealed class FakePolicy: IToolExecutionPolicy
    {
        public ToolExecutionPolicyReference Reference => ExecutionPolicy();

        public ValueTask<ToolExecutionPlanResult> PlanAsync(
            ImmutableArray<ValidatedToolCall> calls, ToolExecutionPolicyContext context, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
