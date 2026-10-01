// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Tests;

/// <summary>Verifies the default budget policy's item, byte, and token ceilings and stable truncation.</summary>
public sealed class BoundedRetrievalBudgetPolicyTests
{
    private static BoundedRetrievalBudgetPolicy Policy(double bytesPerToken = 4) =>
        new(Options.Create(new AgentMemoryOptions { EstimatedBytesPerToken = bytesPerToken }));

    private static ImmutableArray<RetrievalCandidate> Candidates(RetrievalRequestId id, params string[] texts) =>
        [.. texts.Select(text => MemoryTestData.Candidate(id, text))];

    [Fact]
    public async Task SelectAsync_WhenEverythingFits_SelectsAllInOrder()
    {
        var id = new RetrievalRequestId(Guid.NewGuid());
        var candidates = Candidates(id, "aaaa", "bbbb");

        var decision = await Policy().SelectAsync(new RetrievalBudgetRequest(new RetrievalBudget(5, 100, 100), candidates), TestContext.Current.CancellationToken);

        decision.Selected.ShouldBe(candidates);
        decision.Omitted.ShouldBe(0);
    }

    [Fact]
    public async Task SelectAsync_WhenItemCeilingIsReached_StopsAtTheCeiling()
    {
        var id = new RetrievalRequestId(Guid.NewGuid());

        var decision = await Policy().SelectAsync(
            new RetrievalBudgetRequest(new RetrievalBudget(1, 100, 100), Candidates(id, "aaaa", "bbbb", "cccc")), TestContext.Current.CancellationToken);

        decision.Selected.Length.ShouldBe(1);
        decision.Omitted.ShouldBe(2);
    }

    [Fact]
    public async Task SelectAsync_WhenByteCeilingIsReached_DoesNotSkipAheadToSmallerCandidates()
    {
        var id = new RetrievalRequestId(Guid.NewGuid());

        var decision = await Policy().SelectAsync(
            new RetrievalBudgetRequest(new RetrievalBudget(5, 10, 100), Candidates(id, "aaaaaa", "bbbbbb", "c")), TestContext.Current.CancellationToken);

        decision.Selected.Select(static candidate => candidate.Content.Text).ShouldBe(["aaaaaa"]);
        decision.Omitted.ShouldBe(2);
    }

    [Fact]
    public async Task SelectAsync_WhenTokenCeilingIsReached_UsesTheEstimatedBytesPerToken()
    {
        var id = new RetrievalRequestId(Guid.NewGuid());

        var decision = await Policy(bytesPerToken: 2).SelectAsync(
            new RetrievalBudgetRequest(new RetrievalBudget(5, 1_000, 3), Candidates(id, "aaaa", "bbbb")), TestContext.Current.CancellationToken);

        decision.Selected.Length.ShouldBe(1);
        decision.Omitted.ShouldBe(1);
    }

    [Fact]
    public async Task SelectAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var exception = await Should.ThrowAsync<ArgumentNullException>(async () => await Policy().SelectAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task SelectAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var request = new RetrievalBudgetRequest(new RetrievalBudget(1, 1, 1), []);

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await Policy().SelectAsync(request, source.Token));
    }
}
