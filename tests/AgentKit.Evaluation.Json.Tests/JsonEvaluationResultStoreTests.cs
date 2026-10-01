// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Json.Tests;

public sealed class JsonEvaluationResultStoreTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void Constructor_WhenAnArgumentIsNull_ThrowsArgumentNullExceptionNamingIt()
    {
        using var root = new JsonEvaluationTestRoot();

        Should.Throw<ArgumentNullException>(() => new JsonEvaluationResultStore(null!, JsonEvaluationStoreSettings.CreateDefault(), TimeProvider.System)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentNullException>(() => new JsonEvaluationResultStore(root.Target(), null!, TimeProvider.System)).ParamName.ShouldBe("settings");
        Should.Throw<ArgumentNullException>(() => new JsonEvaluationResultStore(root.Target(), JsonEvaluationStoreSettings.CreateDefault(), null!)).ParamName.ShouldBe("time");
    }

    [Fact]
    public async Task InitializeAsync_WhenCalled_CreatesTheManifestAndIsRepeatable()
    {
        using var root = new JsonEvaluationTestRoot();
        using var store = root.Open();

        await store.InitializeAsync(Token);
        await store.InitializeAsync(Token);

        File.Exists(root.ManifestPath).ShouldBeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WhenTokenIsCancelled_ThrowsAndWhenDisposedThrowsObjectDisposed()
    {
        using var root = new JsonEvaluationTestRoot();
        var store = root.Open();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await store.InitializeAsync(cancellation.Token));
        store.Dispose();
        _ = await Should.ThrowAsync<ObjectDisposedException>(async () => await store.InitializeAsync(Token));
        _ = await Should.ThrowAsync<ObjectDisposedException>(async () => await store.AppendAsync(EvaluationResultConformanceData.Result(EvaluationResultConformanceData.NewRun()), Token));
        _ = await Should.ThrowAsync<ObjectDisposedException>(async () => await store.ReadAsync(new EvaluationResultQuery(EvaluationResultConformanceData.NewRun(), 1), Token));
    }

    [Fact]
    public async Task AppendAsync_WhenAcknowledged_WritesOneInspectableJsonLinePerResultWithoutContentFields()
    {
        using var root = new JsonEvaluationTestRoot();
        using var store = root.Open();
        var run = EvaluationResultConformanceData.NewRun();

        _ = await store.AppendAsync(EvaluationResultConformanceData.Result(run, 0), Token);
        _ = await store.AppendAsync(EvaluationResultConformanceData.Result(run, 1), Token);
        _ = await store.AppendAsync(EvaluationResultConformanceData.Result(run, 1), Token);

        var lines = File.ReadAllLines(root.LogPath);
        lines.Length.ShouldBe(2);
        using var document = JsonDocument.Parse(lines[0]);
        document.RootElement.GetProperty("caseId").GetString().ShouldBe("case-0");
        document.RootElement.GetProperty("disposition").GetString().ShouldBe("Evaluated");
    }

    [Fact]
    public async Task InitializeAsync_WhenAnotherInstanceHoldsTheRoot_RejectsASecondWriter()
    {
        using var root = new JsonEvaluationTestRoot();
        using var first = root.Open();
        await first.InitializeAsync(Token);
        using var second = root.Open();

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await second.InitializeAsync(Token));

        first.Dispose();
        await second.InitializeAsync(Token);
    }

    [Fact]
    public async Task InitializeAsync_WhenTheManifestBelongsToAnotherInstance_RefusesToOpen()
    {
        using var root = new JsonEvaluationTestRoot();
        using (var store = root.Open())
        {
            await store.InitializeAsync(Token);
        }

        using var wrong = root.Open(root.Target(new JsonEvaluationStoreInstanceId(Guid.NewGuid())));

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await wrong.InitializeAsync(Token));

        exception.Message.ShouldContain("identity");
    }

    [Fact]
    public async Task InitializeAsync_WhenTheEncodingContractDiffers_RefusesToDecodeUnderAnotherContract()
    {
        using var root = new JsonEvaluationTestRoot();
        using (var store = root.Open())
        {
            await store.InitializeAsync(Token);
        }

        var other = JsonStoreSerialization.CreateCanonicalOptions();
        other.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        using var different = root.Open(settings: new JsonEvaluationStoreSettings(1_048_576, 1_048_576, new JsonEncodingSettings(other)));

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await different.InitializeAsync(Token));
    }

    [Fact]
    public async Task InitializeAsync_WhenTheRootIsMissingAndCreationIsNotAllowed_Refuses()
    {
        using var root = new JsonEvaluationTestRoot();
        Directory.Delete(root.Path);
        using var store = root.Open(root.Target(mode: JsonStoreOpenMode.OpenExisting, recovery: JsonStoreRecoveryMode.ValidateExact));

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await store.InitializeAsync(Token));
    }

    [Fact]
    public async Task InitializeAsync_WhenTheLogEndsWithATornAppend_RecoversOrRefusesAccordingToTheTarget()
    {
        using var root = new JsonEvaluationTestRoot();
        var run = EvaluationResultConformanceData.NewRun();
        using (var store = root.Open())
        {
            _ = await store.AppendAsync(EvaluationResultConformanceData.Result(run), Token);
        }

        await File.AppendAllTextAsync(root.LogPath, "{\"evaluationRunId\":\"torn", Token);

        using (var strict = root.Open(root.Target(recovery: JsonStoreRecoveryMode.ValidateExact, mode: JsonStoreOpenMode.OpenExisting)))
        {
            _ = await Should.ThrowAsync<InvalidOperationException>(async () => await strict.InitializeAsync(Token));
        }

        using var recovering = root.Open();
        (await recovering.ReadAsync(new EvaluationResultQuery(run, 5), Token)).ShouldBeOfType<EvaluationResultsRead>().Results.Length.ShouldBe(1);
    }

    [Fact]
    public async Task InitializeAsync_WhenTheLogHoldsAnInvalidRecord_FailsClosedInsteadOfDroppingEvidence()
    {
        using var root = new JsonEvaluationTestRoot();
        using (var store = root.Open())
        {
            await store.InitializeAsync(Token);
        }

        await File.WriteAllTextAsync(root.LogPath, /*lang=json,strict*/ "{\"not\":\"a result\"}\n", Token);
        using var reopened = root.Open();

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await reopened.InitializeAsync(Token));
        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await reopened.ReadAsync(new EvaluationResultQuery(EvaluationResultConformanceData.NewRun(), 1), Token));
    }

    [Fact]
    public async Task InitializeAsync_WhenTheLogHoldsConflictingRecords_RefusesToOpen()
    {
        using var root = new JsonEvaluationTestRoot();
        var run = EvaluationResultConformanceData.NewRun();
        using (var store = root.Open())
        {
            _ = await store.AppendAsync(EvaluationResultConformanceData.Result(run, latencyMilliseconds: 1), Token);
        }

        var line = (await File.ReadAllLinesAsync(root.LogPath, Token)).Single();
        var conflicting = line.Replace("\"latency\":\"00:00:00.0010000\"", "\"latency\":\"00:00:00.0020000\"", StringComparison.Ordinal);
        conflicting.ShouldNotBe(line);
        await File.AppendAllTextAsync(root.LogPath, conflicting + "\n", Token);
        using var reopened = root.Open();

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await reopened.InitializeAsync(Token));

        exception.Message.ShouldContain("conflicting");
    }

    [Fact]
    public async Task AppendAsync_WhenTheEncodedResultExceedsTheRecordBound_RejectsWithLimitExceededAndWritesNothing()
    {
        using var root = new JsonEvaluationTestRoot();
        using var store = root.Open(settings: new JsonEvaluationStoreSettings(8_192, 1_048_576, JsonEncodingSettings.CreateDefault()));
        var run = EvaluationResultConformanceData.NewRun();
        var small = EvaluationResultConformanceData.Result(run);
        var large = new EvaluationCaseResult(
            run, small.PlanId, small.PlanVersion, small.CaseId, small.CaseOrdinal, small.Repetition, small.Disposition, small.StartedAt, small.Latency,
            small.TraceId, small.Run, small.Manifest, small.Usage, small.Fixture, small.Evaluators, [new EvaluationDiagnostic("big", new string('x', 20_000))]);

        var answer = await store.AppendAsync(large, Token);

        answer.ShouldBeOfType<EvaluationStoreRejected>().Failure.Kind.ShouldBe(EvaluationStoreFailureKind.LimitExceeded);
        (await store.ReadAsync(new EvaluationResultQuery(run, 5), Token)).ShouldBeOfType<EvaluationResultsRead>().Results.ShouldBeEmpty();
        File.Exists(root.LogPath).ShouldBeFalse();
    }

    [Fact]
    public async Task InitializeAsync_WhenTheRecordBoundCannotHoldAMinimalResult_RefusesToOpen()
    {
        using var root = new JsonEvaluationTestRoot();
        using var store = root.Open(settings: new JsonEvaluationStoreSettings(64, 1_048_576, JsonEncodingSettings.CreateDefault()));

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await store.InitializeAsync(Token));

        exception.Message.ShouldContain("record bound");
    }
}
