// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

using AgentKit.Evaluation;

/// <summary>Defines portable append, idempotency, pinning, ordering, paging, concurrency, cancellation, and durability behavior for <see cref="IEvaluationResultStore"/>.</summary>
/// <typeparam name="TFixture">The adapter-specific isolated fixture.</typeparam>
/// <remarks>
/// Every case is required contract behavior for all evaluation result-store adapters. Reopen cases run only when the fixture
/// declares <see cref="ConformanceCapabilities.SupportsDurability"/>; an ephemeral adapter proves instead that nothing survives
/// in its own test project, and never reports a guarantee it does not provide.
/// </remarks>
public abstract class EvaluationResultStoreConformanceTests<TFixture>
    where TFixture : IEvaluationResultStoreConformanceFixture, new()
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Verifies a new result is acknowledged with its identity and is not flagged as a replay.</summary>
    [Fact]
    public async Task AppendAsync_WhenResultIsNew_AcknowledgesItsIdentityWithoutReplay()
    {
        await using var fixture = new TFixture();
        var run = EvaluationResultConformanceData.NewRun();

        var answer = await fixture.Store.AppendAsync(EvaluationResultConformanceData.Result(run, 2, 3, "named"), Token);

        var appended = answer.ShouldBeOfType<EvaluationStoreAppended>();
        appended.Replayed.ShouldBeFalse();
        (appended.Receipt.EvaluationRunId, appended.Receipt.CaseId, appended.Receipt.CaseOrdinal, appended.Receipt.Repetition)
            .ShouldBe((run, new EvaluationCaseId("named"), 2, 3));
    }

    /// <summary>Verifies null arguments are refused before any state is touched.</summary>
    [Fact]
    public async Task AppendAsync_WhenArgumentIsNull_ThrowsArgumentNullException()
    {
        await using var fixture = new TFixture();

        (await Should.ThrowAsync<ArgumentNullException>(async () => await fixture.Store.AppendAsync(null!, Token))).ParamName.ShouldBe("result");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await fixture.Store.ReadAsync(null!, Token))).ParamName.ShouldBe("query");
    }

    /// <summary>Verifies every recorded member round-trips exactly, including every outcome variant and optional member.</summary>
    [Fact]
    public async Task ReadAsync_WhenResultsWereAppended_ReturnsEveryRecordedMemberExactly()
    {
        await using var fixture = new TFixture();
        var run = EvaluationResultConformanceData.NewRun();
        var complete = EvaluationResultConformanceData.Complete(run);
        var sparse = EvaluationResultConformanceData.Sparse(run);
        _ = await fixture.Store.AppendAsync(complete, Token);
        _ = await fixture.Store.AppendAsync(sparse, Token);

        var read = await fixture.Store.ReadAsync(new EvaluationResultQuery(run, 10), Token);

        var page = read.ShouldBeOfType<EvaluationResultsRead>();
        page.Results.ShouldBe([sparse, complete]);
        page.Next.ShouldBeNull();
    }

    /// <summary>Verifies an identical repeat replays the original acknowledgement and records nothing twice.</summary>
    [Fact]
    public async Task AppendAsync_WhenIdenticalResultIsRepeated_ReplaysWithoutDuplicating()
    {
        await using var fixture = new TFixture();
        var run = EvaluationResultConformanceData.NewRun();
        var first = await fixture.Store.AppendAsync(EvaluationResultConformanceData.Result(run), Token);

        var replay = await fixture.Store.AppendAsync(EvaluationResultConformanceData.Result(run), Token);

        replay.ShouldBeOfType<EvaluationStoreAppended>().Replayed.ShouldBeTrue();
        replay.ShouldBeOfType<EvaluationStoreAppended>().Receipt.ShouldBe(first.ShouldBeOfType<EvaluationStoreAppended>().Receipt);
        (await ReadAll(fixture.Store, run)).Count.ShouldBe(1);
    }

    /// <summary>Verifies a different result for a recorded identity is refused and the original is kept.</summary>
    [Fact]
    public async Task AppendAsync_WhenADifferentResultReusesTheIdentity_RejectsWithIdentityConflictAndKeepsTheOriginal()
    {
        await using var fixture = new TFixture();
        var run = EvaluationResultConformanceData.NewRun();
        var original = EvaluationResultConformanceData.Result(run, latencyMilliseconds: 10);
        _ = await fixture.Store.AppendAsync(original, Token);

        var answer = await fixture.Store.AppendAsync(EvaluationResultConformanceData.Result(run, latencyMilliseconds: 99), Token);

        answer.ShouldBeOfType<EvaluationStoreRejected>().Failure.Kind.ShouldBe(EvaluationStoreFailureKind.IdentityConflict);
        (await ReadAll(fixture.Store, run)).ShouldBe([original]);
    }

    /// <summary>Verifies the first result pins a run to one plan identity and version.</summary>
    [Theory]
    [InlineData("other-plan", 1)]
    [InlineData("plan", 2)]
    public async Task AppendAsync_WhenTheRunIsPinnedToAnotherPlanOrVersion_RejectsWithIdentityConflict(string plan, long version)
    {
        await using var fixture = new TFixture();
        var run = EvaluationResultConformanceData.NewRun();
        _ = await fixture.Store.AppendAsync(EvaluationResultConformanceData.Result(run, 0), Token);

        var answer = await fixture.Store.AppendAsync(EvaluationResultConformanceData.Result(run, 1, plan: plan, version: version), Token);

        answer.ShouldBeOfType<EvaluationStoreRejected>().Failure.Kind.ShouldBe(EvaluationStoreFailureKind.IdentityConflict);
        (await ReadAll(fixture.Store, run)).Count.ShouldBe(1);
    }

    /// <summary>Verifies a case occupies exactly one ordinal and an ordinal belongs to exactly one case.</summary>
    [Fact]
    public async Task AppendAsync_WhenACaseAndOrdinalDisagreeWithRecordedEvidence_RejectsWithIdentityConflict()
    {
        await using var fixture = new TFixture();
        var run = EvaluationResultConformanceData.NewRun();
        _ = await fixture.Store.AppendAsync(EvaluationResultConformanceData.Result(run, 0, caseId: "alpha"), Token);

        var otherCaseSameOrdinal = await fixture.Store.AppendAsync(EvaluationResultConformanceData.Result(run, 0, 2, "beta"), Token);
        var sameCaseOtherOrdinal = await fixture.Store.AppendAsync(EvaluationResultConformanceData.Result(run, 1, 1, "alpha"), Token);
        var sameCaseNextRepetition = await fixture.Store.AppendAsync(EvaluationResultConformanceData.Result(run, 0, 2, "alpha"), Token);

        otherCaseSameOrdinal.ShouldBeOfType<EvaluationStoreRejected>().Failure.Kind.ShouldBe(EvaluationStoreFailureKind.IdentityConflict);
        sameCaseOtherOrdinal.ShouldBeOfType<EvaluationStoreRejected>().Failure.Kind.ShouldBe(EvaluationStoreFailureKind.IdentityConflict);
        sameCaseNextRepetition.ShouldBeOfType<EvaluationStoreAppended>().Replayed.ShouldBeFalse();
    }

    /// <summary>Verifies the same ordinal and repetition in different runs never interact.</summary>
    [Fact]
    public async Task AppendAsync_WhenRunsShareOrdinalsAndPlans_KeepsThemIndependent()
    {
        await using var fixture = new TFixture();
        var first = EvaluationResultConformanceData.NewRun();
        var second = EvaluationResultConformanceData.NewRun();

        _ = await fixture.Store.AppendAsync(EvaluationResultConformanceData.Result(first, 0, caseId: "a", plan: "plan-a"), Token);
        var other = await fixture.Store.AppendAsync(EvaluationResultConformanceData.Result(second, 0, caseId: "b", plan: "plan-b", version: 9), Token);

        other.ShouldBeOfType<EvaluationStoreAppended>().Replayed.ShouldBeFalse();
        (await ReadAll(fixture.Store, first)).Single().CaseId.ShouldBe(new EvaluationCaseId("a"));
        (await ReadAll(fixture.Store, second)).Single().CaseId.ShouldBe(new EvaluationCaseId("b"));
    }

    /// <summary>Verifies reads follow case ordinal then repetition regardless of append order.</summary>
    [Fact]
    public async Task ReadAsync_WhenResultsArriveOutOfOrder_ReturnsCaseOrdinalThenRepetitionOrder()
    {
        await using var fixture = new TFixture();
        var run = EvaluationResultConformanceData.NewRun();
        (int Ordinal, int Repetition)[] arrival = [(2, 1), (0, 2), (1, 1), (0, 1), (2, 2), (1, 2)];
        foreach (var (ordinal, repetition) in arrival)
        {
            _ = await fixture.Store.AppendAsync(EvaluationResultConformanceData.Result(run, ordinal, repetition), Token);
        }

        var results = await ReadAll(fixture.Store, run);

        results.Select(static r => (r.CaseOrdinal, r.Repetition)).ShouldBe([(0, 1), (0, 2), (1, 1), (1, 2), (2, 1), (2, 2)]);
    }

    /// <summary>Verifies an unknown run reads as an empty page rather than an error.</summary>
    [Fact]
    public async Task ReadAsync_WhenTheRunIsUnknown_ReturnsAnEmptyPageWithNoContinuation()
    {
        await using var fixture = new TFixture();

        var read = await fixture.Store.ReadAsync(new EvaluationResultQuery(EvaluationResultConformanceData.NewRun(), 5), Token);

        var page = read.ShouldBeOfType<EvaluationResultsRead>();
        page.Results.ShouldBeEmpty();
        page.Next.ShouldBeNull();
    }

    /// <summary>Verifies paging yields every result exactly once, with a continuation only while more remain.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(EvaluationResultQuery.MaximumPageSize)]
    public async Task ReadAsync_WhenPagingThroughARun_ReturnsEveryResultOnceAndStopsWithoutAContinuation(int pageSize)
    {
        await using var fixture = new TFixture();
        var run = EvaluationResultConformanceData.NewRun();
        foreach (var ordinal in Enumerable.Range(0, 5))
        {
            _ = await fixture.Store.AppendAsync(EvaluationResultConformanceData.Result(run, ordinal), Token);
        }

        var seen = new List<int>();
        EvaluationResultCursor? cursor = null;
        var pages = 0;
        do
        {
            var page = (await fixture.Store.ReadAsync(new EvaluationResultQuery(run, pageSize, cursor), Token)).ShouldBeOfType<EvaluationResultsRead>();
            page.Results.Length.ShouldBeLessThanOrEqualTo(pageSize);
            seen.AddRange(page.Results.Select(static r => r.CaseOrdinal));
            cursor = page.Next;
            pages++;
        }
        while (cursor is not null);

        seen.ShouldBe([0, 1, 2, 3, 4]);
        pages.ShouldBe((5 + pageSize - 1) / pageSize);
    }

    /// <summary>Verifies a cursor names a position, so later results at earlier positions are not repeated or skipped incorrectly.</summary>
    [Fact]
    public async Task ReadAsync_WhenACursorIsGiven_ReturnsOnlyResultsStrictlyAfterThatPosition()
    {
        await using var fixture = new TFixture();
        var run = EvaluationResultConformanceData.NewRun();
        foreach (var (ordinal, repetition) in new[] { (0, 1), (0, 2), (1, 1) })
        {
            _ = await fixture.Store.AppendAsync(EvaluationResultConformanceData.Result(run, ordinal, repetition), Token);
        }

        var read = await fixture.Store.ReadAsync(new EvaluationResultQuery(run, 10, new EvaluationResultCursor(0, 1)), Token);

        read.ShouldBeOfType<EvaluationResultsRead>().Results.Select(static r => (r.CaseOrdinal, r.Repetition)).ShouldBe([(0, 2), (1, 1)]);
    }

    /// <summary>Verifies a read returns only the requested run.</summary>
    [Fact]
    public async Task ReadAsync_WhenOtherRunsHaveResults_ReturnsOnlyTheRequestedRun()
    {
        await using var fixture = new TFixture();
        var first = EvaluationResultConformanceData.NewRun();
        var second = EvaluationResultConformanceData.NewRun();
        _ = await fixture.Store.AppendAsync(EvaluationResultConformanceData.Result(first, 0), Token);
        _ = await fixture.Store.AppendAsync(EvaluationResultConformanceData.Result(second, 0), Token);
        _ = await fixture.Store.AppendAsync(EvaluationResultConformanceData.Result(second, 1), Token);

        (await ReadAll(fixture.Store, first)).Count.ShouldBe(1);
        (await ReadAll(fixture.Store, second)).Count.ShouldBe(2);
    }

    /// <summary>Verifies cancellation before an append is observed throws and records nothing.</summary>
    [Fact]
    public async Task AppendAsync_WhenTokenIsAlreadyCancelled_ThrowsBeforeRecordingAnything()
    {
        await using var fixture = new TFixture();
        var run = EvaluationResultConformanceData.NewRun();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await fixture.Store.AppendAsync(EvaluationResultConformanceData.Result(run), cancellation.Token));

        (await ReadAll(fixture.Store, run)).ShouldBeEmpty();
    }

    /// <summary>Verifies cancellation before a read is observed throws.</summary>
    [Fact]
    public async Task ReadAsync_WhenTokenIsAlreadyCancelled_Throws()
    {
        await using var fixture = new TFixture();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await fixture.Store.ReadAsync(new EvaluationResultQuery(EvaluationResultConformanceData.NewRun(), 5), cancellation.Token));
    }

    /// <summary>Verifies concurrent distinct appends are all recorded.</summary>
    [Fact]
    public async Task AppendAsync_WhenDistinctResultsAreAppendedConcurrently_RecordsEachOnce()
    {
        await using var fixture = new TFixture();
        var run = EvaluationResultConformanceData.NewRun();

        var answers = await Task.WhenAll(Enumerable.Range(0, 24).Select(async ordinal =>
            await Task.Run(async () => await fixture.Store.AppendAsync(EvaluationResultConformanceData.Result(run, ordinal), Token), Token)));

        answers.Count(static answer => answer is EvaluationStoreAppended { Replayed: false }).ShouldBe(24);
        (await ReadAll(fixture.Store, run)).Select(static r => r.CaseOrdinal).ShouldBe(Enumerable.Range(0, 24));
    }

    /// <summary>Verifies concurrent identical appends produce exactly one fresh record and replays for the rest.</summary>
    [Fact]
    public async Task AppendAsync_WhenTheSameResultIsAppendedConcurrently_RecordsOnceAndReplaysTheRest()
    {
        await using var fixture = new TFixture();
        var run = EvaluationResultConformanceData.NewRun();

        var answers = await Task.WhenAll(Enumerable.Range(0, 16).Select(async _ =>
            await Task.Run(async () => await fixture.Store.AppendAsync(EvaluationResultConformanceData.Result(run), Token), Token)));

        answers.OfType<EvaluationStoreAppended>().Count(static a => !a.Replayed).ShouldBe(1);
        answers.OfType<EvaluationStoreAppended>().Count(static a => a.Replayed).ShouldBe(15);
        (await ReadAll(fixture.Store, run)).Count.ShouldBe(1);
    }

    /// <summary>Verifies acknowledged results, pinning, and idempotency survive reopening a durable store.</summary>
    [Fact]
    public async Task AppendAsync_WhenTheStoreIsReopened_StillServesPinsAndReplaysAcknowledgedResults()
    {
        await using var fixture = new TFixture();
        if (!fixture.Capabilities.SupportsDurability)
        {
            return;
        }

        var run = EvaluationResultConformanceData.NewRun();
        var complete = EvaluationResultConformanceData.Complete(run);
        _ = await fixture.Store.AppendAsync(complete, Token);
        var first = EvaluationResultConformanceData.Result(run, 0, caseId: "first", plan: "complete-plan", version: 7);
        _ = await fixture.Store.AppendAsync(first, Token);

        var reopened = await fixture.ReopenAsync(Token);

        (await ReadAll(reopened, run)).ShouldBe([first, complete]);
        (await reopened.AppendAsync(complete, Token)).ShouldBeOfType<EvaluationStoreAppended>().Replayed.ShouldBeTrue();
        (await reopened.AppendAsync(EvaluationResultConformanceData.Result(run, 5, plan: "other", version: 7), Token))
            .ShouldBeOfType<EvaluationStoreRejected>().Failure.Kind.ShouldBe(EvaluationStoreFailureKind.IdentityConflict);
    }

    private static async Task<IReadOnlyList<EvaluationCaseResult>> ReadAll(IEvaluationResultStore store, EvaluationRunId run)
    {
        var all = new List<EvaluationCaseResult>();
        EvaluationResultCursor? cursor = null;
        do
        {
            var page = (await store.ReadAsync(new EvaluationResultQuery(run, 3, cursor), Token)).ShouldBeOfType<EvaluationResultsRead>();
            all.AddRange(page.Results);
            cursor = page.Next;
        }
        while (cursor is not null);

        return all;
    }
}
