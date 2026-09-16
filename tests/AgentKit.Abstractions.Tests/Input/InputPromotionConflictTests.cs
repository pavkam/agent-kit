// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;

/// <summary>Verifies InputPromotionConflict behavior and contracts.</summary>
public sealed class InputPromotionConflictTests
{
    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new InputPromotionConflict((InputPromotionConflictKind) 99, "safe")).ParamName.ShouldBe("kind");

    [Fact]
    public void Constructor_WhenSafeReasonIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new InputPromotionConflict(InputPromotionConflictKind.Fenced, " ")).ParamName.ShouldBe("safeReason");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var conflict = new InputPromotionConflict(InputPromotionConflictKind.Fenced, "safe");
        conflict.Kind.ShouldBe(InputPromotionConflictKind.Fenced);
        conflict.SafeReason.ShouldBe("safe");
        InputPromotionResult result = conflict;
        _ = result.ShouldBeOfType<InputPromotionConflict>();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new InputPromotionConflict(InputPromotionConflictKind.Fenced, "safe");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
