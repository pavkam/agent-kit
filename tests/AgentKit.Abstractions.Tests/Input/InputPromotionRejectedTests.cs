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

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsRejection()
    {
        var rejection = new InputRejection(InputRejectionKind.Unauthorized, "safe");
        var rejected = new InputPromotionRejected(rejection);
        rejected.Rejection.ShouldBeSameAs(rejection);
        InputPromotionResult result = rejected;
        _ = result.ShouldBeOfType<InputPromotionRejected>();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new InputPromotionRejected(new InputRejection(InputRejectionKind.Unauthorized, "safe"));
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
