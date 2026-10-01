// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Documents;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="DocumentWriteRequest"/> constraints on the versioned chunk set.</summary>
public sealed class DocumentWriteRequestTests
{
    [Fact]
    public void Constructor_WhenChunkSetIsConsistent_PreservesEveryValue()
    {
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Document(owner);
        var chunks = MemoryTestData.Chunks(record, 3);

        var request = new DocumentWriteRequest(record, chunks, true, new IdempotencyKey("k"), MemoryTestData.Now, MemoryTestData.Grant(owner));

        request.Record.ShouldBe(record);
        request.Chunks.ShouldBe(chunks);
        request.Activate.ShouldBeTrue();
        request.At.ShouldBe(MemoryTestData.Now);
    }

    [Fact]
    public void Constructor_WhenRecordIsNull_ThrowsArgumentNullException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentNullException>(() => new DocumentWriteRequest(null!, MemoryTestData.Chunks(MemoryTestData.Document(owner)), false, new IdempotencyKey("k"), MemoryTestData.Now, MemoryTestData.Grant(owner))).ParamName.ShouldBe("record");
    }

    [Fact]
    public void Constructor_WhenChunksAreDefaultOrEmpty_ThrowsArgumentException()
    {
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Document(owner);

        Should.Throw<ArgumentException>(() => new DocumentWriteRequest(record, default, false, new IdempotencyKey("k"), MemoryTestData.Now, MemoryTestData.Grant(owner))).ParamName.ShouldBe("chunks");
        Should.Throw<ArgumentException>(() => new DocumentWriteRequest(record, [], false, new IdempotencyKey("k"), MemoryTestData.Now, MemoryTestData.Grant(owner))).ParamName.ShouldBe("chunks");
    }

    [Fact]
    public void Constructor_WhenAChunkBelongsToAnotherVersion_ThrowsArgumentException()
    {
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Document(owner);
        var other = MemoryTestData.Chunks(MemoryTestData.Document(owner, "v2", id: record.Id));

        Should.Throw<ArgumentException>(() => new DocumentWriteRequest(record, other, false, new IdempotencyKey("k"), MemoryTestData.Now, MemoryTestData.Grant(owner))).ParamName.ShouldBe("chunks");
    }

    [Fact]
    public void Constructor_WhenOrdinalsAreNotContiguous_ThrowsArgumentException()
    {
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Document(owner);
        var chunks = MemoryTestData.Chunks(record, 3);

        Should.Throw<ArgumentException>(() => new DocumentWriteRequest(record, [chunks[0], chunks[2]], false, new IdempotencyKey("k"), MemoryTestData.Now, MemoryTestData.Grant(owner))).ParamName.ShouldBe("chunks");
    }

    [Fact]
    public void Constructor_WhenChunkersDiffer_ThrowsArgumentException()
    {
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Document(owner);
        var first = MemoryTestData.Chunks(record, 2);
        var second = MemoryTestData.Chunks(record, 2, "another-chunker");

        Should.Throw<ArgumentException>(() => new DocumentWriteRequest(record, [first[0], second[1]], false, new IdempotencyKey("k"), MemoryTestData.Now, MemoryTestData.Grant(owner))).ParamName.ShouldBe("chunks");
    }

    [Fact]
    public void Constructor_WhenChunkIdentitiesRepeat_ThrowsArgumentException()
    {
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Document(owner);
        var chunks = MemoryTestData.Chunks(record, 2);
        var duplicate = new DocumentChunk(chunks[0].Id, record.Id, record.Version, chunks[0].Chunker, 1, "again", chunks[0].Hash);

        Should.Throw<ArgumentException>(() => new DocumentWriteRequest(record, [chunks[0], duplicate], false, new IdempotencyKey("k"), MemoryTestData.Now, MemoryTestData.Grant(owner))).ParamName.ShouldBe("chunks");
    }

    [Fact]
    public void Constructor_WhenKeyIsDefault_ThrowsArgumentNullException()
    {
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Document(owner);

        Should.Throw<ArgumentNullException>(() => new DocumentWriteRequest(record, MemoryTestData.Chunks(record), false, default, MemoryTestData.Now, MemoryTestData.Grant(owner))).ParamName.ShouldBe("idempotencyKey");
    }

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsArgumentNullException()
    {
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Document(owner);
        var chunks = MemoryTestData.Chunks(record);

        Should.Throw<ArgumentNullException>(() => new DocumentWriteRequest(record, chunks, false, new IdempotencyKey("k"), MemoryTestData.Now, null!)).ParamName.ShouldBe("grant");
    }
}
