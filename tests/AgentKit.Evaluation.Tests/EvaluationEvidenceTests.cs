// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationEvidenceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Constructor_WhenNameIsBlank_ThrowsArgumentException(string? name) =>
        Should.Throw<ArgumentException>(() => new EvaluationEvidence(name!, "v")).ParamName.ShouldBe("name");

    [Fact]
    public void Constructor_WhenValueIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new EvaluationEvidence("n", null!)).ParamName.ShouldBe("value");

    [Fact]
    public void Constructor_WhenValueIsEmpty_RecordsTheEmptyFact() =>
        new EvaluationEvidence("n", string.Empty).Value.ShouldBeEmpty();
}
