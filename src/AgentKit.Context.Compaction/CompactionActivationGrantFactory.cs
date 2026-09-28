// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>Builds invocation-only activation grants from captured compaction authorization.</summary>
internal static class CompactionActivationGrantFactory
{
    internal static SecurityGrant Create(CompactionOperationContext context, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(timeProvider);
        var authorization = context.Authorization;
        var now = timeProvider.GetUtcNow();
        return new SecurityGrant(
            new GrantId(Guid.NewGuid()),
            new SecurityRequestId(Guid.NewGuid()),
            authorization.Scope,
            context.Identity,
            authorization,
            new ComponentId("agentkit.context.compaction"),
            SecurityOperationKind.StateMutation,
            SecurityEffect.Append,
            [new ProtectedResource(ProtectedResourceKind.ApplicationState, context.SessionId.ToString())],
            new InputFingerprint($"compaction:{context.CompactionId}"),
            authorization.PolicySnapshot.Version,
            new SecurityRevocationVersion(1),
            notBefore: now,
            expiresAt: now.AddMinutes(5),
            allowedUses: 1);
    }
}
