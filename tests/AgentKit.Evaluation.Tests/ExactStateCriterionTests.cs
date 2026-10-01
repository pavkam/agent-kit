// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class ExactStateCriterionTests
{
    [Fact]
    public void Constructor_WhenNoExpectationIsDeclared_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new ExactStateCriterion()).ParamName.ShouldBe("outcome");

    [Fact]
    public void Constructor_WhenAnEnumerationIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new ExactStateCriterion((ExpectedRunOutcome) 99)).ParamName.ShouldBe("outcome");
        Should.Throw<ArgumentOutOfRangeException>(() => new ExactStateCriterion(text: "x", textComparison: (ExactTextComparison) 99)).ParamName.ShouldBe("textComparison");
    }

    [Fact]
    public void Constructor_WhenJsonIsUndefinedOrCountIsNegative_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new ExactStateCriterion(json: default(JsonElement))).ParamName.ShouldBe("json");
        Should.Throw<ArgumentOutOfRangeException>(() => new ExactStateCriterion(newMessageCount: -1)).ParamName.ShouldBe("newMessageCount");
    }

    [Fact]
    public void Constructor_WhenOnlyAZeroCountIsDeclared_IsValid() =>
        new ExactStateCriterion(newMessageCount: 0).NewMessageCount.ShouldBe(0);

    [Fact]
    public void Constructor_WhenJsonIsSupplied_CopiesItSoTheCallerDocumentCanBeDisposed()
    {
        ExactStateCriterion criterion;
        using (var document = JsonDocument.Parse("""{"a":1}"""))
        {
            criterion = new ExactStateCriterion(json: document.RootElement);
        }

        criterion.Json!.Value.GetProperty("a").GetInt32().ShouldBe(1);
    }

    [Fact]
    public void Equals_WhenJsonIsStructurallyEqual_IsEqualAndHashesAlike()
    {
        using var left = JsonDocument.Parse("""{"a":1}""");
        using var right = JsonDocument.Parse("""{"a":1}""");
        using var other = JsonDocument.Parse("""{"a":2}""");

        new ExactStateCriterion(json: left.RootElement).ShouldBe(new ExactStateCriterion(json: right.RootElement));
        new ExactStateCriterion(json: left.RootElement).GetHashCode().ShouldBe(new ExactStateCriterion(json: right.RootElement).GetHashCode());
        new ExactStateCriterion(json: left.RootElement).ShouldNotBe(new ExactStateCriterion(json: other.RootElement));
        new ExactStateCriterion(text: "a").ShouldNotBe(new ExactStateCriterion(json: left.RootElement));
    }
}
