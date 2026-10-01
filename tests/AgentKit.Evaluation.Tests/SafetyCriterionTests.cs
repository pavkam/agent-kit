// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class SafetyCriterionTests
{
    [Fact]
    public void Constructor_WhenArraysAreDefault_ThrowsWithTheParameterName()
    {
        Should.Throw<ArgumentException>(() => new SafetyCriterion(default, [], [])).ParamName.ShouldBe("forbiddenSubstrings");
        Should.Throw<ArgumentException>(() => new SafetyCriterion([], default, [])).ParamName.ShouldBe("forbiddenPatterns");
        Should.Throw<ArgumentException>(() => new SafetyCriterion([], [], default)).ParamName.ShouldBe("requiredAnyOf");
    }

    [Fact]
    public void Constructor_WhenNoRuleIsDeclared_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new SafetyCriterion([], [], [])).ParamName.ShouldBe("forbiddenSubstrings");

    [Fact]
    public void Constructor_WhenARuleValueIsEmpty_ThrowsWithTheParameterName()
    {
        Should.Throw<ArgumentException>(() => new SafetyCriterion([""], [], [])).ParamName.ShouldBe("forbiddenSubstrings");
        Should.Throw<ArgumentException>(() => new SafetyCriterion([], [""], [])).ParamName.ShouldBe("forbiddenPatterns");
        Should.Throw<ArgumentException>(() => new SafetyCriterion([], [], [""])).ParamName.ShouldBe("requiredAnyOf");
    }

    [Fact]
    public void Constructor_WhenAPatternIsNotAValidExpression_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new SafetyCriterion([], ["(unclosed"], [])).ParamName.ShouldBe("forbiddenPatterns");

    [Fact]
    public void Constructor_WhenAPatternNeedsBacktracking_ThrowsArgumentExceptionInsteadOfRunningIt() =>
        Should.Throw<ArgumentException>(() => new SafetyCriterion([], ["(?<=a)b"], [])).ParamName.ShouldBe("forbiddenPatterns");

    [Fact]
    public void Equals_WhenEveryRuleMatches_IsEqualAndHashesAlike()
    {
        var left = new SafetyCriterion(["secret"], ["a+b"], ["sorry"], ignoreCase: true);
        var right = new SafetyCriterion(["secret"], ["a+b"], ["sorry"], ignoreCase: true);

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
        left.ShouldNotBe(new SafetyCriterion(["secret"], ["a+b"], ["sorry"], ignoreCase: false));
        left.Key.Value.ShouldBe("safety");
    }
}
