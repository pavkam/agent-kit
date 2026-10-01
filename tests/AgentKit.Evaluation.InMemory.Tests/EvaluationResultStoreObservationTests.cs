// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.InMemory.Tests;

public sealed class EvaluationResultStoreObservationTests
{
    [Fact]
    public void Name_WhenEnumerationsAreDefined_ReturnTheBoundedNames()
    {
        EvaluationResultStoreObservation.Name(EvaluationResultStoreOperationKind.Append).ShouldBe("append");
        EvaluationResultStoreObservation.Name(EvaluationResultStoreOperationKind.Read).ShouldBe("read");
        EvaluationResultStoreObservation.Name(EvaluationStoreFailureKind.IdentityConflict).ShouldBe("identity_conflict");
        EvaluationResultStoreObservation.Name(EvaluationStoreFailureKind.LimitExceeded).ShouldBe("limit_exceeded");
        EvaluationResultStoreObservation.Name(EvaluationStoreFailureKind.Unavailable).ShouldBe("unavailable");
    }

    [Fact]
    public void Name_WhenEnumerationsAreUndefined_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => EvaluationResultStoreObservation.Name((EvaluationResultStoreOperationKind) 9)).ParamName.ShouldBe("kind");
        Should.Throw<ArgumentOutOfRangeException>(() => EvaluationResultStoreObservation.Name((EvaluationStoreFailureKind) 9)).ParamName.ShouldBe("kind");
    }

    [Fact]
    public void Safe_WhenTheObservationThrows_SwallowsIt() =>
        Should.NotThrow(() => EvaluationResultStoreObservation.Safe(static () => throw new InvalidOperationException()));

    [Fact]
    public async Task ObserveAsync_WhenTheOperationFaults_RecordsAFaultedOutcomeAndRethrows()
    {
        var logger = new RecordingLogger<InMemoryEvaluationResultStore>();

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await EvaluationResultStoreObservation.ObserveAsync<int>(
            logger, TimeProvider.System, "in_memory", EvaluationResultStoreOperationKind.Read, EvaluationResultConformanceData.NewRun(),
            static () => throw new InvalidOperationException("secret"), static _ => null));

        var entry = logger.Snapshot().Single();
        entry.EventId.Id.ShouldBe(36102);
        entry.Message.ShouldNotContain("secret");
    }

    [Fact]
    public async Task ObserveAsync_WhenTheClockFails_StillReturnsTheResult()
    {
        var result = await EvaluationResultStoreObservation.ObserveAsync(
            new RecordingLogger<InMemoryEvaluationResultStore>(), new ThrowingClock(), "in_memory", EvaluationResultStoreOperationKind.Read,
            EvaluationResultConformanceData.NewRun(), static () => 42, static _ => null);

        result.ShouldBe(42);
    }

    private sealed class ThrowingClock: TimeProvider
    {
        public override long GetTimestamp() => throw new InvalidOperationException("clock");
    }
}
