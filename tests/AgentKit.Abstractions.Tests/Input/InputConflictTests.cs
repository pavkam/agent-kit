// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;



/// <summary>Verifies InputConflict behavior and contracts.</summary>
public sealed class InputConflictTests
{
    [Fact]
    public void InputCapacityAndRejectionEvidence_WhenArgumentsAreInvalid_ThrowsExactExpectedException()
    {
        var reasonException = ShouldThrowExactly<ArgumentException>(() => new InputConflict(Input(1), " "));
        reasonException.ParamName.ShouldBe("safeReason");
    }

    private static InputId Input(long value) => new(Guid.Parse($"10000000-0000-0000-0000-{value:000000000000}"));

    private static TException ShouldThrowExactly<TException>(Func<object?> action)
        where TException : Exception
    {
        var exception = Should.Throw<Exception>(() => _ = action());
        exception.GetType().ShouldBe(typeof(TException));
        return (TException) exception;
    }
}
