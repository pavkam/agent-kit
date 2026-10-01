// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationFixtureReferenceTests
{
    [Theory]
    [InlineData(null, "1", "key")]
    [InlineData(" ", "1", "key")]
    [InlineData("fixture", null, "version")]
    [InlineData("fixture", "", "version")]
    public void Constructor_WhenTextIsBlank_ThrowsWithTheParameterName(string? key, string? version, string parameter)
    {
        var exception = Should.Throw<ArgumentException>(() => new EvaluationFixtureReference(key!, version!));

        exception.ParamName.ShouldBe(parameter);
    }

    [Fact]
    public void Constructor_WhenTextIsSupplied_PreservesBothValuesAndComparesByValue()
    {
        var reference = new EvaluationFixtureReference("workspace", "2026-10");

        reference.Key.ShouldBe("workspace");
        reference.Version.ShouldBe("2026-10");
        reference.ShouldBe(new EvaluationFixtureReference("workspace", "2026-10"));
        reference.ShouldNotBe(new EvaluationFixtureReference("workspace", "2026-11"));
    }
}
