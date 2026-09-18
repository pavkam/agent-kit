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
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience.Value, nameof(audience));
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentOutOfRangeException.ThrowIfUndefined(effect);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedUses);
        ArgumentException.ThrowIfDefaultOrEmpty(resources);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputFingerprint.Value, nameof(inputFingerprint));

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

    /// <summary>Initializes a protected request bound to one complete captured authorization context.</summary>
    /// <param name="id">The stable request identity.</param><param name="scope">The exact authorization scope.</param><param name="toolCallId">The causing tool call, when applicable.</param><param name="identity">The authenticated execution identity.</param><param name="authorization">The complete captured profile, policy-snapshot, authority, configuration, scope, and identity evidence the selected authority must evaluate.</param><param name="audience">The component that will enforce and perform the effect.</param><param name="kind">The protected operation kind.</param><param name="effect">The requested effect.</param><param name="resources">The ordered canonical resources.</param><param name="inputFingerprint">The normalized input fingerprint.</param><param name="deadline">The exclusive decision deadline.</param><param name="requestedUses">The positive maximum consumption count.</param>
    /// <exception cref="ArgumentNullException"><paramref name="authorization"/> is null.</exception><exception cref="ArgumentException">Its scope or identity differs from the request.</exception>
    public SecurityRequest(SecurityRequestId id, SecurityAuthorizationScope scope, ToolCallId? toolCallId,
        ExecutionIdentity identity, SecurityAuthorizationContext authorization, ComponentId audience,
        SecurityOperationKind kind, SecurityEffect effect, ImmutableArray<ProtectedResource> resources,
        InputFingerprint inputFingerprint, DateTimeOffset deadline, int requestedUses = 1)
        : this(id, scope, toolCallId, identity, audience, kind, effect, resources, inputFingerprint, deadline, requestedUses)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentException.ThrowIfNotEqual(authorization.Scope, scope, nameof(authorization));
        ArgumentException.ThrowIfNotEqual(authorization.Identity, identity, nameof(authorization));
        Authorization = authorization;
    }

    /// <summary>Gets the request identity.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The initialized value is <see langword="default"/>.</exception>
    public SecurityRequestId Id
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, nameof(Id));
            field = value;
        }
    }
    /// <summary>Gets or initializes the exact authorization scope.</summary>
    /// <value>The non-null scope, which must equal the captured authorization scope when <see cref="Authorization"/> is present.</value>
    /// <exception cref="ArgumentNullException">The initialized value is null.</exception>
    /// <exception cref="ArgumentException">A record copy assigns a scope different from the captured authorization scope.</exception>
    public SecurityAuthorizationScope Scope
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Scope));
            if (Authorization is not null)
            {
                ArgumentException.ThrowIfNotEqual(Authorization.Scope, value, nameof(Scope));
            }

            field = value;
        }
    }
    /// <summary>Gets the causing tool-call identity, when applicable.</summary>
    public ToolCallId? ToolCallId { get; init; }
    /// <summary>Gets or initializes the authenticated execution identity.</summary>
    /// <value>The non-null identity, which must equal the captured authorization identity when <see cref="Authorization"/> is present.</value>
    /// <exception cref="ArgumentNullException">The initialized value is null.</exception>
    /// <exception cref="ArgumentException">A record copy assigns an identity different from the captured authorization identity.</exception>
    public ExecutionIdentity Identity
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Identity));
            if (Authorization is not null)
            {
                ArgumentException.ThrowIfNotEqual(Authorization.Identity, value, nameof(Identity));
            }

            field = value;
        }
    }
    /// <summary>Gets complete captured authorization evidence when the caller requires snapshot-bound evaluation.</summary><value>The immutable captured selection, or null only for the legacy unpinned request path.</value>
    public SecurityAuthorizationContext? Authorization { get; }
    /// <summary>Gets the effecting component audience.</summary>
    /// <exception cref="ArgumentException">The initialized value's <see cref="ComponentId.Value"/> is blank.</exception>
    public ComponentId Audience
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value.Value, nameof(Audience));
            field = value;
        }
    }
    /// <summary>Gets the operation kind.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The initialized value is undefined.</exception>
    public SecurityOperationKind Kind
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(value, nameof(Kind));
            field = value;
        }
    }
    /// <summary>Gets the requested effect.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The initialized value is undefined.</exception>
    public SecurityEffect Effect
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(value, nameof(Effect));
            field = value;
        }
    }
    /// <summary>Gets the ordered canonical resources.</summary>
    /// <exception cref="ArgumentException">The initialized array is default or empty.</exception>
    public ImmutableArray<ProtectedResource> Resources
    {
        get;
        init
        {
            ArgumentException.ThrowIfDefaultOrEmpty(value, nameof(Resources));
            field = value;
        }
    }
    /// <summary>Gets the normalized input fingerprint.</summary>
    /// <exception cref="ArgumentException">The initialized value's <see cref="InputFingerprint.Value"/> is blank.</exception>
    public InputFingerprint InputFingerprint
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value.Value, nameof(InputFingerprint));
            field = value;
        }
    }
    /// <summary>Gets the exclusive decision deadline.</summary>
    public DateTimeOffset Deadline { get; init; }
    /// <summary>Gets the requested maximum use count.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The initialized value is not positive.</exception>
    public int RequestedUses
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, nameof(RequestedUses));
            field = value;
        }
    }

    /// <summary>Compares every member structurally, including the ordered <see cref="Resources"/> sequence.</summary>
    /// <param name="other">The request to compare with.</param>
    /// <returns><see langword="true"/> when both requests describe the same protected operation.</returns>
    public bool Equals(SecurityRequest? other) =>
        other is not null
        && Id == other.Id
        && Scope == other.Scope
        && ToolCallId == other.ToolCallId
        && Identity == other.Identity
        && Authorization == other.Authorization
        && Audience == other.Audience
        && Kind == other.Kind
        && Effect == other.Effect
        && Resources.SequenceEqual(other.Resources)
        && InputFingerprint == other.InputFingerprint
        && Deadline == other.Deadline
        && RequestedUses == other.RequestedUses;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(Scope);
        hash.Add(ToolCallId);
        hash.Add(Identity);
        hash.Add(Authorization);
        hash.Add(Audience);
        hash.Add(Kind);
        hash.Add(Effect);
        foreach (var resource in Resources)
        {
            hash.Add(resource);
        }

        hash.Add(InputFingerprint);
        hash.Add(Deadline);
        hash.Add(RequestedUses);
        return hash.ToHashCode();
    }
}
