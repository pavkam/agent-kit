// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Live.Tests;

/// <summary>
/// Opt-in verification of <see cref="ModelJudgeEvaluator"/> against a real OpenAI model. The test is skipped, with the
/// reason reported, unless <c>AGENTKIT_LIVE_TESTS=1</c> and <c>OPENAI_API_KEY</c> are both set, so offline runs and CI
/// never spend money or need the public internet.
/// </summary>
public sealed class LiveModelJudgeTests
{
    private const string _optInVariable = "AGENTKIT_LIVE_TESTS";
    private const string _credentialVariable = "OPENAI_API_KEY";
    private const string _modelVariable = "AGENTKIT_LIVE_OPENAI_MODEL";
    private static readonly TimeSpan _timeBound = TimeSpan.FromMinutes(2);

    [Fact]
    public async Task RunAsync_WhenAnOpenAIModelJudgesAnObviouslyCorrectAnswer_RecordsAPassingRubricOutcome()
    {
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable(_optInVariable) == "1",
            $"Live tests are opt-in; set {_optInVariable}=1 to run them.");
        var apiKey = Environment.GetEnvironmentVariable(_credentialVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(apiKey), $"Set {_credentialVariable} to run the live model-judge test.");
        var model = Environment.GetEnvironmentVariable(_modelVariable) is { Length: > 0 } configured ? configured : "gpt-4o-mini";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(_timeBound);
        var storeKey = new EvaluationResultStoreKey("live");
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI(apiKey!, model)
            .WithInstructions("Answer in one short sentence.");
        _ = builder.Services
            .AddAgentEvaluation()
            .AddModelRequestJudgeClient()
            .AddModelJudgeEvaluator(options =>
            {
                // The judge is the same explicit model alias the sugar registers; calls and tokens are capped so a
                // misbehaving run cannot spend beyond a few cents.
                options.JudgeModel = new ModelAlias("assistant");
                options.RepeatCount = 2;
                options.MaximumCalls = 4;
                options.MaximumTokens = 8_000;
                options.MaximumStandardDeviation = 2d;
            })
            .AddInMemoryEvaluationResultStore(storeKey);
        await using var engine = builder.Build();
        var definition = (await engine.GetAgentsAsync(timeout.Token)).Definitions.Single();
        var inputs = engine.Services.GetRequiredService<IIdentifierGenerator<InputId>>();
        var criteria = new EvaluationCriteria([new RubricCriterion("The answer states that Paris is the capital of France.")]);
        var evaluationCase = new EvaluationCase(
            new EvaluationCaseId("capital-of-france"),
            definition.Id,
            new EvaluationCaseExecution(engine.Identity, definition.SessionProfile),
            new AgentInput(
                inputs.Create(),
                InputDelivery.Steer,
                [new TextPart("What is the capital of France?", TextSemantics.Plain, ExtensionData.Empty)],
                ExtensionData.Empty),
            new AgentRunOptions(),
            [new EvaluatorReference(ModelJudgeEvaluator.Key)],
            criteria,
            fixture: null);
        var plan = new EvaluationPlan(
            new EvaluationPlanId("live-model-judge"),
            new EvaluationPlanVersion(1),
            [evaluationCase],
            new EvaluationExecutionPolicy(1, 1, _timeBound, null, null, false, false),
            new EvaluationRecordingPolicy(storeKey, []));

        var report = await engine.Services.GetRequiredService<IEvaluationRunner>().RunAsync(plan, timeout.Token);

        report.Status.ShouldBe(EvaluationReportStatus.Completed);
        report.Summary.Passed.ShouldBe(1, customMessage: "The judge model should accept an obviously correct answer.");
    }
}
