// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

/// <summary>Verifies <see cref="MemoryEvent"/> constraints and content-free shape.</summary>
public sealed class MemoryEventTests
{
    private static readonly AgentId _agent = new(Guid.NewGuid());
    private static readonly SessionId _session = new(Guid.NewGuid());

    private static MemoryEvent Create(
        MemoryEventKind kind = MemoryEventKind.ProposalAccepted,
        TenantId? tenant = null,
        AgentId? agent = null,
        SessionId? session = null,
        MemoryProfileKey? profile = null,
        MemoryProfileVersion? version = null,
        MemoryId? memory = null,
        DocumentId? document = null,
        RetrievalRequestId? request = null,
        string outcome = "accepted",
        int? count = null) => new(
            kind, tenant ?? new TenantId("t"), agent ?? _agent, session ?? _session, profile ?? new MemoryProfileKey("p"),
            version ?? new MemoryProfileVersion(1), memory, document, request, outcome, count, DateTimeOffset.UnixEpoch);

    [Fact]
    public void Constructor_WhenValid_PreservesEveryValue()
    {
        var memory = new MemoryId(Guid.NewGuid());

        var memoryEvent = Create(MemoryEventKind.MemoryDeleted, memory: memory, outcome: "tombstoned", count: 2);

        memoryEvent.Kind.ShouldBe(MemoryEventKind.MemoryDeleted);
        memoryEvent.MemoryId.ShouldBe(memory);
        memoryEvent.Outcome.ShouldBe("tombstoned");
        memoryEvent.Count.ShouldBe(2);
        memoryEvent.OccurredAt.ShouldBe(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Create(kind: (MemoryEventKind) 99)).ParamName.ShouldBe("kind");

    [Fact]
    public void Constructor_WhenTenantOrProfileKeyIsDefault_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => Create(tenant: default(TenantId))).ParamName.ShouldBe("tenantId");
        Should.Throw<ArgumentNullException>(() => Create(profile: default(MemoryProfileKey))).ParamName.ShouldBe("profileKey");
    }

    [Fact]
    public void Constructor_WhenAnIdentityIsDefault_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Create(agent: default(AgentId))).ParamName.ShouldBe("agentId");
        Should.Throw<ArgumentOutOfRangeException>(() => Create(session: default(SessionId))).ParamName.ShouldBe("sessionId");
        Should.Throw<ArgumentOutOfRangeException>(() => Create(memory: default(MemoryId))).ParamName.ShouldBe("memoryId");
        Should.Throw<ArgumentOutOfRangeException>(() => Create(document: default(DocumentId))).ParamName.ShouldBe("documentId");
        Should.Throw<ArgumentOutOfRangeException>(() => Create(request: default(RetrievalRequestId))).ParamName.ShouldBe("retrievalRequestId");
        Should.Throw<ArgumentOutOfRangeException>(() => Create(version: default(MemoryProfileVersion))).ParamName.ShouldBe("profileVersion");
    }

    [Fact]
    public void Constructor_WhenOutcomeIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Create(outcome: " ")).ParamName.ShouldBe("outcome");

    [Fact]
    public void Constructor_WhenCountIsNegative_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Create(count: -1)).ParamName.ShouldBe("count");
}
