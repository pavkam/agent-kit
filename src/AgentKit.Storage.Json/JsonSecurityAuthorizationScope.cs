// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json;

/// <summary>Portable JSON mirror of <see cref="SecurityAuthorizationScope"/>, binding persisted protected work to one agent, optional session, and causal operation.</summary>
/// <remarks>
/// <para>
/// The scope is the binding a grant, an enforcement request, and an audit record all have to agree on, so it must survive a
/// storage round trip exactly. <see cref="AgentKit.AgentId"/> and <see cref="AgentKit.SessionId"/> are unwrapped to their
/// underlying <see cref="Guid"/> values and rebuilt through their validating constructors, and the polymorphic correlation is
/// delegated to <see cref="JsonOperationCorrelation"/>, which owns its own discriminator.
/// </para>
/// <para>
/// A null <see cref="SessionId"/> means the operation truthfully belongs to no session; it is preserved as null rather than
/// normalized to an empty GUID, because a fabricated session binding would widen the scope a later enforcement check compares
/// against.
/// </para>
/// </remarks>
/// <param name="AgentId">The raw value of the non-empty agent identity performing the protected operation.</param>
/// <param name="SessionId">The raw value of the session the operation belongs to, or <see langword="null"/> when it truthfully belongs to none.</param>
/// <param name="Correlation">The non-null causal correlation describing when and why the operation occurs.</param>
public sealed record JsonSecurityAuthorizationScope(
    Guid AgentId,
    Guid? SessionId,
    JsonOperationCorrelation Correlation)
{
    /// <summary>Projects one domain authorization scope into its portable JSON representation.</summary>
    /// <param name="value">The non-null scope to project.</param>
    /// <returns>A document carrying the unwrapped agent and optional session identities plus the projected correlation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static JsonSecurityAuthorizationScope FromDomain(SecurityAuthorizationScope value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new JsonSecurityAuthorizationScope(
            value.AgentId.Value,
            value.SessionId?.Value,
            JsonOperationCorrelation.FromDomain(value.Correlation));
    }

    /// <summary>Reconstructs the exact domain scope this document was projected from.</summary>
    /// <returns>A scope equal to the projected original, with a null session preserved as null.</returns>
    /// <remarks>
    /// Every identity is rebuilt through its own validating constructor and the scope through
    /// <see cref="SecurityAuthorizationScope(AgentId, SessionId?, OperationCorrelation)"/>, so persisted evidence is
    /// revalidated on read instead of being trusted because it was written by this process earlier.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><see cref="Correlation"/> is null, which a well-formed document never is.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The persisted agent identity, session identity, or a correlation identity is empty, or the correlation kind is undefined.</exception>
    public SecurityAuthorizationScope ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Correlation);
        return new SecurityAuthorizationScope(
            new AgentId(AgentId),
            SessionId is { } sessionId ? new SessionId(sessionId) : null,
            Correlation.ToDomain());
    }
}
