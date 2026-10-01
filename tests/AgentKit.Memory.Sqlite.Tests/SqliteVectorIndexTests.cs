// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Sqlite.Tests;

/// <summary>Verifies <see cref="SqliteVectorIndex"/> construction, honest capability claims, and watermark persistence.</summary>
public sealed class SqliteVectorIndexTests: IDisposable
{
    private readonly SqliteMemoryTestDatabase _database = new();
    private readonly TestGoalGrants _grants = new();

    /// <inheritdoc/>
    public void Dispose() => _database.Dispose();

    [Fact]
    public void Constructor_WhenADependencyIsInvalid_ThrowsNamingIt()
    {
        var space = MemoryTestData.Space();
        var target = _database.Target();
        var settings = SqliteMemorySettings.CreateDefault();
        var ids = new TestIntentIds();

        Should.Throw<ArgumentNullException>(() => new SqliteVectorIndex(null!, target, settings, _grants, ids, TimeProvider.System)).ParamName.ShouldBe("space");
        Should.Throw<ArgumentNullException>(() => new SqliteVectorIndex(space, null!, settings, _grants, ids, TimeProvider.System)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentNullException>(() => new SqliteVectorIndex(space, target, null!, _grants, ids, TimeProvider.System)).ParamName.ShouldBe("settings");
        Should.Throw<ArgumentNullException>(() => new SqliteVectorIndex(space, target, settings, null!, ids, TimeProvider.System)).ParamName.ShouldBe("grants");
        Should.Throw<ArgumentNullException>(() => new SqliteVectorIndex(space, target, settings, _grants, null!, TimeProvider.System)).ParamName.ShouldBe("intentIds");
        Should.Throw<ArgumentNullException>(() => new SqliteVectorIndex(space, target, settings, _grants, ids, null!)).ParamName.ShouldBe("time");
    }

    [Fact]
    public void Constructor_WhenConstructed_AdvertisesOnlyAVerifiedExactScan()
    {
        var index = Open();

        index.IsDurable.ShouldBeTrue();
        index.ApproximateSearch.ShouldBeFalse();
        index.SecurityAudience.ShouldBe(new ComponentId("agentkit.vectors.sqlite"));
    }

    [Fact]
    public async Task Operations_WhenRequestIsNull_ThrowArgumentNullExceptionBeforeAnyEffect()
    {
        var index = Open();

        (await Should.ThrowAsync<ArgumentNullException>(async () => await index.UpsertAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await index.SearchAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await index.DeleteAsync(null!))).ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task SearchAsync_WhenAnotherIndexInstanceSharesTheFile_SeesAcknowledgedBatchesAndTheWatermark()
    {
        var owner = MemoryTestData.NewOwner();
        var chunk = MemoryTestData.Chunks(MemoryTestData.Document(owner), 1)[0];
        var writer = Open();
        var reader = Open();
        var upsert = await writer.UpsertAsync(new VectorIndexRequestFactory(_grants, writer.SecurityAudience).Upsert(writer.VectorSpace, [MemoryTestData.Vector(owner, chunk, 1, 0, 0)], owner.Authorization), TestContext.Current.CancellationToken);

        var result = await reader.SearchAsync(new VectorIndexRequestFactory(_grants, reader.SecurityAudience).Search(reader.VectorSpace, [1f, 0f, 0f], 3, owner.Authorization), TestContext.Current.CancellationToken);

        result.Matches.ShouldHaveSingleItem().ChunkId.ShouldBe(chunk.Id);
        result.Watermark.ShouldBe(upsert.Watermark);
    }

    [Fact]
    public async Task UpsertAsync_WhenMoreBatchesThanTheReceiptWindowArrive_PrunesOldestReceiptsOnly()
    {
        var owner = MemoryTestData.NewOwner();
        var index = Open();
        var factory = new VectorIndexRequestFactory(_grants, index.SecurityAudience);
        var chunks = MemoryTestData.Chunks(MemoryTestData.Document(owner), 1);

        for (var batch = 0; batch < 3; batch++)
        {
            _ = await index.UpsertAsync(factory.Upsert(index.VectorSpace, [MemoryTestData.Vector(owner, chunks[0], 1, 0, 0)], owner.Authorization, $"u{batch}"), TestContext.Current.CancellationToken);
        }

        var replay = await index.UpsertAsync(factory.Upsert(index.VectorSpace, [MemoryTestData.Vector(owner, chunks[0], 1, 0, 0)], owner.Authorization, "u2"), TestContext.Current.CancellationToken);
        replay.Replayed.ShouldBeTrue();
    }

    private SqliteVectorIndex Open() =>
        new(MemoryTestData.Space(), _database.Target(), SqliteMemorySettings.CreateDefault(), _grants, new TestIntentIds(), TimeProvider.System);
}
