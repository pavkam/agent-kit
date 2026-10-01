// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.InMemory.Tests;

using System.Diagnostics;

using AgentKit.Observability;

/// <summary>Verifies <see cref="InMemoryVectorIndex"/> argument constraints, capability claims, and observability.</summary>
public sealed class InMemoryVectorIndexTests
{
    [Fact]
    public void Constructor_WhenADependencyIsInvalid_ThrowsNamingIt()
    {
        var grants = new TestGoalGrants();
        var ids = new TestIntentIds();
        var space = MemoryTestData.Space();

        Should.Throw<ArgumentNullException>(() => new InMemoryVectorIndex(null!, grants, ids, TimeProvider.System)).ParamName.ShouldBe("space");
        Should.Throw<ArgumentNullException>(() => new InMemoryVectorIndex(space, null!, ids, TimeProvider.System)).ParamName.ShouldBe("grants");
        Should.Throw<ArgumentNullException>(() => new InMemoryVectorIndex(space, grants, null!, TimeProvider.System)).ParamName.ShouldBe("intentIds");
        Should.Throw<ArgumentNullException>(() => new InMemoryVectorIndex(space, grants, ids, null!)).ParamName.ShouldBe("time");
    }

    [Fact]
    public void Constructor_WhenConstructed_ClaimsAnExactNonDurableScan()
    {
        var space = MemoryTestData.Space();

        var index = new InMemoryVectorIndex(space, new TestGoalGrants(), new TestIntentIds(), TimeProvider.System);

        index.VectorSpace.ShouldBe(space);
        index.IsDurable.ShouldBeFalse();
        index.ApproximateSearch.ShouldBeFalse();
        index.SecurityAudience.ShouldBe(new ComponentId("agentkit.vectors.in-memory"));
    }

    [Fact]
    public async Task Operations_WhenRequestIsNull_ThrowArgumentNullExceptionBeforeAnyEffect()
    {
        var index = new InMemoryVectorIndex(MemoryTestData.Space(), new TestGoalGrants(), new TestIntentIds(), TimeProvider.System);

        (await Should.ThrowAsync<ArgumentNullException>(async () => await index.UpsertAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await index.SearchAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await index.DeleteAsync(null!))).ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task SearchAsync_WhenObserved_EmitsOneSpanWithTheVectorFamilyAndNoQueryContent()
    {
        var grants = new TestGoalGrants();
        var index = new InMemoryVectorIndex(MemoryTestData.Space(), grants, new TestIntentIds(), TimeProvider.System);
        var owner = MemoryTestData.NewOwner($"tenant-{Guid.NewGuid():N}");
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            observation => observation.OperationName == AgentKitActivityNames.MemoryStoreOperation
                && observation.GetTagItem(AgentKitTagNames.TenantId)?.ToString() == owner.Identity.TenantId.Value
                && observation.GetTagItem(AgentKitTagNames.MemoryStoreFamily)?.ToString() == "vector");
        var request = new VectorIndexRequestFactory(grants, index.SecurityAudience).Search(index.VectorSpace, [0.25f, 0.5f, 0.75f], 3, owner.Authorization);

        _ = await index.SearchAsync(request, TestContext.Current.CancellationToken);

        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Ok);
        span.GetTagItem(AgentKitTagNames.MemoryStoreOperation).ShouldBe("search");
        span.Tags.Select(static tag => tag.Value).Any(static value => value?.ToString()?.Contains("0.25", StringComparison.Ordinal) == true).ShouldBeFalse();
    }

    [Fact]
    public async Task UpsertAsync_WhenTheSpaceIsIncompatible_RecordsAnIncompatibleSpaceOutcomeWithoutConsumingAGrant()
    {
        var grants = new TestGoalGrants();
        var logger = new RecordingLogger<InMemoryVectorIndex>();
        var index = new InMemoryVectorIndex(MemoryTestData.Space(), grants, new TestIntentIds(), TimeProvider.System, logger);
        var owner = MemoryTestData.NewOwner($"tenant-{Guid.NewGuid():N}");
        var foreign = MemoryTestData.Space(model: "embed-2");
        var chunk = MemoryTestData.Chunks(MemoryTestData.Document(owner), 1)[0];
        var request = new VectorIndexRequestFactory(grants, index.SecurityAudience).Upsert(foreign, [MemoryTestData.Vector(owner, chunk, 1, 0, 0)], owner.Authorization);
        var consumed = grants.ConsumedCount;

        var result = await index.UpsertAsync(request, TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.IncompatibleVectorSpace);
        grants.ConsumedCount.ShouldBe(consumed);
        logger.Snapshot().ShouldHaveSingleItem().Message.ShouldContain("incompatible_vector_space");
    }

    [Fact]
    public async Task SearchAsync_WhenTheLoggerThrows_StillReturnsTheSemanticResult()
    {
        var grants = new TestGoalGrants();
        var index = new InMemoryVectorIndex(MemoryTestData.Space(), grants, new TestIntentIds(), TimeProvider.System, new RecordingLogger<InMemoryVectorIndex> { ThrowOnWrite = true });
        var owner = MemoryTestData.NewOwner($"tenant-{Guid.NewGuid():N}");

        var result = await index.SearchAsync(new VectorIndexRequestFactory(grants, index.SecurityAudience).Search(index.VectorSpace, [1f, 0f, 0f], 3, owner.Authorization), TestContext.Current.CancellationToken);

        result.IsSearched.ShouldBeTrue();
    }
}
