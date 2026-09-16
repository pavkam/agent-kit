// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;



/// <summary>Verifies InputCapacityLimit behavior and contracts.</summary>
public sealed class InputCapacityLimitTests
{
    [Fact]
    public void InputCapacityAndRejectionEvidence_WhenArgumentsAreInvalid_ThrowsExactExpectedException()
    {
        var capacityException = ShouldThrowExactly<ArgumentOutOfRangeException>(() => new InputCapacityLimit(0, 0));
        capacityException.ParamName.ShouldBe("maximumPendingInputs");
    }

    [Fact]
    public void Constructor_WhenCurrentPendingInputsIsNegative_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new InputCapacityLimit(1, -1)).ParamName.ShouldBe("currentPendingInputs");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var limit = new InputCapacityLimit(10, 3);
        limit.MaximumPendingInputs.ShouldBe(10);
        limit.CurrentPendingInputs.ShouldBe(3);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new InputCapacityLimit(10, 3);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static TException ShouldThrowExactly<TException>(Func<object?> action)
        where TException : Exception
    {
        var exception = Should.Throw<Exception>(() => _ = action());
        exception.GetType().ShouldBe(typeof(TException));
        return (TException) exception;
    }
}
