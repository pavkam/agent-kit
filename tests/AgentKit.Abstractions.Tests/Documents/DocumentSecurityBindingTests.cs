// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Documents;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="DocumentSecurityBinding"/> resources and fingerprints bind exactly one operation.</summary>
public sealed class DocumentSecurityBindingTests
{
    private static readonly IdempotencyKey _key = new("k");

    [Fact]
    public void Resource_WhenIdIsSupplied_NamesOneApplicationStateResource()
    {
        var id = new DocumentId(Guid.NewGuid());

        var resource = DocumentSecurityBinding.Resource(id);

        resource.Kind.ShouldBe(ProtectedResourceKind.ApplicationState);
        resource.Identifier.ShouldBe($"document:{id}");
        Should.Throw<ArgumentOutOfRangeException>(() => DocumentSecurityBinding.Resource(default)).ParamName.ShouldBe("id");
    }

    [Fact]
    public void WriteFingerprint_WhenChunkSetOrFlagsChange_ChangesTheFingerprint()
    {
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Document(owner);
        var chunks = MemoryTestData.Chunks(record, 2);
        var baseline = DocumentSecurityBinding.WriteFingerprint(record, chunks, false, _key, MemoryTestData.Now);

        baseline.ShouldBe(DocumentSecurityBinding.WriteFingerprint(record, chunks, false, _key, MemoryTestData.Now));
        baseline.ShouldNotBe(DocumentSecurityBinding.WriteFingerprint(record, chunks, true, _key, MemoryTestData.Now));
        baseline.ShouldNotBe(DocumentSecurityBinding.WriteFingerprint(record, MemoryTestData.Chunks(record, 3), false, _key, MemoryTestData.Now));
        baseline.ShouldNotBe(DocumentSecurityBinding.WriteFingerprint(MemoryTestData.Document(owner), MemoryTestData.Chunks(MemoryTestData.Document(owner)), false, _key, MemoryTestData.Now));
    }

    [Fact]
    public void WriteFingerprint_WhenArgumentsAreInvalid_Throws()
    {
        var record = MemoryTestData.Document(MemoryTestData.NewOwner());

        Should.Throw<ArgumentNullException>(() => DocumentSecurityBinding.WriteFingerprint(null!, MemoryTestData.Chunks(record), false, _key, MemoryTestData.Now)).ParamName.ShouldBe("record");
        Should.Throw<ArgumentException>(() => DocumentSecurityBinding.WriteFingerprint(record, default, false, _key, MemoryTestData.Now)).ParamName.ShouldBe("chunks");
        Should.Throw<ArgumentNullException>(() => DocumentSecurityBinding.WriteFingerprint(record, MemoryTestData.Chunks(record), false, default, MemoryTestData.Now)).ParamName.ShouldBe("idempotencyKey");
    }

    [Fact]
    public void ActivateFingerprint_WhenPartsDiffer_ChangesTheFingerprint()
    {
        var id = new DocumentId(Guid.NewGuid());
        var baseline = DocumentSecurityBinding.ActivateFingerprint(id, new DocumentVersion("v2"), new DocumentVersion("v1"), _key, MemoryTestData.Now);

        baseline.ShouldBe(DocumentSecurityBinding.ActivateFingerprint(id, new DocumentVersion("v2"), new DocumentVersion("v1"), _key, MemoryTestData.Now));
        baseline.ShouldNotBe(DocumentSecurityBinding.ActivateFingerprint(id, new DocumentVersion("v3"), new DocumentVersion("v1"), _key, MemoryTestData.Now));
        baseline.ShouldNotBe(DocumentSecurityBinding.ActivateFingerprint(id, new DocumentVersion("v2"), null, _key, MemoryTestData.Now));
    }

    [Fact]
    public void ActivateFingerprint_WhenArgumentsAreInvalid_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => DocumentSecurityBinding.ActivateFingerprint(default, new DocumentVersion("v"), null, _key, MemoryTestData.Now)).ParamName.ShouldBe("id");
        Should.Throw<ArgumentNullException>(() => DocumentSecurityBinding.ActivateFingerprint(new DocumentId(Guid.NewGuid()), default, null, _key, MemoryTestData.Now)).ParamName.ShouldBe("version");
        Should.Throw<ArgumentNullException>(() => DocumentSecurityBinding.ActivateFingerprint(new DocumentId(Guid.NewGuid()), new DocumentVersion("v"), null, default, MemoryTestData.Now)).ParamName.ShouldBe("idempotencyKey");
    }

    [Fact]
    public void ReadFingerprint_WhenPartsDiffer_ChangesTheFingerprint()
    {
        var id = new DocumentId(Guid.NewGuid());

        DocumentSecurityBinding.ReadFingerprint(id, null, false).ShouldNotBe(DocumentSecurityBinding.ReadFingerprint(id, null, true));
        DocumentSecurityBinding.ReadFingerprint(id, null, false).ShouldNotBe(DocumentSecurityBinding.ReadFingerprint(id, new DocumentVersion("v1"), false));
        Should.Throw<ArgumentOutOfRangeException>(() => DocumentSecurityBinding.ReadFingerprint(default, null, false)).ParamName.ShouldBe("id");
    }

    [Fact]
    public void DeleteFingerprint_WhenModeDiffers_ChangesTheFingerprint()
    {
        var id = new DocumentId(Guid.NewGuid());

        DocumentSecurityBinding.DeleteFingerprint(id, DocumentDeleteMode.Tombstone, _key, MemoryTestData.Now)
            .ShouldNotBe(DocumentSecurityBinding.DeleteFingerprint(id, DocumentDeleteMode.Purge, _key, MemoryTestData.Now));
        Should.Throw<ArgumentOutOfRangeException>(() => DocumentSecurityBinding.DeleteFingerprint(default, DocumentDeleteMode.Tombstone, _key, MemoryTestData.Now)).ParamName.ShouldBe("id");
        Should.Throw<ArgumentNullException>(() => DocumentSecurityBinding.DeleteFingerprint(id, DocumentDeleteMode.Tombstone, default, MemoryTestData.Now)).ParamName.ShouldBe("idempotencyKey");
    }
}
