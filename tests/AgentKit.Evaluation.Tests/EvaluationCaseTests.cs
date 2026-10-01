// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationCaseTests
{
    private static EvaluationCase Build(
        EvaluationCaseId? id = null,
        AgentId? agent = null,
        EvaluationCaseExecution? execution = null,
        AgentInput? input = null,
        AgentRunOptions? options = null,
        ImmutableArray<EvaluatorReference>? evaluators = null,
        EvaluationCriteria? criteria = null) =>
        new(
            id ?? new EvaluationCaseId("c"),
            agent ?? EvaluationTestData.Agent,
            execution ?? EvaluationTestData.Execution(),
            input ?? EvaluationTestData.Input(),
            options ?? new AgentRunOptions(),
            evaluators ?? [],
            criteria ?? EvaluationCriteria.Empty,
            null);

    [Fact]
    public void Constructor_WhenIdIsDefault_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Build(id: default(EvaluationCaseId))).ParamName.ShouldBe("id");

    [Fact]
    public void Constructor_WhenAgentIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Build(agent: default(AgentId))).ParamName.ShouldBe("agentId");

    [Fact]
    public void Constructor_WhenAReferenceIsNull_ThrowsArgumentNullExceptionNamingIt()
    {
        Should.Throw<ArgumentNullException>(() => new EvaluationCase(new EvaluationCaseId("c"), EvaluationTestData.Agent, null!, EvaluationTestData.Input(), new AgentRunOptions(), [], EvaluationCriteria.Empty, null))
            .ParamName.ShouldBe("execution");
        Should.Throw<ArgumentNullException>(() => new EvaluationCase(new EvaluationCaseId("c"), EvaluationTestData.Agent, EvaluationTestData.Execution(), null!, new AgentRunOptions(), [], EvaluationCriteria.Empty, null))
            .ParamName.ShouldBe("input");
        Should.Throw<ArgumentNullException>(() => new EvaluationCase(new EvaluationCaseId("c"), EvaluationTestData.Agent, EvaluationTestData.Execution(), EvaluationTestData.Input(), null!, [], EvaluationCriteria.Empty, null))
            .ParamName.ShouldBe("runOptions");
        Should.Throw<ArgumentNullException>(() => new EvaluationCase(new EvaluationCaseId("c"), EvaluationTestData.Agent, EvaluationTestData.Execution(), EvaluationTestData.Input(), new AgentRunOptions(), [], null!, null))
            .ParamName.ShouldBe("criteria");
    }

    [Fact]
    public void Constructor_WhenEvaluatorsAreDefaultOrContainNull_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => new EvaluationCase(new EvaluationCaseId("c"), EvaluationTestData.Agent, EvaluationTestData.Execution(), EvaluationTestData.Input(), new AgentRunOptions(), default, EvaluationCriteria.Empty, null))
            .ParamName.ShouldBe("evaluators");
        Should.Throw<ArgumentException>(() => Build(evaluators: [null!]))
            .ParamName.ShouldBe("evaluators");
    }

    [Fact]
    public void Constructor_WhenAnEvaluatorKeyRepeats_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Build(evaluators: [new EvaluatorReference(new EvaluatorKey("a")), new EvaluatorReference(new EvaluatorKey("a"), new EvaluatorVersion(2))]))
            .ParamName.ShouldBe("evaluators");

    [Fact]
    public void Constructor_WhenNoEvaluatorIsListed_RecordsTheRunOnly() =>
        Build(evaluators: []).Evaluators.ShouldBeEmpty();

    [Fact]
    public void Equals_WhenEveryMemberMatchesByValue_IsEqualAndHashesAlike()
    {
        var input = EvaluationTestData.Input();
        var left = EvaluationTestData.Case("same", evaluators: [new EvaluatorReference(new EvaluatorKey("a"))], fixture: new EvaluationFixtureReference("f", "1"), input: input);
        var right = EvaluationTestData.Case("same", evaluators: [new EvaluatorReference(new EvaluatorKey("a"))], fixture: new EvaluationFixtureReference("f", "1"), input: input);

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
        left.ShouldNotBe(EvaluationTestData.Case("same", evaluators: [new EvaluatorReference(new EvaluatorKey("b"))], fixture: new EvaluationFixtureReference("f", "1"), input: input));
    }
}
