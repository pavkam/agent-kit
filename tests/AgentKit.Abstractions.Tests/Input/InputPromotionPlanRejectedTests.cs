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

    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new InputPromotionPlanRejected((InputPromotionPlanRejectionKind) 99, 1, 1, "safe")).ParamName.ShouldBe("kind");

    [Fact]
    public void Constructor_WhenRequiredCountIsNegative_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new InputPromotionPlanRejected(InputPromotionPlanRejectionKind.NothingEligible, -1, 1, "safe")).ParamName.ShouldBe("requiredCount");

    [Fact]
    public void Constructor_WhenMaximumPromotionsIsNotPositive_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new InputPromotionPlanRejected(InputPromotionPlanRejectionKind.NothingEligible, 0, 0, "safe")).ParamName.ShouldBe("maximumPromotions");

    [Fact]
    public void Constructor_WhenSafeReasonIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new InputPromotionPlanRejected(InputPromotionPlanRejectionKind.NothingEligible, 0, 1, " ")).ParamName.ShouldBe("safeReason");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var rejected = new InputPromotionPlanRejected(InputPromotionPlanRejectionKind.NothingEligible, 0, 1, "safe");
        rejected.Kind.ShouldBe(InputPromotionPlanRejectionKind.NothingEligible);
        rejected.RequiredCount.ShouldBe(0);
        rejected.MaximumPromotions.ShouldBe(1);
        rejected.SafeReason.ShouldBe("safe");
        InputPromotionPlanningResult result = rejected;
        _ = result.ShouldBeOfType<InputPromotionPlanRejected>();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new InputPromotionPlanRejected(InputPromotionPlanRejectionKind.NothingEligible, 0, 1, "safe");
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
