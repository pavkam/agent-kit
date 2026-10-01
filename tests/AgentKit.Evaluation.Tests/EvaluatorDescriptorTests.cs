// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluatorDescriptorTests
{
    private static EvaluatorDescriptor Build(
        string key = "schema",
        long version = 1,
        string name = "Schema",
        ImmutableArray<EvaluationCriterionKey>? supported = null,
        bool requiresFixture = false) =>
        new(new EvaluatorKey(key), new EvaluatorVersion(version), name, supported ?? [], requiresFixture);

    private sealed record Marker(string Name): EvaluationCriterion(new EvaluationCriterionKey(Name));

    [Fact]
    public void Constructor_WhenKeyIsDefault_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new EvaluatorDescriptor(default, new EvaluatorVersion(1), "n", [], false)).ParamName.ShouldBe("key");

    [Fact]
    public void Constructor_WhenVersionIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluatorDescriptor(new EvaluatorKey("k"), default, "n", [], false)).ParamName.ShouldBe("version");

    [Fact]
    public void Constructor_WhenDisplayNameIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Build(name: " ")).ParamName.ShouldBe("displayName");

    [Fact]
    public void Constructor_WhenSupportedCriteriaAreDefaultBlankOrRepeated_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => new EvaluatorDescriptor(new EvaluatorKey("k"), new EvaluatorVersion(1), "n", default, false))
            .ParamName.ShouldBe("supportedCriteria");
        Should.Throw<ArgumentException>(() => Build(supported: [default]))
            .ParamName.ShouldBe("supportedCriteria");
        Should.Throw<ArgumentException>(() => Build(supported: [new EvaluationCriterionKey("a"), new EvaluationCriterionKey("a")]))
            .ParamName.ShouldBe("supportedCriteria");
    }

    [Fact]
    public void Supports_WhenCriteriaIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => Build().Supports(null!, null)).ParamName.ShouldBe("criteria");

    [Fact]
    public void Supports_WhenNoCriteriaAreDeclared_AcceptsAnyCase() =>
        Build().Supports(EvaluationCriteria.Empty, null).ShouldBeTrue();

    [Fact]
    public void Supports_WhenACriterionMatches_AcceptsTheCaseAndOtherwiseRejectsIt()
    {
        var descriptor = Build(supported: [new EvaluationCriterionKey("wanted")]);

        descriptor.Supports(new EvaluationCriteria([new Marker("wanted")]), null).ShouldBeTrue();
        descriptor.Supports(new EvaluationCriteria([new Marker("other")]), null).ShouldBeFalse();
        descriptor.Supports(EvaluationCriteria.Empty, null).ShouldBeFalse();
    }

    [Fact]
    public void Supports_WhenAFixtureIsRequired_RejectsACaseWithoutOne()
    {
        var descriptor = Build(requiresFixture: true);

        descriptor.Supports(EvaluationCriteria.Empty, null).ShouldBeFalse();
        descriptor.Supports(EvaluationCriteria.Empty, new EvaluationFixtureReference("f", "1")).ShouldBeTrue();
    }

    [Fact]
    public void Equals_WhenEveryMemberMatches_IsEqualAndHashesAlike()
    {
        var left = Build(supported: [new EvaluationCriterionKey("a")]);
        var right = Build(supported: [new EvaluationCriterionKey("a")]);

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
        left.ShouldNotBe(Build(supported: [new EvaluationCriterionKey("b")]));
    }
}
