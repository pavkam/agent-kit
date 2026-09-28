// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Tests;

/// <summary>Builds deterministic single-use grants for components that only carry authority, never verify it.</summary>
/// <remarks>
/// Grant verification belongs to the grant store and to the concrete journal adapter that consumes the use. These
/// grants exist so the fenced decorator and the coordinator can be tested for what they do with authority they were
/// handed, without reimplementing the enforcement path under test elsewhere.
/// </remarks>
internal static class DurabilityGrantFactory
{
    /// <summary>Creates one single-use grant bound to the supplied capture.</summary>
    /// <param name="authorization">The non-null capture the grant is issued under.</param>
    /// <param name="audience">The consuming component identity, or null for the test journal audience.</param>
    /// <param name="kind">The protected operation kind the grant permits.</param>
    /// <param name="effect">The protected effect the grant permits.</param>
    /// <returns>A grant valid for exactly one use.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="authorization"/> is null.</exception>
    internal static SecurityGrant Create(
        SecurityAuthorizationContext authorization,
        ComponentId? audience = null,
        SecurityOperationKind kind = SecurityOperationKind.StateMutation,
        SecurityEffect effect = SecurityEffect.Create)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        return new SecurityGrant(
            new GrantId(Guid.Parse("c0000000-0000-0000-0000-000000000001")),
            new SecurityRequestId(Guid.Parse("c1000000-0000-0000-0000-000000000001")),
            authorization.Scope,
            authorization.Identity,
            authorization,
            audience ?? new ComponentId("test.durable.journal"),
            kind,
            effect,
            [DurableJournalSecurityBinding.Resource(
                new DurableJournalKey("journal"), DurableJournalTestData.Address())],
            DurableJournalSecurityBinding.Fingerprint(DurableJournalTestData.Address()),
            authorization.PolicySnapshot.Version,
            new SecurityRevocationVersion(1),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddYears(100),
            allowedUses: 1);
    }
}
