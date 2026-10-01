// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class ExactStateEvaluatorTests
{
    private static ValueTask<EvaluationOutcome> Run(AgentRunResult<ValidatedOutput> result, ExactStateCriterion criterion) =>
        new ExactStateEvaluator().EvaluateAsync(EvaluationTestData.ContextFor(result, criterion), TestContext.Current.CancellationToken);

    public static TheoryData<string, string, ExactTextComparison, bool> TextFixtures => new()
    {
        { "Paris", "Paris", ExactTextComparison.Ordinal, true },
        { "paris", "Paris", ExactTextComparison.Ordinal, false },
        { "paris", "Paris", ExactTextComparison.OrdinalIgnoreCase, true },
        { "  Paris \n", "Paris", ExactTextComparison.TrimmedOrdinal, true },
        { "  paris \n", "Paris", ExactTextComparison.TrimmedOrdinal, false },
        { "Paris, France", "Paris", ExactTextComparison.Ordinal, false },
    };

    [Fact]
    public void Descriptor_WhenRead_DeclaresTheDocumentedKeyVersionAndCriterion()
    {
        var descriptor = new ExactStateEvaluator().Descriptor;

        descriptor.Key.Value.ShouldBe("exact-state");
        descriptor.Key.ShouldBe(ExactStateEvaluator.Key);
        descriptor.Version.ShouldBe(new EvaluatorVersion(1));
        descriptor.SupportedCriteria.ShouldBe([ExactStateCriterion.CriterionKey]);
    }

    [Theory]
    [MemberData(nameof(TextFixtures))]
    public async Task EvaluateAsync_WhenTextIsFixtured_AppliesTheDeclaredComparison(string produced, string expected, ExactTextComparison comparison, bool matches)
    {
        var outcome = await Run(EvaluationTestData.Finished(produced), new ExactStateCriterion(text: expected, textComparison: comparison));

        (outcome is EvaluationPassed).ShouldBe(matches);
        outcome.Evidence.Single().ShouldBe(new EvaluationEvidence("text", matches ? "matched" : "mismatch"));
    }

    [Fact]
    public async Task EvaluateAsync_WhenJsonIsStructurallyEqual_PassesRegardlessOfFormatting()
    {
        using var expected = JsonDocument.Parse("""{"b":[1,2],"a":"x"}""");

        var equal = await Run(EvaluationTestData.Finished(/*lang=json,strict*/ """{ "a" : "x", "b": [1, 2] }"""), new ExactStateCriterion(json: expected.RootElement));
        var different = await Run(EvaluationTestData.Finished(/*lang=json,strict*/ """{"a":"x","b":[2,1]}"""), new ExactStateCriterion(json: expected.RootElement));
        var notJson = await Run(EvaluationTestData.Finished("not json"), new ExactStateCriterion(json: expected.RootElement));

        _ = equal.ShouldBeOfType<EvaluationPassed>();
        _ = different.ShouldBeOfType<EvaluationFailed>();
        _ = notJson.ShouldBeOfType<EvaluationFailed>();
    }

    [Fact]
    public async Task EvaluateAsync_WhenOutcomeAndMessageCountAreDeclared_ChecksEachAndNamesTheMismatches()
    {
        var finished = EvaluationTestData.Finished("ok");

        var matching = await Run(finished, new ExactStateCriterion(ExpectedRunOutcome.Succeeded, newMessageCount: 1));
        var mismatching = await Run(finished, new ExactStateCriterion(ExpectedRunOutcome.PolicyHalted, newMessageCount: 3));

        _ = matching.ShouldBeOfType<EvaluationPassed>();
        var failed = mismatching.ShouldBeOfType<EvaluationFailed>();
        failed.Summary.ShouldBe("2 exact-state expectation(s) did not match.");
        failed.Evidence.Select(static e => e.Name).ShouldBe(["outcome", "new_message_count"]);
    }

    [Fact]
    public async Task EvaluateAsync_WhenTheRunEndedInAKnownNonSuccessOutcome_ComparesItTruthfully()
    {
        var halted = EvaluationTestData.Finished("refused", outcome: new RunFailed(new RunFailure(RunResultTestData.Error(AgentErrorCodes.Cancelled))));

        var outcome = await Run(halted, new ExactStateCriterion(ExpectedRunOutcome.Failed));

        _ = outcome.ShouldBeOfType<EvaluationPassed>();
    }

    [Fact]
    public async Task EvaluateAsync_WhenEvidenceIsRecorded_NeverContainsExpectedOrProducedContent()
    {
        var outcome = await Run(EvaluationTestData.Finished("PRODUCED-CANARY"), new ExactStateCriterion(text: "EXPECTED-CANARY"));

        outcome.Evidence.ShouldAllBe(static e => !e.Value.Contains("CANARY", StringComparison.Ordinal) && !e.Name.Contains("CANARY", StringComparison.Ordinal));
        outcome.Summary.ShouldNotContain("CANARY");
    }

    [Fact]
    public async Task EvaluateAsync_WhenCriterionIsMissingOrRunWasRejected_IsUnsupportedOrSkipped()
    {
        var evaluator = new ExactStateEvaluator();

        _ = (await evaluator.EvaluateAsync(EvaluationTestData.Context(), TestContext.Current.CancellationToken)).ShouldBeOfType<EvaluationUnsupported>();
        _ = (await Run(EvaluationTestData.Rejected(), new ExactStateCriterion(text: "x"))).ShouldBeOfType<EvaluationSkipped>();
    }

    [Fact]
    public async Task EvaluateAsync_WhenArgumentsAreInvalid_ThrowsBeforeAnyWork()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<ArgumentNullException>(async () => await new ExactStateEvaluator().EvaluateAsync(null!, TestContext.Current.CancellationToken));
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await new ExactStateEvaluator().EvaluateAsync(EvaluationTestData.Context(), cancellation.Token));
    }
}
