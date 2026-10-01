// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationOutputReaderTests
{
    [Fact]
    public void Text_WhenValidatedOutputCarriesText_PrefersItOverAssistantText()
    {
        var finished = EvaluationTestData.Finished("assistant", output: new ValidatedOutput(OutputMode.Text, "validated", null, null));

        EvaluationOutputReader.Text(EvaluationTestData.Context(finished)).ShouldBe("validated");
    }

    [Fact]
    public void Text_WhenValidatedOutputCarriesNoText_FallsBackToAssistantText()
    {
        var finished = EvaluationTestData.Finished("assistant", output: new ValidatedOutput(OutputMode.Text, null, null, null));

        EvaluationOutputReader.Text(EvaluationTestData.Context(finished)).ShouldBe("assistant");
        EvaluationOutputReader.Text(EvaluationTestData.Context(EvaluationTestData.Rejected())).ShouldBeEmpty();
    }

    [Fact]
    public void TryReadJson_WhenStructuredOutputExists_PrefersItOverText()
    {
        using var document = JsonDocument.Parse("""{"from":"structured"}""");
        var finished = EvaluationTestData.Finished(/*lang=json,strict*/ """{"from":"text"}""", output: new ValidatedOutput(OutputMode.NativeSchema, null, document.RootElement, null));

        EvaluationOutputReader.TryReadJson(EvaluationTestData.Context(finished), out var json).ShouldBeTrue();
        json.GetProperty("from").GetString().ShouldBe("structured");
    }

    [Fact]
    public void TryReadJson_WhenOnlyTextExists_ParsesItAndOtherwiseReturnsFalse()
    {
        EvaluationOutputReader.TryReadJson(EvaluationTestData.Context(EvaluationTestData.Finished(/*lang=json,strict*/ """{"a":[1,2]}""")), out var json).ShouldBeTrue();
        json.GetProperty("a").GetArrayLength().ShouldBe(2);
        EvaluationOutputReader.TryReadJson(EvaluationTestData.Context(EvaluationTestData.Finished("not json")), out _).ShouldBeFalse();
        EvaluationOutputReader.TryReadJson(EvaluationTestData.Context(EvaluationTestData.Finished("  ")), out _).ShouldBeFalse();
    }

    [Fact]
    public void TryReadJson_WhenTextNestsBeyondTheDepthBound_ReturnsFalseInsteadOfThrowing()
    {
        var deep = string.Concat(Enumerable.Repeat("[", 100)) + string.Concat(Enumerable.Repeat("]", 100));

        EvaluationOutputReader.TryReadJson(EvaluationTestData.Context(EvaluationTestData.Finished(deep)), out _).ShouldBeFalse();
    }

    [Fact]
    public void Outcome_WhenEachKnownOutcomeIsGiven_MapsItToItsExpectation()
    {
        var error = RunResultTestData.Error(AgentErrorCodes.Cancelled);

        EvaluationOutputReader.Outcome(new RunSucceeded()).ShouldBe(ExpectedRunOutcome.Succeeded);
        EvaluationOutputReader.Outcome(new RunIdle()).ShouldBe(ExpectedRunOutcome.Idle);
        EvaluationOutputReader.Outcome(new RunDeferred([RunResultTestData.Deferred()])).ShouldBe(ExpectedRunOutcome.Deferred);
        EvaluationOutputReader.Outcome(new RunCancelled(new CancellationReason(error))).ShouldBe(ExpectedRunOutcome.Cancelled);
        EvaluationOutputReader.Outcome(new RunFailed(new RunFailure(error))).ShouldBe(ExpectedRunOutcome.Failed);
    }
}
