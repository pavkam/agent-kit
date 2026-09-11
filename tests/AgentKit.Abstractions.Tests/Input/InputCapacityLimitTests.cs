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

    private static TException ShouldThrowExactly<TException>(Func<object?> action)
        where TException : Exception
    {
        var exception = Should.Throw<Exception>(() => _ = action());
        exception.GetType().ShouldBe(typeof(TException));
        return (TException) exception;
    }
}
