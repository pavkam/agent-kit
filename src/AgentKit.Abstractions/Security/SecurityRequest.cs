// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes one fully normalized protected operation before policy evaluation or effects.</summary>
public sealed record SecurityRequest
{
    /// <summary>Initializes a normalized security request.</summary>
    /// <param name="id">The stable request identity.</param>
    /// <param name="scope">The exact authorization scope.</param>
    /// <param name="toolCallId">The causing tool call, when applicable.</param>
    /// <param name="identity">The authenticated execution identity.</param>
    /// <param name="audience">The component that will enforce and perform the effect.</param>
    /// <param name="kind">The protected operation kind.</param>
    /// <param name="effect">The requested effect.</param>
    /// <param name="resources">The ordered canonical resources.</param>
    /// <param name="inputFingerprint">The normalized input fingerprint.</param>
    /// <param name="deadline">The exclusive deadline after which the request must not be granted.</param>
    /// <param name="requestedUses">The positive maximum number of effect consumptions requested.</param>
    /// <exception cref="ArgumentNullException"><paramref name="scope"/> or <paramref name="identity"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="resources"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum is undefined or <paramref name="requestedUses"/> is not positive.</exception>
    public SecurityRequest(
        SecurityRequestId id,
        SecurityAuthorizationScope scope,
        ToolCallId? toolCallId,
        ExecutionIdentity identity,
        ComponentId audience,
        SecurityOperationKind kind,
        SecurityEffect effect,
        ImmutableArray<ProtectedResource> resources,
        InputFingerprint inputFingerprint,
        DateTimeOffset deadline,
        int requestedUses = 1)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentOutOfRangeException.ThrowIfUndefined(effect);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedUses);
        ArgumentException.ThrowIfDefaultOrEmpty(resources);

        Id = id;
        Scope = scope;
        ToolCallId = toolCallId;
        Identity = identity;
        Audience = audience;
        Kind = kind;
        Effect = effect;
        Resources = resources;
        InputFingerprint = inputFingerprint;
        Deadline = deadline;
        RequestedUses = requestedUses;
    }

    /// <summary>Gets the request identity.</summary>
    public SecurityRequestId Id { get; init; }
    /// <summary>Gets the exact authorization scope.</summary>
    public SecurityAuthorizationScope Scope { get; init; }
    /// <summary>Gets the causing tool-call identity, when applicable.</summary>
    public ToolCallId? ToolCallId { get; init; }
    /// <summary>Gets the authenticated execution identity.</summary>
    public ExecutionIdentity Identity { get; init; }
    /// <summary>Gets the effecting component audience.</summary>
    public ComponentId Audience { get; init; }
    /// <summary>Gets the operation kind.</summary>
    public SecurityOperationKind Kind { get; init; }
    /// <summary>Gets the requested effect.</summary>
    public SecurityEffect Effect { get; init; }
    /// <summary>Gets the ordered canonical resources.</summary>
    public ImmutableArray<ProtectedResource> Resources { get; init; }
    /// <summary>Gets the normalized input fingerprint.</summary>
    public InputFingerprint InputFingerprint { get; init; }
    /// <summary>Gets the exclusive decision deadline.</summary>
    public DateTimeOffset Deadline { get; init; }
    /// <summary>Gets the requested maximum use count.</summary>
    public int RequestedUses { get; init; }
}
