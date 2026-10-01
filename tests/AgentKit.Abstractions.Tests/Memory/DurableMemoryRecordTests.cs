// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="DurableMemoryRecord"/> constraints and state derivation.</summary>
public sealed class DurableMemoryRecordTests
{
    [Fact]
    public void Constructor_WhenValid_PreservesEveryValue()
    {
        var owner = MemoryTestData.NewOwner();

        var record = MemoryTestData.Record(owner, "text", MemoryLifecycleState.Accepted, "notes", shared: true, DataClassification.Confidential);

        record.AgentId.ShouldBe(owner.AgentId);
        record.Namespace.ShouldBe(new MemoryNamespace("notes"));
        record.TenantId.ShouldBe(owner.Identity.TenantId);
        record.Visibility.SharedWithTenant.ShouldBeTrue();
        record.State.ShouldBe(MemoryLifecycleState.Accepted);
        record.Classification.ShouldBe(DataClassification.Confidential);
        record.Version.ShouldBe(new VersionToken("1"));
    }

    [Fact]
    public void Constructor_WhenVisibilityBelongsToAnotherTenant_ThrowsArgumentException()
    {
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Record(owner);

        Should.Throw<ArgumentException>(() => new DurableMemoryRecord(
            record.Id, record.AgentId, record.SourceSessionId, record.SourceRunId, record.Namespace, new TenantId("another"),
            record.Visibility, record.Kind, record.Content, record.Classification, record.Provenance, record.Retention,
            record.State, record.Version, record.CreatedAt, record.UpdatedAt, record.Extensions)).ParamName.ShouldBe("visibility");
    }

    [Fact]
    public void Constructor_WhenUpdateIsBeforeCreation_ThrowsArgumentOutOfRangeException()
    {
        var record = MemoryTestData.Record(MemoryTestData.NewOwner());

        Should.Throw<ArgumentOutOfRangeException>(() => new DurableMemoryRecord(
            record.Id, record.AgentId, record.SourceSessionId, record.SourceRunId, record.Namespace, record.TenantId,
            record.Visibility, record.Kind, record.Content, record.Classification, record.Provenance, record.Retention,
            record.State, record.Version, record.CreatedAt, record.CreatedAt.AddTicks(-1), record.Extensions)).ParamName.ShouldBe("updatedAt");
    }

    [Fact]
    public void Constructor_WhenStateOrKindOrClassificationIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentOutOfRangeException>(() => MemoryTestData.Record(owner, state: (MemoryLifecycleState) 99)).ParamName.ShouldBe("state");
        Should.Throw<ArgumentOutOfRangeException>(() => MemoryTestData.Record(owner, classification: (DataClassification) 99)).ParamName.ShouldBe("classification");
    }

    [Fact]
    public void Constructor_WhenIdentityIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var owner = MemoryTestData.NewOwner();

        Should.Throw<ArgumentOutOfRangeException>(() => MemoryTestData.Record(owner, id: default(MemoryId))).ParamName.ShouldBe("id");
    }

    [Fact]
    public void WithState_WhenTransitioning_ChangesOnlyStateVersionAndUpdateInstant()
    {
        var record = MemoryTestData.Record(MemoryTestData.NewOwner());
        var at = MemoryTestData.Now.AddMinutes(5);

        var next = record.WithState(MemoryLifecycleState.Expired, new VersionToken("2"), at);

        next.State.ShouldBe(MemoryLifecycleState.Expired);
        next.Version.ShouldBe(new VersionToken("2"));
        next.UpdatedAt.ShouldBe(at);
        next.Id.ShouldBe(record.Id);
        next.Content.ShouldBe(record.Content);
        next.CreatedAt.ShouldBe(record.CreatedAt);
    }

    [Fact]
    public void WithState_WhenInstantPrecedesTheCurrentUpdate_KeepsTheCurrentUpdateInstant()
    {
        var record = MemoryTestData.Record(MemoryTestData.NewOwner());

        record.WithState(MemoryLifecycleState.Expired, new VersionToken("2"), record.UpdatedAt.AddMinutes(-5)).UpdatedAt.ShouldBe(record.UpdatedAt);
    }

    [Fact]
    public void WithState_WhenContentIsSupplied_ReplacesTheBody()
    {
        var record = MemoryTestData.Record(MemoryTestData.NewOwner());

        record.WithState(MemoryLifecycleState.Deleted, new VersionToken("2"), MemoryTestData.Now, MemoryContent.Purged).Content.ShouldBe(MemoryContent.Purged);
    }
}
