// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationExportRejectedTests
{
    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationExportRejected((EvaluationExportFailureKind) 9, "m")).ParamName.ShouldBe("kind");

    [Fact]
    public void Constructor_WhenMessageIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new EvaluationExportRejected(EvaluationExportFailureKind.Faulted, " ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_PreservesThem()
    {
        var rejection = new EvaluationExportRejected(EvaluationExportFailureKind.Cancelled, "stopped");

        (rejection.Kind, rejection.SafeMessage).ShouldBe((EvaluationExportFailureKind.Cancelled, "stopped"));
    }
}
