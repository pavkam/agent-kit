// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationReadRejectedTests
{
    [Fact]
    public void Constructor_WhenFailureIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new EvaluationReadRejected(null!)).ParamName.ShouldBe("failure");

    [Fact]
    public void Constructor_WhenFailureIsSupplied_ExposesIt()
    {
        var failure = new EvaluationStoreFailure(EvaluationStoreFailureKind.Unavailable, "down");

        new EvaluationReadRejected(failure).Failure.ShouldBe(failure);
    }
}
