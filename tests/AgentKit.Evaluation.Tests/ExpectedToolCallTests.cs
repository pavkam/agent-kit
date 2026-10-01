// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class ExpectedToolCallTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Constructor_WhenToolIsBlank_ThrowsArgumentException(string? tool) =>
        Should.Throw<ArgumentException>(() => new ExpectedToolCall(tool!)).ParamName.ShouldBe("tool");

    [Fact]
    public void Constructor_WhenMinimumIsNegative_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ExpectedToolCall("t", -1)).ParamName.ShouldBe("minimumCalls");

    [Fact]
    public void Constructor_WhenMaximumIsBelowMinimum_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ExpectedToolCall("t", 2, 1)).ParamName.ShouldBe("maximumCalls");

    [Fact]
    public void Constructor_WhenArgumentsSubsetIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ExpectedToolCall("t", argumentsSubset: default(JsonElement))).ParamName.ShouldBe("argumentsSubset");

    [Fact]
    public void Constructor_WhenDefaults_ExpectsAtLeastOneSuccessfulCall()
    {
        var expectation = new ExpectedToolCall("read");

        (expectation.MinimumCalls, expectation.MaximumCalls, expectation.RequireSuccess, expectation.ArgumentsSubset)
            .ShouldBe((1, null, true, null));
    }

    [Fact]
    public void Equals_WhenArgumentsSubsetsAreStructurallyEqual_IsEqualAndHashesAlike()
    {
        using var left = JsonDocument.Parse("""{"path":"a"}""");
        using var right = JsonDocument.Parse("""{"path":"a"}""");
        using var other = JsonDocument.Parse("""{"path":"b"}""");

        new ExpectedToolCall("read", argumentsSubset: left.RootElement).ShouldBe(new ExpectedToolCall("read", argumentsSubset: right.RootElement));
        new ExpectedToolCall("read", argumentsSubset: left.RootElement).GetHashCode().ShouldBe(new ExpectedToolCall("read", argumentsSubset: right.RootElement).GetHashCode());
        new ExpectedToolCall("read", argumentsSubset: left.RootElement).ShouldNotBe(new ExpectedToolCall("read", argumentsSubset: other.RootElement));
        new ExpectedToolCall("read").ShouldNotBe(new ExpectedToolCall("read", argumentsSubset: left.RootElement));
    }
}
