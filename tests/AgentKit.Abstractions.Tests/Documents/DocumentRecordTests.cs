// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Documents;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="DocumentRecord"/> constraints.</summary>
public sealed class DocumentRecordTests
{
    [Fact]
    public void Constructor_WhenValid_PreservesEveryValue()
    {
        var owner = MemoryTestData.NewOwner();

        var record = MemoryTestData.Document(owner, "v7", "sha256:bb", shared: true);

        record.AgentId.ShouldBe(owner.AgentId);
        record.TenantId.ShouldBe(owner.Identity.TenantId);
        record.Version.ShouldBe(new DocumentVersion("v7"));
        record.ContentHash.ShouldBe(new ContentHash("sha256:bb"));
        record.Visibility.SharedWithTenant.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WhenVisibilityBelongsToAnotherTenant_ThrowsArgumentException()
    {
        var record = MemoryTestData.Document(MemoryTestData.NewOwner());

        Should.Throw<ArgumentException>(() => new DocumentRecord(
            record.Id, record.AgentId, record.SourceSessionId, record.SourceRunId, new TenantId("another"), record.Visibility,
            record.Version, record.ContentHash, record.Metadata, record.Classification, record.Provenance, record.Retention)).ParamName.ShouldBe("visibility");
    }

    [Fact]
    public void Constructor_WhenIdIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => MemoryTestData.Document(MemoryTestData.NewOwner(), id: default(DocumentId))).ParamName.ShouldBe("id");

    [Fact]
    public void Constructor_WhenClassificationIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var record = MemoryTestData.Document(MemoryTestData.NewOwner());

        Should.Throw<ArgumentOutOfRangeException>(() => new DocumentRecord(
            record.Id, record.AgentId, record.SourceSessionId, record.SourceRunId, record.TenantId, record.Visibility,
            record.Version, record.ContentHash, record.Metadata, (DataClassification) 99, record.Provenance, record.Retention)).ParamName.ShouldBe("classification");
    }

    [Fact]
    public void Constructor_WhenAReferenceIsNull_ThrowsArgumentNullException()
    {
        var record = MemoryTestData.Document(MemoryTestData.NewOwner());

        Should.Throw<ArgumentNullException>(() => new DocumentRecord(
            record.Id, record.AgentId, record.SourceSessionId, record.SourceRunId, record.TenantId, record.Visibility,
            record.Version, record.ContentHash, null!, record.Classification, record.Provenance, record.Retention)).ParamName.ShouldBe("metadata");
    }
}
