// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationRunIdTests
{
    [Fact]
    public void Constructor_WhenValueIsEmpty_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationRunId(Guid.Empty));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void ToString_WhenValueIsSupplied_ReturnsHyphenatedText()
    {
        var value = Guid.Parse("e1000000-0000-0000-0000-000000000001");

        new EvaluationRunId(value).ToString().ShouldBe("e1000000-0000-0000-0000-000000000001");
        new EvaluationRunId(value).Value.ShouldBe(value);
    }

    [Fact]
    public void Equals_WhenInstanceIsDefault_IsNotEqualToAnyValidIdentity() =>
        default(EvaluationRunId).ShouldNotBe(EvaluationTestData.RunId);
}
