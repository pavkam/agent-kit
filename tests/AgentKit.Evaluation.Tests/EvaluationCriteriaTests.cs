// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationCriteriaTests
{
    private sealed record FirstCriterion(string Text): EvaluationCriterion(new EvaluationCriterionKey("first"));

    private sealed record SecondCriterion: EvaluationCriterion
    {
        public SecondCriterion(EvaluationCriterionKey key) : base(key)
        {
        }
    }

    [Fact]
    public void Constructor_WhenItemsAreDefault_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new EvaluationCriteria(default)).ParamName.ShouldBe("items");

    [Fact]
    public void Constructor_WhenItemsContainNull_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new EvaluationCriteria([null!])).ParamName.ShouldBe("items");

    [Fact]
    public void Constructor_WhenTwoCriteriaShareAKey_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new EvaluationCriteria([new FirstCriterion("a"), new SecondCriterion(new EvaluationCriterionKey("first"))]))
            .ParamName.ShouldBe("items");

    [Fact]
    public void Constructor_WhenCriterionKeyIsBlank_ThrowsFromTheBaseConstructor() =>
        Should.Throw<ArgumentException>(() => new SecondCriterion(default)).ParamName.ShouldBe("key");

    [Fact]
    public void Empty_WhenRead_HoldsNoCriteria()
    {
        EvaluationCriteria.Empty.Items.ShouldBeEmpty();
        EvaluationCriteria.Empty.Find<FirstCriterion>().ShouldBeNull();
    }

    [Fact]
    public void Find_WhenCriterionOfThatTypeExists_ReturnsItAndContainsReportsItsKey()
    {
        var criteria = new EvaluationCriteria([new FirstCriterion("a"), new SecondCriterion(new EvaluationCriterionKey("second"))]);

        criteria.Find<FirstCriterion>().ShouldBe(new FirstCriterion("a"));
        criteria.Contains(new EvaluationCriterionKey("second")).ShouldBeTrue();
        criteria.Contains(new EvaluationCriterionKey("missing")).ShouldBeFalse();
    }

    [Fact]
    public void Equals_WhenItemsAreEqualByValue_IsEqualAndHashesAlike()
    {
        var left = new EvaluationCriteria([new FirstCriterion("a")]);
        var right = new EvaluationCriteria([new FirstCriterion("a")]);

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
        left.ShouldNotBe(new EvaluationCriteria([new FirstCriterion("b")]));
    }
}
