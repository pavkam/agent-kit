// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;



/// <summary>Verifies InputPromotionPlanRejected behavior and contracts.</summary>
public sealed class InputPromotionPlanRejectedTests
{
    [Fact]
    public void InputPromotionPlanRejected_WhenSelectionLimitDoesNotExceedMaximum_ThrowsArgumentOutOfRangeExceptionWithParamName()
    {
        var exception = ShouldThrowExactly<ArgumentOutOfRangeException>(() => new InputPromotionPlanRejected(InputPromotionPlanRejectionKind.SelectionLimitExceeded, 1, 1, "selection limit"));
        exception.ParamName.ShouldBe("requiredCount");
    }

    private static TException ShouldThrowExactly<TException>(Func<object?> action)
        where TException : Exception
    {
        var exception = Should.Throw<Exception>(() => _ = action());
        exception.GetType().ShouldBe(typeof(TException));
        return (TException) exception;
    }
}
