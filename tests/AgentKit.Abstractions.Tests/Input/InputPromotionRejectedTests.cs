// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;



/// <summary>Verifies InputPromotionRejected behavior and contracts.</summary>
public sealed class InputPromotionRejectedTests
{
    [Fact]
    public void InputPromotionPlanAndResults_WhenRequiredEvidenceIsNull_ThrowArgumentNullExceptionWithParamName()
    {
        var rejectedException = ShouldThrowExactly<ArgumentNullException>(() => new InputPromotionRejected(null!));
        rejectedException.ParamName.ShouldBe("rejection");
    }

    private static TException ShouldThrowExactly<TException>(Func<object?> action)
        where TException : Exception
    {
        var exception = Should.Throw<Exception>(() => _ = action());
        exception.GetType().ShouldBe(typeof(TException));
        return (TException) exception;
    }
}
