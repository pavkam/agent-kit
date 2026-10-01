// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationStoreFailureTests
{
    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationStoreFailure((EvaluationStoreFailureKind) 77, "m")).ParamName.ShouldBe("kind");

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Constructor_WhenMessageIsBlank_ThrowsArgumentException(string? message) =>
        Should.Throw<ArgumentException>(() => new EvaluationStoreFailure(EvaluationStoreFailureKind.Unavailable, message!)).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_PreservesThem()
    {
        var failure = new EvaluationStoreFailure(EvaluationStoreFailureKind.LimitExceeded, "too big");

        (failure.Kind, failure.SafeMessage).ShouldBe((EvaluationStoreFailureKind.LimitExceeded, "too big"));
    }
}
