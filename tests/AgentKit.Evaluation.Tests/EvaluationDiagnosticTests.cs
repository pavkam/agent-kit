// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationDiagnosticTests
{
    [Theory]
    [InlineData(null, "m", "code")]
    [InlineData(" ", "m", "code")]
    [InlineData("c", "", "safeMessage")]
    [InlineData("c", null, "safeMessage")]
    public void Constructor_WhenTextIsBlank_ThrowsWithTheParameterName(string? code, string? message, string parameter) =>
        Should.Throw<ArgumentException>(() => new EvaluationDiagnostic(code!, message!)).ParamName.ShouldBe(parameter);

    [Fact]
    public void Constructor_WhenTextIsSupplied_PreservesIt()
    {
        var diagnostic = new EvaluationDiagnostic("code", "message");

        diagnostic.Code.ShouldBe("code");
        diagnostic.SafeMessage.ShouldBe("message");
    }
}
