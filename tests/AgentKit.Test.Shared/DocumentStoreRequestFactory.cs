// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>Builds document-store requests whose grants bind exactly the operation each request performs.</summary>
/// <param name="grants">The grant harness shared with the store under test.</param>
/// <param name="audience">The store audience the grants must name.</param>
public sealed class DocumentStoreRequestFactory(TestGoalGrants grants, ComponentId audience)
{
    /// <summary>Builds an exactly authorized publication request.</summary>
    /// <param name="record">The version's record.</param>
    /// <param name="chunks">The complete chunk set.</param>
    /// <param name="activate">Whether the pointer switches atomically.</param>
    /// <param name="authorization">The caller's authorization.</param>
    /// <param name="key">The replay key.</param>
    /// <returns>The request.</returns>
    public DocumentWriteRequest Write(DocumentRecord record, ImmutableArray<DocumentChunk> chunks, bool activate, SecurityAuthorizationContext authorization, string key = "write-1")
    {
        var instant = MemoryTestData.Now.AddMinutes(1);
        return new(
            record,
            chunks,
            activate,
            new IdempotencyKey(key),
            instant,
            grants.Issue(audience, authorization, SecurityOperationKind.StateMutation, SecurityEffect.CreateOrReplace, [DocumentSecurityBinding.Resource(record.Id)],
                DocumentSecurityBinding.WriteFingerprint(record, chunks, activate, new IdempotencyKey(key), instant)));
    }

    /// <summary>Builds an exactly authorized activation request.</summary>
    /// <param name="id">The document.</param>
    /// <param name="version">The version to activate.</param>
    /// <param name="expectedActive">The version that must be active, or null.</param>
    /// <param name="authorization">The caller's authorization.</param>
    /// <param name="key">The replay key.</param>
    /// <returns>The request.</returns>
    public DocumentActivateRequest Activate(DocumentId id, string version, string? expectedActive, SecurityAuthorizationContext authorization, string key = "activate-1")
    {
        var instant = MemoryTestData.Now.AddMinutes(2);
        DocumentVersion? expected = expectedActive is null ? null : new DocumentVersion(expectedActive);
        return new(
            id,
            new DocumentVersion(version),
            expected,
            new IdempotencyKey(key),
            instant,
            grants.Issue(audience, authorization, SecurityOperationKind.StateMutation, SecurityEffect.Mutate, [DocumentSecurityBinding.Resource(id)],
                DocumentSecurityBinding.ActivateFingerprint(id, new DocumentVersion(version), expected, new IdempotencyKey(key), instant)));
    }

    /// <summary>Builds an exactly authorized read request.</summary>
    /// <param name="id">The document.</param>
    /// <param name="version">The exact version text, or null for the active version.</param>
    /// <param name="includeChunks">Whether chunks are returned.</param>
    /// <param name="authorization">The caller's authorization.</param>
    /// <returns>The request.</returns>
    public DocumentReadRequest Read(DocumentId id, string? version, bool includeChunks, SecurityAuthorizationContext authorization)
    {
        DocumentVersion? requested = version is null ? null : new DocumentVersion(version);
        return new(
            id,
            requested,
            includeChunks,
            grants.Issue(audience, authorization, SecurityOperationKind.StateRead, SecurityEffect.Observe, [DocumentSecurityBinding.Resource(id)],
                DocumentSecurityBinding.ReadFingerprint(id, requested, includeChunks)));
    }

    /// <summary>Builds an exactly authorized delete request.</summary>
    /// <param name="id">The document.</param>
    /// <param name="mode">How far the deletion goes.</param>
    /// <param name="authorization">The caller's authorization.</param>
    /// <param name="key">The replay key.</param>
    /// <returns>The request.</returns>
    public DocumentDeleteRequest Delete(DocumentId id, DocumentDeleteMode mode, SecurityAuthorizationContext authorization, string key = "delete-1")
    {
        var instant = MemoryTestData.Now.AddMinutes(3);
        return new(
            id,
            mode,
            new IdempotencyKey(key),
            instant,
            grants.Issue(audience, authorization, SecurityOperationKind.StateMutation, SecurityEffect.Delete, [DocumentSecurityBinding.Resource(id)],
                DocumentSecurityBinding.DeleteFingerprint(id, mode, new IdempotencyKey(key), instant)));
    }
}
