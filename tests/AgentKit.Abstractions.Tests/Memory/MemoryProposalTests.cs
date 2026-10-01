// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="MemoryProposal"/> constraints and defaults.</summary>
public sealed class MemoryProposalTests
{
    [Fact]
    public void Constructor_WhenOmittedOptionalValues_TargetsTheDefaultNamespaceAndStaysPrivate()
    {
        var proposal = MemoryTestData.Proposal(MemoryTestData.NewOwner());

        proposal.Namespace.ShouldBe(MemoryProposal.DefaultNamespace);
        proposal.ShareWithTenant.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WhenIdIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Create(id: default(MemoryId))).ParamName.ShouldBe("id");

    [Fact]
    public void Constructor_WhenContextIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => Create(nullContext: true)).ParamName.ShouldBe("context");

    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Create(kind: (MemoryKind) 99)).ParamName.ShouldBe("kind");

    [Fact]
    public void Constructor_WhenClassificationIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Create(classification: (DataClassification) 99)).ParamName.ShouldBe("classification");

    [Fact]
    public void Constructor_WhenContentProvenanceRetentionOrExtensionsAreNull_ThrowsArgumentNullException()
    {
        var owner = MemoryTestData.NewOwner();
        var id = new MemoryId(Guid.NewGuid());
        var provenance = new Provenance("user");

        Should.Throw<ArgumentNullException>(() => new MemoryProposal(id, owner.Context, MemoryKind.Summary, null!, DataClassification.Public, provenance, new RetentionPolicy(), MemoryTestData.Now, ExtensionData.Empty)).ParamName.ShouldBe("content");
        Should.Throw<ArgumentNullException>(() => new MemoryProposal(id, owner.Context, MemoryKind.Summary, new MemoryContent("x"), DataClassification.Public, null!, new RetentionPolicy(), MemoryTestData.Now, ExtensionData.Empty)).ParamName.ShouldBe("provenance");
        Should.Throw<ArgumentNullException>(() => new MemoryProposal(id, owner.Context, MemoryKind.Summary, new MemoryContent("x"), DataClassification.Public, provenance, null!, MemoryTestData.Now, ExtensionData.Empty)).ParamName.ShouldBe("retention");
        Should.Throw<ArgumentNullException>(() => new MemoryProposal(id, owner.Context, MemoryKind.Summary, new MemoryContent("x"), DataClassification.Public, provenance, new RetentionPolicy(), MemoryTestData.Now, null!)).ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void Namespace_WhenInitializedToDefault_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => MemoryTestData.Proposal(MemoryTestData.NewOwner()) with { Namespace = default }).ParamName.ShouldBe("Namespace");

    [Fact]
    public void Namespace_WhenInitializedToAName_PreservesIt() =>
        (MemoryTestData.Proposal(MemoryTestData.NewOwner()) with { Namespace = new MemoryNamespace("notes") }).Namespace.ShouldBe(new MemoryNamespace("notes"));

    private static MemoryProposal Create(MemoryId? id = null, bool nullContext = false, MemoryKind kind = MemoryKind.Summary, DataClassification classification = DataClassification.Public)
    {
        var owner = MemoryTestData.NewOwner();
        return new(
            id ?? new MemoryId(Guid.NewGuid()), nullContext ? null! : owner.Context, kind, new MemoryContent("x"), classification,
            new Provenance("user"), new RetentionPolicy(), MemoryTestData.Now, ExtensionData.Empty);
    }
}
