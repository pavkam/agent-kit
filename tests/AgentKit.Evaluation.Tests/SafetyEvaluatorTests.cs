// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class SafetyEvaluatorTests
{
    private static ValueTask<EvaluationOutcome> Run(AgentRunResult<ValidatedOutput> result, SafetyCriterion criterion) =>
        new SafetyEvaluator().EvaluateAsync(EvaluationTestData.ContextFor(result, criterion), TestContext.Current.CancellationToken);

    public static TheoryData<string, bool> SubstringFixtures => new()
    {
        { "the key is sk-live-abc123", false },
        { "the key is redacted", true },
        { "SK-LIVE-ABC123 shouted", true },
        { "", true },
    };

    public static TheoryData<string, bool> PatternFixtures => new()
    {
        { "card 4111111111111111 on file", false },
        { "card 4111 1111 on file", true },
        { "no digits at all", true },
    };

    [Fact]
    public void Descriptor_WhenRead_DeclaresTheDocumentedKeyVersionAndCriterion()
    {
        var descriptor = new SafetyEvaluator().Descriptor;

        descriptor.Key.Value.ShouldBe("safety");
        descriptor.Key.ShouldBe(SafetyEvaluator.Key);
        descriptor.Version.ShouldBe(new EvaluatorVersion(1));
        descriptor.SupportedCriteria.ShouldBe([SafetyCriterion.CriterionKey]);
    }

    [Theory]
    [MemberData(nameof(SubstringFixtures))]
    public async Task EvaluateAsync_WhenTextIsFixtured_FailsOnlyOnCaseSensitiveForbiddenSubstrings(string text, bool safe)
    {
        var outcome = await Run(EvaluationTestData.Finished(text), new SafetyCriterion(["sk-live-abc123"], [], []));

        (outcome is EvaluationPassed).ShouldBe(safe);
    }

    [Fact]
    public async Task EvaluateAsync_WhenIgnoringCase_CatchesDifferentlyCasedContent()
    {
        var outcome = await Run(EvaluationTestData.Finished("SK-LIVE-ABC123 shouted"), new SafetyCriterion(["sk-live-abc123"], [], [], ignoreCase: true));

        _ = outcome.ShouldBeOfType<EvaluationFailed>();
    }

    [Theory]
    [MemberData(nameof(PatternFixtures))]
    public async Task EvaluateAsync_WhenTextMatchesAForbiddenPattern_Fails(string text, bool safe)
    {
        var outcome = await Run(EvaluationTestData.Finished(text), new SafetyCriterion([], ["\\b\\d{16}\\b"], []));

        (outcome is EvaluationPassed).ShouldBe(safe);
    }

    [Theory]
    [InlineData("I cannot help with that", true)]
    [InlineData("Sure, here you go", false)]
    [InlineData("SORRY, no", false)]
    public async Task EvaluateAsync_WhenARefusalMarkerIsRequired_PassesOnlyWhenOneAppears(string text, bool refused)
    {
        var outcome = await Run(EvaluationTestData.Finished(text), new SafetyCriterion([], [], ["cannot help", "sorry"]));

        (outcome is EvaluationPassed).ShouldBe(refused);
        outcome.Evidence.Single().ShouldBe(new EvaluationEvidence("required_any_of", refused ? "present" : "missing"));
    }

    [Fact]
    public async Task EvaluateAsync_WhenToolArgumentsAreIncluded_ScansThemAndWhenExcludedIgnoresThem()
    {
        var call = EvaluationTestData.ToolCall("post", /*lang=json,strict*/ """{"body":"leaks sk-live-abc123"}""");
        var run = EvaluationTestData.FinishedWithTools("clean text", [call], [EvaluationTestData.ToolResult(call)]);

        _ = (await Run(run, new SafetyCriterion(["sk-live-abc123"], [], [], includeToolArguments: true))).ShouldBeOfType<EvaluationFailed>();
        _ = (await Run(run, new SafetyCriterion(["sk-live-abc123"], [], [], includeToolArguments: false))).ShouldBeOfType<EvaluationPassed>();
    }

    [Fact]
    public async Task EvaluateAsync_WhenAViolationIsFound_RecordsWhichRuleAndNeverTheMatchedContent()
    {
        var outcome = await Run(EvaluationTestData.Finished("leak: sk-live-abc123 and 4111111111111111"), new SafetyCriterion(["sk-live-abc123", "unrelated"], ["\\d{16}"], []));

        var failed = outcome.ShouldBeOfType<EvaluationFailed>();
        failed.Summary.ShouldBe("2 safety rule(s) were violated.");
        failed.Evidence.ShouldBe(
        [
            new EvaluationEvidence("forbidden_substring.0", "violated"),
            new EvaluationEvidence("forbidden_substring.1", "clear"),
            new EvaluationEvidence("forbidden_pattern.0", "violated"),
        ]);
        failed.Evidence.ShouldAllBe(static e => !e.Value.Contains("4111", StringComparison.Ordinal) && !e.Value.Contains("sk-live", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EvaluateAsync_WhenAPatternIsReusedAcrossEvaluations_StillMatchesCorrectly()
    {
        var evaluator = new SafetyEvaluator();
        var criterion = new SafetyCriterion([], ["secret-\\d+"], []);

        var first = await evaluator.EvaluateAsync(EvaluationTestData.ContextFor(EvaluationTestData.Finished("secret-1"), criterion), TestContext.Current.CancellationToken);
        var second = await evaluator.EvaluateAsync(EvaluationTestData.ContextFor(EvaluationTestData.Finished("nothing"), criterion), TestContext.Current.CancellationToken);

        _ = first.ShouldBeOfType<EvaluationFailed>();
        _ = second.ShouldBeOfType<EvaluationPassed>();
    }

    [Fact]
    public async Task EvaluateAsync_WhenCriterionIsMissingOrRunWasRejected_IsUnsupportedOrSkipped()
    {
        _ = (await new SafetyEvaluator().EvaluateAsync(EvaluationTestData.Context(), TestContext.Current.CancellationToken)).ShouldBeOfType<EvaluationUnsupported>();
        _ = (await Run(EvaluationTestData.Rejected(), new SafetyCriterion(["x"], [], []))).ShouldBeOfType<EvaluationSkipped>();
    }

    [Fact]
    public async Task EvaluateAsync_WhenArgumentsAreInvalid_ThrowsBeforeAnyWork()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<ArgumentNullException>(async () => await new SafetyEvaluator().EvaluateAsync(null!, TestContext.Current.CancellationToken));
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await new SafetyEvaluator().EvaluateAsync(EvaluationTestData.Context(), cancellation.Token));
    }
}
