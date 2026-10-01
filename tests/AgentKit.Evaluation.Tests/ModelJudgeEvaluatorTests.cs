// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class ModelJudgeEvaluatorTests
{
    private const string _rubric = "The answer names Paris as the capital of France.";

    private static ModelJudgeSettings Settings(int repeat = 3, int maxCalls = 100, long? maxTokens = null, double deviation = 0.25, int characters = 1_000) =>
        new(new ModelAlias("judge-model"), repeat, new ModelJudgeBudget(maxCalls, maxTokens), deviation, characters);

    private static EvaluationContext Context(string answer = "Paris is the capital of France.", double threshold = 0.7) =>
        EvaluationTestData.ContextFor(EvaluationTestData.Finished(answer), new RubricCriterion(_rubric, 5, threshold));

    private static ValueTask<EvaluationOutcome> Run(RecordedModelJudgeClient client, EvaluationContext? context = null, ModelJudgeSettings? settings = null) =>
        new ModelJudgeEvaluator(client, settings ?? Settings()).EvaluateAsync(context ?? Context(), TestContext.Current.CancellationToken);

    private static string Evidence(EvaluationOutcome outcome, string name) => outcome.Evidence.Single(e => e.Name == name).Value;

    [Fact]
    public void Constructor_WhenAnArgumentIsNull_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new ModelJudgeEvaluator(null!, Settings())).ParamName.ShouldBe("client");
        Should.Throw<ArgumentNullException>(() => new ModelJudgeEvaluator(new RecordedModelJudgeClient(), null!)).ParamName.ShouldBe("settings");
    }

    [Fact]
    public void Descriptor_WhenRead_DeclaresTheDocumentedKeyVersionAndCriterion()
    {
        var descriptor = new ModelJudgeEvaluator(new RecordedModelJudgeClient(), Settings()).Descriptor;

        descriptor.Key.Value.ShouldBe("model-judge");
        descriptor.Key.ShouldBe(ModelJudgeEvaluator.Key);
        descriptor.Version.ShouldBe(new EvaluatorVersion(1));
        descriptor.SupportedCriteria.ShouldBe([RubricCriterion.CriterionKey]);
    }

    [Fact]
    public async Task EvaluateAsync_WhenSamplesAreHighAndAgree_PassesWithMeanSampleCountAndDeviation()
    {
        var client = new RecordedModelJudgeClient(RecordedModelJudgeClient.Reply(4), RecordedModelJudgeClient.Reply(5), RecordedModelJudgeClient.Reply(4));

        var outcome = await Run(client);

        var passed = outcome.ShouldBeOfType<EvaluationPassed>();
        passed.Score!.Value.ShouldBe((0.8 + 1.0 + 0.8) / 3, 1e-9);
        passed.Score.SampleCount.ShouldBe(3);
        passed.Score.StandardDeviation.ShouldBe(0.11547, 1e-4);
        client.Requests.Select(static r => r.Sample).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task EvaluateAsync_WhenSamplesAreLowAndAgree_Fails()
    {
        var client = new RecordedModelJudgeClient(RecordedModelJudgeClient.Reply(1), RecordedModelJudgeClient.Reply(2), RecordedModelJudgeClient.Reply(1));

        var outcome = await Run(client);

        outcome.ShouldBeOfType<EvaluationFailed>().Score!.Value.ShouldBeLessThan(0.7);
    }

    [Fact]
    public async Task EvaluateAsync_WhenTheMeanSitsExactlyOnTheThreshold_Passes()
    {
        var client = new RecordedModelJudgeClient(RecordedModelJudgeClient.Reply(5), RecordedModelJudgeClient.Reply(5), RecordedModelJudgeClient.Reply(5));

        var outcome = await Run(client, Context(threshold: 1.0));

        _ = outcome.ShouldBeOfType<EvaluationPassed>();
    }

    [Fact]
    public async Task EvaluateAsync_WhenSamplesDisagreeBeyondTheBound_IsInconclusiveButStillReportsTheUncertainty()
    {
        var client = new RecordedModelJudgeClient(RecordedModelJudgeClient.Reply(0), RecordedModelJudgeClient.Reply(5), RecordedModelJudgeClient.Reply(0));

        var outcome = await Run(client);

        var inconclusive = outcome.ShouldBeOfType<EvaluationInconclusive>();
        inconclusive.Score!.StandardDeviation.ShouldBeGreaterThan(0.25);
        inconclusive.Score.SampleCount.ShouldBe(3);
    }

    [Fact]
    public async Task EvaluateAsync_WhenASingleSampleIsRequested_ReportsZeroDeviation()
    {
        var client = new RecordedModelJudgeClient(RecordedModelJudgeClient.Reply(5));

        var outcome = await Run(client, settings: Settings(repeat: 1));

        outcome.ShouldBeOfType<EvaluationPassed>().Score!.StandardDeviation.ShouldBe(0d);
    }

    [Fact]
    public async Task EvaluateAsync_WhenTheBudgetRunsOutMidJudgement_IsInconclusiveInsteadOfGuessing()
    {
        var client = new RecordedModelJudgeClient(RecordedModelJudgeClient.Reply(5), RecordedModelJudgeClient.Reply(5), RecordedModelJudgeClient.Reply(5));

        var outcome = await Run(client, settings: Settings(maxCalls: 2));

        var inconclusive = outcome.ShouldBeOfType<EvaluationInconclusive>();
        inconclusive.Summary.ShouldBe("Only 2 of 3 judge samples completed.");
        Evidence(outcome, "judge.budget.exhausted").ShouldBe("true");
        Evidence(outcome, "judge.budget.calls").ShouldBe("2");
        client.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task EvaluateAsync_WhenTheTokenBudgetIsSpent_StopsCallingTheJudge()
    {
        var client = new RecordedModelJudgeClient(
            RecordedModelJudgeClient.Reply(5, input: 90, output: 20), RecordedModelJudgeClient.Reply(5), RecordedModelJudgeClient.Reply(5));

        var outcome = await Run(client, settings: Settings(maxTokens: 100));

        _ = outcome.ShouldBeOfType<EvaluationInconclusive>();
        client.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task EvaluateAsync_WhenNoCallRemainsAtAll_IsInconclusiveWithoutCallingTheJudge()
    {
        var evaluator = new ModelJudgeEvaluator(new RecordedModelJudgeClient(RecordedModelJudgeClient.Reply(5)), Settings(repeat: 1, maxCalls: 1));
        _ = await evaluator.EvaluateAsync(Context(), TestContext.Current.CancellationToken);

        var second = await evaluator.EvaluateAsync(Context(), TestContext.Current.CancellationToken);

        second.ShouldBeOfType<EvaluationInconclusive>().Summary.ShouldBe("The judge budget was exhausted before any sample completed.");
        Evidence(second, "judge.budget.exhausted").ShouldBe("true");
    }

    [Fact]
    public async Task EvaluateAsync_WhenEveryRequestFails_ReportsAnEvaluatorFailureNotAgentQuality()
    {
        var failed = new ModelJudgeFailed(ModelJudgeFailureKind.Unavailable, "down");
        var client = new RecordedModelJudgeClient(failed, failed, failed);

        var outcome = await Run(client);

        outcome.ShouldBeOfType<EvaluatorFaulted>().ErrorType.ShouldBe("ModelJudgeFailure");
        Evidence(outcome, "judge.failed_requests").ShouldBe("3");
    }

    [Fact]
    public async Task EvaluateAsync_WhenRepliesAreUnparseable_IsInconclusiveAndCountsThem()
    {
        var garbage = new ModelJudgeCompleted("I think it is good.", "p", "m", 1, 1);
        var client = new RecordedModelJudgeClient(garbage, RecordedModelJudgeClient.Reply(9), garbage);

        var outcome = await Run(client);

        _ = outcome.ShouldBeOfType<EvaluationInconclusive>();
        Evidence(outcome, "judge.invalid_replies").ShouldBe("3");
        Evidence(outcome, "judge.samples").ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_WhenOnlySomeRepliesAreUnparseable_IsInconclusiveBecauseTheJudgementIsIncomplete()
    {
        var garbage = new ModelJudgeCompleted("nope", "p", "m", 1, 1);
        var client = new RecordedModelJudgeClient(RecordedModelJudgeClient.Reply(5), garbage, RecordedModelJudgeClient.Reply(5));

        var outcome = await Run(client);

        outcome.ShouldBeOfType<EvaluationInconclusive>().Summary.ShouldBe("Only 2 of 3 judge samples completed.");
        Evidence(outcome, "judge.samples").ShouldBe("5,5");
    }

    [Fact]
    public async Task EvaluateAsync_WhenJudging_RecordsTheRubricModelProviderPromptFingerprintAndSamples()
    {
        var client = new RecordedModelJudgeClient(RecordedModelJudgeClient.Reply(4), RecordedModelJudgeClient.Reply(4), RecordedModelJudgeClient.Reply(5));

        var outcome = await Run(client);

        Evidence(outcome, "judge.model").ShouldBe("judge-model");
        Evidence(outcome, "judge.rubric").ShouldBe(_rubric);
        Evidence(outcome, "judge.scale").ShouldBe("5");
        Evidence(outcome, "judge.threshold").ShouldBe("0.7");
        Evidence(outcome, "judge.repeat_count").ShouldBe("3");
        Evidence(outcome, "judge.provider").ShouldBe("recorded-provider");
        Evidence(outcome, "judge.resolved_model").ShouldBe("recorded-model-2026");
        Evidence(outcome, "judge.samples").ShouldBe("4,4,5");
        Evidence(outcome, "judge.prompt.sha256").ShouldBe(ModelJudgePrompt.Fingerprint(client.Requests[0].Instructions));
        client.Requests[0].Instructions.ShouldContain(_rubric);
        client.Requests[0].Model.ShouldBe(new ModelAlias("judge-model"));
    }

    [Fact]
    public async Task EvaluateAsync_WhenJudging_NeverRecordsTheCandidateOrTheJudgeReason()
    {
        var client = new RecordedModelJudgeClient(
            RecordedModelJudgeClient.Reply(5, "REASON-CANARY"), RecordedModelJudgeClient.Reply(5, "REASON-CANARY"), RecordedModelJudgeClient.Reply(5, "REASON-CANARY"));

        var outcome = await Run(client, Context("CANDIDATE-CANARY says Paris."));

        outcome.Summary.ShouldNotContain("CANARY");
        outcome.Evidence.ShouldAllBe(static e => !e.Value.Contains("CANARY", StringComparison.Ordinal));
        client.Requests[0].Candidate.ShouldContain("CANDIDATE-CANARY");
    }

    [Fact]
    public async Task EvaluateAsync_WhenTheCandidateExceedsTheBound_IsInconclusiveWithoutSpendingBudgetOrTruncating()
    {
        var client = new RecordedModelJudgeClient();

        var outcome = await Run(client, Context(new string('x', 2_000)));

        _ = outcome.ShouldBeOfType<EvaluationInconclusive>();
        client.Requests.ShouldBeEmpty();
        Evidence(outcome, "judge.rubric").ShouldBe(_rubric);
    }

    [Fact]
    public async Task EvaluateAsync_WhenThereIsNothingToJudge_IsUnsupportedOrSkipped()
    {
        var client = new RecordedModelJudgeClient();

        _ = (await Run(client, EvaluationTestData.Context())).ShouldBeOfType<EvaluationUnsupported>();
        _ = (await Run(client, EvaluationTestData.ContextFor(EvaluationTestData.Rejected(), new RubricCriterion("r")))).ShouldBeOfType<EvaluationSkipped>();
        _ = (await Run(client, EvaluationTestData.ContextFor(EvaluationTestData.Finished("  "), new RubricCriterion("r")))).ShouldBeOfType<EvaluationSkipped>();
        client.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_WhenCancelledBeforeOrDuringJudging_PropagatesCancellation()
    {
        var evaluator = new ModelJudgeEvaluator(new RecordedModelJudgeClient(RecordedModelJudgeClient.Reply(5)), Settings());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<ArgumentNullException>(async () => await evaluator.EvaluateAsync(null!, TestContext.Current.CancellationToken));
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await evaluator.EvaluateAsync(Context(), cancellation.Token));
    }

    [Fact]
    public async Task EvaluateAsync_WhenRunConcurrently_SharesOneBudgetAcrossEvaluations()
    {
        var replies = Enumerable.Range(0, 40).Select(static _ => (ModelJudgeResponse) RecordedModelJudgeClient.Reply(5)).ToArray();
        var client = new RecordedModelJudgeClient(replies);
        var evaluator = new ModelJudgeEvaluator(client, Settings(repeat: 1, maxCalls: 10));

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 30).Select(_ => evaluator.EvaluateAsync(Context(), TestContext.Current.CancellationToken).AsTask()));

        outcomes.Count(static o => o is EvaluationPassed).ShouldBe(10);
        outcomes.Count(static o => o is EvaluationInconclusive).ShouldBe(20);
        client.Requests.Count.ShouldBe(10);
    }
}
