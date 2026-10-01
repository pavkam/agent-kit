// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>Builds memory-store requests whose grants bind exactly the operation each request performs.</summary>
/// <param name="grants">The grant harness shared with the store under test.</param>
/// <param name="audience">The store audience the grants must name.</param>
public sealed class MemoryStoreRequestFactory(TestGoalGrants grants, ComponentId audience)
{
    /// <summary>Builds an exactly authorized write request.</summary>
    /// <param name="record">The record to create.</param>
    /// <param name="authorization">The caller's authorization.</param>
    /// <param name="key">The replay key.</param>
    /// <returns>The request.</returns>
    public MemoryWriteRequest Write(DurableMemoryRecord record, SecurityAuthorizationContext authorization, string key = "write-1") => new(
        record,
        new IdempotencyKey(key),
        grants.Issue(audience, authorization, SecurityOperationKind.StateMutation, SecurityEffect.Create, [MemorySecurityBinding.Resource(record.Id)],
            MemorySecurityBinding.WriteFingerprint(record, new IdempotencyKey(key))));

    /// <summary>Builds an exactly authorized read request.</summary>
    /// <param name="id">The memory to read.</param>
    /// <param name="authorization">The caller's authorization.</param>
    /// <returns>The request.</returns>
    public MemoryReadRequest Read(MemoryId id, SecurityAuthorizationContext authorization) => new(
        id,
        grants.Issue(audience, authorization, SecurityOperationKind.StateRead, SecurityEffect.Observe, [MemorySecurityBinding.Resource(id)],
            MemorySecurityBinding.ReadFingerprint(id)));

    /// <summary>Builds an exactly authorized page read.</summary>
    /// <param name="authorization">The caller's authorization.</param>
    /// <param name="namespace">The namespace filter, or null.</param>
    /// <param name="states">The states, or default for active only.</param>
    /// <param name="terms">The keyword terms, or default for none.</param>
    /// <param name="afterSequence">The exclusive cursor.</param>
    /// <param name="limit">The page size.</param>
    /// <returns>The request.</returns>
    public MemoryListRequest List(
        SecurityAuthorizationContext authorization,
        MemoryNamespace? @namespace = null,
        ImmutableArray<MemoryLifecycleState> states = default,
        ImmutableArray<string> terms = default,
        long afterSequence = 0,
        int limit = 50) => new(
            @namespace,
            states,
            terms,
            afterSequence,
            limit,
            grants.Issue(audience, authorization, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                [MemorySecurityBinding.CollectionResource(authorization.Scope.AgentId)],
                MemorySecurityBinding.ListFingerprint(@namespace, states, terms, afterSequence, limit)));

    /// <summary>Builds an exactly authorized transition request.</summary>
    /// <param name="id">The record to transition.</param>
    /// <param name="to">The target state.</param>
    /// <param name="expectedVersion">The expected version text.</param>
    /// <param name="authorization">The caller's authorization.</param>
    /// <param name="replacement">The replacement for a correction, or null.</param>
    /// <param name="key">The replay key.</param>
    /// <param name="at">The transition instant, or the shared test instant.</param>
    /// <returns>The request.</returns>
    public MemoryTransitionRequest Transition(
        MemoryId id,
        MemoryLifecycleState to,
        string expectedVersion,
        SecurityAuthorizationContext authorization,
        DurableMemoryRecord? replacement = null,
        string key = "transition-1",
        DateTimeOffset? at = null)
    {
        var instant = at ?? MemoryTestData.Now.AddMinutes(1);
        return new(
            id,
            to,
            new VersionToken(expectedVersion),
            replacement,
            new IdempotencyKey(key),
            instant,
            grants.Issue(audience, authorization, SecurityOperationKind.StateMutation, SecurityEffect.Mutate, [MemorySecurityBinding.Resource(id)],
                MemorySecurityBinding.TransitionFingerprint(id, to, new VersionToken(expectedVersion), replacement, new IdempotencyKey(key), instant)));
    }

    /// <summary>Builds an exactly authorized delete request.</summary>
    /// <param name="id">The record to delete.</param>
    /// <param name="mode">How far the deletion goes.</param>
    /// <param name="authorization">The caller's authorization.</param>
    /// <param name="expectedVersion">The expected live version text, or null.</param>
    /// <param name="key">The replay key.</param>
    /// <returns>The request.</returns>
    public MemoryDeleteRequest Delete(
        MemoryId id,
        MemoryDeleteMode mode,
        SecurityAuthorizationContext authorization,
        string? expectedVersion = null,
        string key = "delete-1")
    {
        var instant = MemoryTestData.Now.AddMinutes(2);
        VersionToken? version = expectedVersion is null ? null : new VersionToken(expectedVersion);
        return new(
            id,
            version,
            mode,
            new IdempotencyKey(key),
            instant,
            grants.Issue(audience, authorization, SecurityOperationKind.StateMutation, SecurityEffect.Delete, [MemorySecurityBinding.Resource(id)],
                MemorySecurityBinding.DeleteFingerprint(id, version, mode, new IdempotencyKey(key), instant)));
    }
}
