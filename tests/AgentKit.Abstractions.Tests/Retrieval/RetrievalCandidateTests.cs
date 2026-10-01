// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Retrieval;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="RetrievalCandidate"/> constraints that keep candidates data with provenance.</summary>
public sealed class RetrievalCandidateTests
{
    private static readonly RetrievalRequestId _request = new(Guid.NewGuid());
    private static readonly RetrievalSourceIdentity _source = new(new RetrievalSourceKey("s"), "1", null);

    private static RetrievalCandidate Create(MemoryId? memory, DocumentId? document, ChunkId? chunk, double score = 1) => new(
        _request, _source, memory, document, chunk, new CandidateContent("t"), new Provenance("p"), TrustClassification.UntrustedData, score, DataClassification.Internal);

    [Fact]
    public void Constructor_WhenMemoryCandidate_PreservesEveryValue()
    {
        var memory = new MemoryId(Guid.NewGuid());

        var candidate = MemoryTestData.Candidate(_request, "text", 0.5, "s", memory);

        candidate.MemoryId.ShouldBe(memory);
        candidate.DocumentId.ShouldBeNull();
        candidate.Score.ShouldBe(0.5);
        candidate.Trust.ShouldBe(TrustClassification.UntrustedData);
    }

    [Fact]
    public void Constructor_WhenDocumentChunkCandidate_Accepts() =>
        Create(null, new DocumentId(Guid.NewGuid()), new ChunkId(Guid.NewGuid())).ChunkId.ShouldNotBeNull();

    [Fact]
    public void Constructor_WhenItNamesNeitherMemoryNorDocument_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Create(null, null, null)).ParamName.ShouldBe("memoryId");

    [Fact]
    public void Constructor_WhenItNamesBothMemoryAndDocument_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Create(new MemoryId(Guid.NewGuid()), new DocumentId(Guid.NewGuid()), null)).ParamName.ShouldBe("memoryId");

    [Fact]
    public void Constructor_WhenItNamesAChunkWithoutADocument_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Create(new MemoryId(Guid.NewGuid()), null, new ChunkId(Guid.NewGuid()))).ParamName.ShouldBe("memoryId");

    [Fact]
    public void Constructor_WhenAnIdentityIsDefault_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Create(default(MemoryId), null, null)).ParamName.ShouldBe("memoryId");
        Should.Throw<ArgumentOutOfRangeException>(() => Create(null, default(DocumentId), null)).ParamName.ShouldBe("documentId");
        Should.Throw<ArgumentOutOfRangeException>(() => Create(null, new DocumentId(Guid.NewGuid()), default(ChunkId))).ParamName.ShouldBe("chunkId");
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_WhenScoreIsNotFinite_ThrowsArgumentOutOfRangeException(double score) =>
        Should.Throw<ArgumentOutOfRangeException>(() => Create(new MemoryId(Guid.NewGuid()), null, null, score)).ParamName.ShouldBe("score");

    [Fact]
    public void Constructor_WhenRequestIdIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RetrievalCandidate(default, _source, new MemoryId(Guid.NewGuid()), null, null, new CandidateContent("t"), new Provenance("p"), TrustClassification.Stored, 1, DataClassification.Public)).ParamName.ShouldBe("requestId");

    [Fact]
    public void Constructor_WhenAReferenceIsNull_ThrowsArgumentNullException()
    {
        var memory = new MemoryId(Guid.NewGuid());

        Should.Throw<ArgumentNullException>(() => new RetrievalCandidate(_request, null!, memory, null, null, new CandidateContent("t"), new Provenance("p"), TrustClassification.Stored, 1, DataClassification.Public)).ParamName.ShouldBe("source");
        Should.Throw<ArgumentNullException>(() => new RetrievalCandidate(_request, _source, memory, null, null, null!, new Provenance("p"), TrustClassification.Stored, 1, DataClassification.Public)).ParamName.ShouldBe("content");
        Should.Throw<ArgumentNullException>(() => new RetrievalCandidate(_request, _source, memory, null, null, new CandidateContent("t"), null!, TrustClassification.Stored, 1, DataClassification.Public)).ParamName.ShouldBe("provenance");
    }

    [Fact]
    public void Constructor_WhenEnumerationsAreUndefined_ThrowsArgumentOutOfRangeException()
    {
        var memory = new MemoryId(Guid.NewGuid());

        Should.Throw<ArgumentOutOfRangeException>(() => new RetrievalCandidate(_request, _source, memory, null, null, new CandidateContent("t"), new Provenance("p"), (TrustClassification) 9, 1, DataClassification.Public)).ParamName.ShouldBe("trust");
        Should.Throw<ArgumentOutOfRangeException>(() => new RetrievalCandidate(_request, _source, memory, null, null, new CandidateContent("t"), new Provenance("p"), TrustClassification.Stored, 1, (DataClassification) 9)).ParamName.ShouldBe("classification");
    }

    [Fact]
    public void WithScore_WhenScoreChanges_KeepsIdentityContentAndProvenance()
    {
        var candidate = MemoryTestData.Candidate(_request, "text", 1);

        var rescored = candidate.WithScore(9);

        rescored.Score.ShouldBe(9);
        rescored.MemoryId.ShouldBe(candidate.MemoryId);
        rescored.Content.ShouldBe(candidate.Content);
        rescored.Provenance.ShouldBe(candidate.Provenance);
        Should.Throw<ArgumentOutOfRangeException>(() => candidate.WithScore(double.NaN)).ParamName.ShouldBe("score");
    }
}
