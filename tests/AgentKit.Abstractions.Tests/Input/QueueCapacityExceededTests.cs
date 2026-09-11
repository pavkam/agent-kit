// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;



/// <summary>Verifies QueueCapacityExceeded behavior and contracts.</summary>
public sealed class QueueCapacityExceededTests
{
    [Fact]
    public void InputCapacityAndRejectionEvidence_WhenArgumentsAreInvalid_ThrowsExactExpectedException()
    {
        var retryException = ShouldThrowExactly<ArgumentOutOfRangeException>(() => new QueueCapacityExceeded(new InputCapacityLimit(1, 0), TimeSpan.FromTicks(-1)));
        retryException.ParamName.ShouldBe("retryAfter");
    }

    private static TException ShouldThrowExactly<TException>(Func<object?> action)
        where TException : Exception
    {
        var exception = Should.Throw<Exception>(() => _ = action());
        exception.GetType().ShouldBe(typeof(TException));
        return (TException) exception;
    }
}
