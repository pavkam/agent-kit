// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class ToolEffectCriterionTests
{
    [Fact]
    public void Constructor_WhenArraysAreDefaultOrContainInvalidItems_ThrowsWithTheParameterName()
    {
        Should.Throw<ArgumentException>(() => new ToolEffectCriterion(default, [])).ParamName.ShouldBe("required");
        Should.Throw<ArgumentException>(() => new ToolEffectCriterion([], default)).ParamName.ShouldBe("forbidden");
        Should.Throw<ArgumentException>(() => new ToolEffectCriterion([null!], [])).ParamName.ShouldBe("required");
        Should.Throw<ArgumentException>(() => new ToolEffectCriterion([], [" "])).ParamName.ShouldBe("forbidden");
    }

    [Fact]
    public void Constructor_WhenNothingIsDeclared_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new ToolEffectCriterion([], [])).ParamName.ShouldBe("required");

    [Fact]
    public void Constructor_WhenAForbiddenToolRepeatsOrIsAlsoRequired_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => new ToolEffectCriterion([], ["rm", "rm"])).ParamName.ShouldBe("forbidden");
        Should.Throw<ArgumentException>(() => new ToolEffectCriterion([new ExpectedToolCall("read")], ["read"])).ParamName.ShouldBe("forbidden");
    }

    [Fact]
    public void Equals_WhenExpectationsMatchInOrder_IsEqualAndHashesAlike()
    {
        var left = new ToolEffectCriterion([new ExpectedToolCall("read")], ["rm"]);
        var right = new ToolEffectCriterion([new ExpectedToolCall("read")], ["rm"]);

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
        left.ShouldNotBe(new ToolEffectCriterion([new ExpectedToolCall("read")], ["format"]));
        left.Key.Value.ShouldBe("tool-effect");
    }
}
