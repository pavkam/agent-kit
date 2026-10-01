// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class SchemaEvaluatorTests
{
    private static IOutputSchemaEngine Engine()
    {
        var provider = new ServiceCollection().AddAgentOutput().BuildServiceProvider();
        return provider.GetRequiredService<IOutputSchemaEngine>();
    }

    private static EvaluationContext Context(string output, JsonSchemaDocument? schema = null, OutputSchemaProcessingLimits? limits = null) =>
        EvaluationTestData.ContextFor(EvaluationTestData.Finished(output), new SchemaCriterion(schema ?? SchemaCriterionTests.Schema(), limits));

    public static TheoryData<string, bool> OutputFixtures => new()
    {
        { /*lang=json,strict*/ """{"answer":"42"}""", true },
        { /*lang=json,strict*/ """{"answer":"42","extra":1}""", true },
        { /*lang=json,strict*/ """{"answer":42}""", false },
        { """{}""", false },
        { """[]""", false },
    };

    [Fact]
    public void Constructor_WhenEngineIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new SchemaEvaluator(null!)).ParamName.ShouldBe("engine");

    [Fact]
    public void Descriptor_WhenRead_DeclaresTheDocumentedKeyVersionAndCriterion()
    {
        var descriptor = new SchemaEvaluator(Engine()).Descriptor;

        descriptor.Key.ShouldBe(SchemaEvaluator.Key);
        descriptor.Key.Value.ShouldBe("schema");
        descriptor.Version.ShouldBe(new EvaluatorVersion(1));
        descriptor.SupportedCriteria.ShouldBe([SchemaCriterion.CriterionKey]);
        descriptor.RequiresFixture.ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(OutputFixtures))]
    public async Task EvaluateAsync_WhenOutputIsFixtured_PassesOnlyWhenItSatisfiesTheSchema(string output, bool valid)
    {
        var outcome = await new SchemaEvaluator(Engine()).EvaluateAsync(Context(output), TestContext.Current.CancellationToken);

        if (valid)
        {
            _ = outcome.ShouldBeOfType<EvaluationPassed>();
        }
        else
        {
            _ = outcome.ShouldBeOfType<EvaluationFailed>();
        }

        outcome.Score.ShouldBe(EvaluationScore.Certain(valid));
        outcome.Evidence.ShouldContain(static e => e.Name == "schema.name" && e.Value == "answer-schema");
        outcome.Evidence.ShouldContain(static e => e.Name == "engine.profile");
    }

    [Fact]
    public async Task EvaluateAsync_WhenOutputViolatesTheSchema_RecordsIssueCodesAndPathsNeverTheValue()
    {
        var failing = await new SchemaEvaluator(Engine()).EvaluateAsync(Context(/*lang=json,strict*/ """{"answer":12345}"""), TestContext.Current.CancellationToken);

        failing.Evidence.ShouldContain(static e => e.Name.StartsWith("issue.", StringComparison.Ordinal));
        failing.Evidence.ShouldAllBe(static e => !e.Value.Contains("12345", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EvaluateAsync_WhenOutputIsNotJson_FailsWithoutCallingTheEngineEvaluation()
    {
        var outcome = await new SchemaEvaluator(Engine()).EvaluateAsync(Context("plain words"), TestContext.Current.CancellationToken);

        outcome.ShouldBeOfType<EvaluationFailed>().Summary.ShouldBe("The output is not a JSON value.");
    }

    [Fact]
    public async Task EvaluateAsync_WhenTheSchemaExceedsTheLimits_ReportsUnsupportedRatherThanAModelFailure()
    {
        var outcome = await new SchemaEvaluator(Engine()).EvaluateAsync(
            Context(/*lang=json,strict*/ """{"answer":"x"}""", limits: new OutputSchemaProcessingLimits(8, 1, 1)),
            TestContext.Current.CancellationToken);

        _ = outcome.ShouldBeOfType<EvaluationUnsupported>();
    }

    [Fact]
    public async Task EvaluateAsync_WhenTheCaseDeclaresNoSchemaCriterion_IsUnsupported()
    {
        var outcome = await new SchemaEvaluator(Engine()).EvaluateAsync(EvaluationTestData.Context(), TestContext.Current.CancellationToken);

        _ = outcome.ShouldBeOfType<EvaluationUnsupported>();
    }

    [Fact]
    public async Task EvaluateAsync_WhenTheRunProducedNoResult_IsSkipped()
    {
        var context = EvaluationTestData.ContextFor(EvaluationTestData.Rejected(), new SchemaCriterion(SchemaCriterionTests.Schema()));

        var outcome = await new SchemaEvaluator(Engine()).EvaluateAsync(context, TestContext.Current.CancellationToken);

        _ = outcome.ShouldBeOfType<EvaluationSkipped>();
    }

    [Fact]
    public async Task EvaluateAsync_WhenArgumentsAreInvalid_ThrowsBeforeAnyWork()
    {
        var evaluator = new SchemaEvaluator(Engine());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<ArgumentNullException>(async () => await evaluator.EvaluateAsync(null!, TestContext.Current.CancellationToken));
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await evaluator.EvaluateAsync(Context("{}"), cancellation.Token));
    }
}
