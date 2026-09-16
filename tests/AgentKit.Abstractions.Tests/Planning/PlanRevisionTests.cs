// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Planning;



/// <summary>Verifies PlanRevision behavior and contracts.</summary>
public sealed class PlanRevisionTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var revision = new PlanRevision(3);
        revision.Value.ShouldBe(3);
        revision.ToString().ShouldBe("3");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenValueIsNotPositive_ThrowsExactParameter(long value)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new PlanRevision(value));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Equals_WhenSameValue_InstancesAreEqual()
    {
        var first = new PlanRevision(2);
        var second = new PlanRevision(2);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }
}
