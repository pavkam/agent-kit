// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;



/// <summary>Verifies AcceptedInput behavior and contracts.</summary>
public sealed class AcceptedInputTests
{
    [Fact]
    public void InputPromotionPlanAndResults_WhenRequiredEvidenceIsNull_ThrowArgumentNullExceptionWithParamName()
    {
        var acceptedException = ShouldThrowExactly<ArgumentNullException>(() => new AcceptedInput(null!));
        acceptedException.ParamName.ShouldBe("receipt");
    }

    private static TException ShouldThrowExactly<TException>(Func<object?> action)
        where TException : Exception
    {
        var exception = Should.Throw<Exception>(() => _ = action());
        exception.GetType().ShouldBe(typeof(TException));
        return (TException) exception;
    }
}
