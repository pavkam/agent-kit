// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class ToolEffectEvaluatorTests
{
    private static ValueTask<EvaluationOutcome> Run(AgentRunResult<ValidatedOutput> result, ToolEffectCriterion criterion) =>
        new ToolEffectEvaluator().EvaluateAsync(EvaluationTestData.ContextFor(result, criterion), TestContext.Current.CancellationToken);

    private static AgentRunFinished<ValidatedOutput> Calls(params (string Tool, string Args, bool Success)[] calls)
    {
        var parts = calls.Select((call, index) => EvaluationTestData.ToolCall(call.Tool, call.Args, index + 1)).ToArray();
        var results = parts.Select((part, index) => EvaluationTestData.ToolResult(part, calls[index].Success)).ToArray();
        return EvaluationTestData.FinishedWithTools("done", parts, results);
    }

    [Fact]
    public void Descriptor_WhenRead_DeclaresTheDocumentedKeyVersionAndCriterion()
    {
        var descriptor = new ToolEffectEvaluator().Descriptor;

        descriptor.Key.Value.ShouldBe("tool-effect");
        descriptor.Key.ShouldBe(ToolEffectEvaluator.Key);
        descriptor.Version.ShouldBe(new EvaluatorVersion(1));
        descriptor.SupportedCriteria.ShouldBe([ToolEffectCriterion.CriterionKey]);
    }

    [Fact]
    public async Task EvaluateAsync_WhenARequiredToolSucceeded_Passes()
    {
        var outcome = await Run(Calls(("read", "{}", true)), new ToolEffectCriterion([new ExpectedToolCall("read")], []));

        _ = outcome.ShouldBeOfType<EvaluationPassed>();
        outcome.Evidence.Single().ShouldBe(new EvaluationEvidence("required:read", "satisfied calls=1 succeeded=1"));
    }

    [Fact]
    public async Task EvaluateAsync_WhenARequiredToolWasNeverCalled_Fails()
    {
        var outcome = await Run(Calls(("write", "{}", true)), new ToolEffectCriterion([new ExpectedToolCall("read")], []));

        outcome.ShouldBeOfType<EvaluationFailed>().Evidence.Single().Value.ShouldBe("violated calls=0 succeeded=0");
    }

    [Fact]
    public async Task EvaluateAsync_WhenTheRequiredCallFailed_FailsOnlyWhenSuccessIsRequired()
    {
        var run = Calls(("read", "{}", false));

        _ = (await Run(run, new ToolEffectCriterion([new ExpectedToolCall("read", requireSuccess: true)], []))).ShouldBeOfType<EvaluationFailed>();
        _ = (await Run(run, new ToolEffectCriterion([new ExpectedToolCall("read", requireSuccess: false)], []))).ShouldBeOfType<EvaluationPassed>();
    }

    [Theory]
    [InlineData(0, 1, 1, false)]
    [InlineData(1, 1, 1, true)]
    [InlineData(2, 1, 1, false)]
    [InlineData(2, 1, 2, true)]
    [InlineData(1, 2, 3, false)]
    [InlineData(0, 0, 0, true)]
    public async Task EvaluateAsync_WhenCallCountsAreFixtured_HonorsTheMinimumAndMaximum(int made, int minimum, int maximum, bool satisfied)
    {
        var run = Calls([.. Enumerable.Range(0, made).Select(static _ => ("read", "{}", true))]);

        var outcome = await Run(run, new ToolEffectCriterion([new ExpectedToolCall("read", minimum, maximum)], []));

        (outcome is EvaluationPassed).ShouldBe(satisfied);
    }

    [Fact]
    public async Task EvaluateAsync_WhenAForbiddenToolWasCalled_FailsEvenIfEverythingElseHeld()
    {
        var run = Calls(("read", "{}", true), ("delete", "{}", true));

        var outcome = await Run(run, new ToolEffectCriterion([new ExpectedToolCall("read")], ["delete"]));

        var failed = outcome.ShouldBeOfType<EvaluationFailed>();
        failed.Evidence.ShouldContain(new EvaluationEvidence("forbidden:delete", "violated calls=1"));
        failed.Evidence.ShouldContain(new EvaluationEvidence("required:read", "satisfied calls=1 succeeded=1"));
    }

    [Fact]
    public async Task EvaluateAsync_WhenAForbiddenToolWasNotCalled_Passes()
    {
        var outcome = await Run(Calls(("read", "{}", true)), new ToolEffectCriterion([], ["delete"]));

        outcome.ShouldBeOfType<EvaluationPassed>().Evidence.Single().ShouldBe(new EvaluationEvidence("forbidden:delete", "clear calls=0"));
    }

    [Fact]
    public async Task EvaluateAsync_WhenACallIsMatchedByCanonicalIdentity_CountsItAsThatTool()
    {
        var call = EvaluationTestData.ToolCall("alias-used-by-model", "{}", 1, canonical: "files.read");
        var run = EvaluationTestData.FinishedWithTools("done", [call], [EvaluationTestData.ToolResult(call)]);

        var byCanonical = await Run(run, new ToolEffectCriterion([new ExpectedToolCall("files.read")], []));
        var byAlias = await Run(run, new ToolEffectCriterion([new ExpectedToolCall("alias-used-by-model")], []));
        var forbidden = await Run(run, new ToolEffectCriterion([], ["files.read"]));

        _ = byCanonical.ShouldBeOfType<EvaluationPassed>();
        _ = byAlias.ShouldBeOfType<EvaluationPassed>();
        _ = forbidden.ShouldBeOfType<EvaluationFailed>();
    }

    [Theory]
    [InlineData(/*lang=json,strict*/ """{"path":"a.txt","mode":"r"}""", /*lang=json,strict*/ """{"path":"a.txt"}""", true)]
    [InlineData(/*lang=json,strict*/ """{"path":"a.txt"}""", /*lang=json,strict*/ """{"path":"b.txt"}""", false)]
    [InlineData(/*lang=json,strict*/ """{"path":"a.txt"}""", /*lang=json,strict*/ """{"missing":1}""", false)]
    [InlineData(/*lang=json,strict*/ """{"opts":{"deep":true,"x":1}}""", /*lang=json,strict*/ """{"opts":{"deep":true}}""", true)]
    [InlineData(/*lang=json,strict*/ """{"list":[1,2]}""", /*lang=json,strict*/ """{"list":[1]}""", false)]
    [InlineData(/*lang=json,strict*/ """{"list":[1,2]}""", /*lang=json,strict*/ """{"list":[1,2]}""", true)]
    [InlineData("""[1]""", /*lang=json,strict*/ """{"a":1}""", false)]
    public async Task EvaluateAsync_WhenArgumentsAreFixtured_MatchesObjectSubsetsAndExactArraysAndScalars(string actual, string expected, bool matches)
    {
        using var subset = JsonDocument.Parse(expected);
        var run = Calls(("read", actual, true));

        var outcome = await Run(run, new ToolEffectCriterion([new ExpectedToolCall("read", argumentsSubset: subset.RootElement)], []));

        (outcome is EvaluationPassed).ShouldBe(matches);
    }

    [Fact]
    public async Task EvaluateAsync_WhenEvidenceIsRecorded_NeverContainsArguments()
    {
        var run = Calls(("read", /*lang=json,strict*/ """{"path":"ARG-CANARY"}""", true));

        var outcome = await Run(run, new ToolEffectCriterion([new ExpectedToolCall("read")], []));

        outcome.Evidence.ShouldAllBe(static e => !e.Value.Contains("CANARY", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EvaluateAsync_WhenCriterionIsMissingOrRunWasRejected_IsUnsupportedOrSkipped()
    {
        _ = (await new ToolEffectEvaluator().EvaluateAsync(EvaluationTestData.Context(), TestContext.Current.CancellationToken)).ShouldBeOfType<EvaluationUnsupported>();
        _ = (await Run(EvaluationTestData.Rejected(), new ToolEffectCriterion([], ["x"]))).ShouldBeOfType<EvaluationSkipped>();
    }

    [Fact]
    public async Task EvaluateAsync_WhenArgumentsAreInvalid_ThrowsBeforeAnyWork()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<ArgumentNullException>(async () => await new ToolEffectEvaluator().EvaluateAsync(null!, TestContext.Current.CancellationToken));
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await new ToolEffectEvaluator().EvaluateAsync(EvaluationTestData.Context(), cancellation.Token));
    }
}
