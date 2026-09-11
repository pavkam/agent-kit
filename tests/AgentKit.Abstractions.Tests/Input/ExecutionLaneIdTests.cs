// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;



/// <summary>Verifies ExecutionLaneId behavior and contracts.</summary>
public sealed class ExecutionLaneIdTests: Conformance.GuidIdentityConformanceTests<ExecutionLaneId>
{
    [Fact]
    public void ExecutionLaneId_WhenValueIsEmpty_ThrowsArgumentOutOfRangeExceptionWithParamName()
    {
        var exception = ShouldThrowExactly<ArgumentOutOfRangeException>(() => new ExecutionLaneId(Guid.Empty));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void ExecutionLaneId_WhenValueIsPresent_RetainsTheValidatedValueAndCanonicalText()
    {
        var value = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var lane = new ExecutionLaneId(value);
        lane.Value.ShouldBe(value);
        lane.ToString().ShouldBe("10000000-0000-0000-0000-000000000001");
    }

    private static TException ShouldThrowExactly<TException>(Func<object?> action)
        where TException : Exception
    {
        var exception = Should.Throw<Exception>(() => _ = action());
        exception.GetType().ShouldBe(typeof(TException));
        return (TException) exception;
    }

    /// <inheritdoc/>
    protected override ExecutionLaneId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(ExecutionLaneId subject) => subject.Value;
}
