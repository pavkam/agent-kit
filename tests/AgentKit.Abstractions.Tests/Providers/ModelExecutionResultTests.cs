// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies the <see cref="ModelExecutionResult"/> family.</summary>
public sealed class ModelExecutionResultTests
{
    [Fact]
    public void Constructor_WhenCompletedAttemptIsMissing_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            new ModelExecutionCompleted(ProvidersTestData.Decision(), 1, null!));
        exception.ParamName.ShouldBe("result");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenAttemptsAreNotPositive_ThrowsArgumentOutOfRangeException(int attempts)
    {
        var failure = Failure();
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new ModelFallbackRequired(ProvidersTestData.Decision(), attempts, failure)).ParamName.ShouldBe("attempts");
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new ModelExecutionFailed(ProvidersTestData.Decision(), attempts, failure)).ParamName.ShouldBe("attempts");
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new ModelExecutionCancelled(ProvidersTestData.Decision(), attempts, failure)).ParamName.ShouldBe("attempts");
    }

    [Fact]
    public void Constructor_WhenFallbackFailureIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            new ModelFallbackRequired(ProvidersTestData.Decision(), 1, null!));
        exception.ParamName.ShouldBe("failure");
    }

    [Fact]
    public void Constructor_WhenOutcomesAreValid_PreservesTheClosedKind()
    {
        var selection = ProvidersTestData.Decision();
        var failure = Failure();
        var completed = new ModelAttemptCompleted(new ModelResponse(
            new ModelRequestId(Guid.Parse("77777777-7777-7777-7777-777777777777")),
            new ProviderResponseIdentity(
                new ProviderId("openai"),
                null,
                new ApiFamilyId("chat"),
                new ModelId("gpt"),
                new ModelId("gpt"),
                null,
                null,
                null),
            [new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty)],
            NormalizedStopReason.Completed,
            ModelUsage.NotReported,
            ExtensionData.Empty));

        ModelExecutionResult success = new ModelExecutionCompleted(selection, 1, completed);
        ModelExecutionResult fallback = new ModelFallbackRequired(selection, 2, failure);
        ModelExecutionResult failed = new ModelExecutionFailed(selection, 1, failure);
        ModelExecutionResult cancelled = new ModelExecutionCancelled(selection, 1, failure);

        success.ShouldBeOfType<ModelExecutionCompleted>().Result.ShouldBe(completed);
        fallback.ShouldBeOfType<ModelFallbackRequired>().Failure.ShouldBe(failure);
        failed.ShouldBeOfType<ModelExecutionFailed>().Attempts.ShouldBe(1);
        cancelled.ShouldBeOfType<ModelExecutionCancelled>().Cancellation.ShouldBe(failure);
    }

    private static ProviderFailure Failure() => new(
        ProviderFailureKind.Unavailable,
        new ProviderId("openai"),
        null,
        null,
        null,
        null,
        "unavailable",
        null,
        ExtensionData.Empty);
}
