// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json;

/// <summary>Portable JSON mirror of <see cref="SecurityRequest"/>, one fully normalized protected operation as it stood before policy evaluation or any effect.</summary>
/// <remarks>
/// <para>
/// Persisting the request is what lets an audit trail explain why a grant exists and lets a deferred approval resolve against
/// the exact operation that was asked for. The document therefore stores the normalized request as issued, including the
/// requested use count and the exclusive decision deadline, and never the decision that followed it.
/// </para>
/// <para>
/// <see cref="ToolCallId"/> is optional because not every protected operation originates from a tool call; a null value is
/// preserved rather than normalized to an empty GUID, so a request that no tool caused never acquires a fabricated tool
/// correlation. <see cref="Authorization"/> is required: every request names the complete captured context the selected
/// authority must evaluate, so a document without one is corrupt and is rejected rather than reconstructed.
/// </para>
/// </remarks>
/// <param name="Id">The raw value of the non-empty stable request identity.</param>
/// <param name="Scope">The non-null exact authorization scope of the requested operation.</param>
/// <param name="ToolCallId">The raw value of the causing tool call, or <see langword="null"/> when no tool call caused the request.</param>
/// <param name="Identity">The non-null authenticated execution identity the request was issued for.</param>
/// <param name="Authorization">The non-null complete captured authorization evidence the selected authority must evaluate.</param>
/// <param name="Audience">The non-blank canonical component identifier of the component that will enforce and perform the effect.</param>
/// <param name="Kind">The protected operation kind.</param>
/// <param name="Effect">The requested material effect.</param>
/// <param name="Resources">The ordered, non-default, non-empty canonical resources the request names.</param>
/// <param name="InputFingerprint">The non-blank normalized input fingerprint.</param>
/// <param name="Deadline">The exclusive deadline after which the request must not be granted.</param>
/// <param name="RequestedUses">The positive maximum number of effect consumptions requested.</param>
public sealed record JsonSecurityRequest(
    Guid Id,
    JsonSecurityAuthorizationScope Scope,
    Guid? ToolCallId,
    JsonExecutionIdentity Identity,
    JsonSecurityAuthorizationContext Authorization,
    string Audience,
    SecurityOperationKind Kind,
    SecurityEffect Effect,
    ImmutableArray<JsonProtectedResource> Resources,
    string InputFingerprint,
    DateTimeOffset Deadline,
    int RequestedUses)
{
    /// <summary>Projects one domain security request into its portable JSON representation.</summary>
    /// <param name="value">The non-null normalized request to project.</param>
    /// <returns>A document carrying every unwrapped identity and bound, a never-default ordered resource array, and a null tool correlation exactly when no tool call caused the request.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static JsonSecurityRequest FromDomain(SecurityRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ImmutableArray<JsonProtectedResource> resources =
            value.Resources.IsDefault ? [] : [.. value.Resources.Select(JsonProtectedResource.FromDomain)];
        return new JsonSecurityRequest(
            value.Id.Value,
            JsonSecurityAuthorizationScope.FromDomain(value.Scope),
            value.ToolCallId?.Value,
            JsonExecutionIdentity.FromDomain(value.Identity),
            JsonSecurityAuthorizationContext.FromDomain(value.Authorization),
            value.Audience.Value,
            value.Kind,
            value.Effect,
            resources,
            value.InputFingerprint.Value,
            value.Deadline,
            value.RequestedUses);
    }

    /// <summary>Reconstructs the exact domain request this document was projected from.</summary>
    /// <returns>A request equal to the projected original, with its captured authorization and tool correlation only when the document carried one.</returns>
    /// <remarks>
    /// <see cref="RequestedUses"/> is always passed
    /// explicitly rather than relying on the domain constructor's default, because a persisted request that asked for several
    /// uses must not be replayed as a single-use request.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><see cref="Scope"/>, <see cref="Identity"/>, or <see cref="Authorization"/> is null, which a well-formed document never is.</exception>
    /// <exception cref="ArgumentException"><see cref="Audience"/> or <see cref="InputFingerprint"/> is blank, <see cref="Resources"/> is empty or contains a null element, or <see cref="Authorization"/> disagrees with the request's scope or identity.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A persisted identity is empty, <see cref="Kind"/> or <see cref="Effect"/> is undefined, or <see cref="RequestedUses"/> is not positive.</exception>
    public SecurityRequest ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Scope);
        ArgumentNullException.ThrowIfNull(Identity);
        ArgumentNullException.ThrowIfNull(Authorization);
        var persisted = Resources.IsDefault ? [] : Resources;
        ArgumentException.ThrowIfContainsNull(persisted, nameof(Resources));
        var id = new SecurityRequestId(Id);
        var scope = Scope.ToDomain();
        ToolCallId? toolCallId = ToolCallId is { } callId ? new ToolCallId(callId) : null;
        var identity = Identity.ToDomain();
        var audience = new ComponentId(Audience);
        ImmutableArray<ProtectedResource> resources =
            [.. persisted.Select(static resource => resource.ToDomain())];
        var inputFingerprint = new InputFingerprint(InputFingerprint);
        return new SecurityRequest(
            id,
            scope,
            toolCallId,
            identity,
            Authorization.ToDomain(),
            audience,
            Kind,
            Effect,
            resources,
            inputFingerprint,
            Deadline,
            RequestedUses);
    }

    /// <summary>Compares requests by ordered resource contents rather than by immutable-array storage identity.</summary>
    /// <param name="other">The candidate request to compare with this one, which may be null.</param>
    /// <returns><see langword="true"/> when every scalar member and nested document is equal and both resource sequences are element-wise equal in the same order.</returns>
    /// <remarks>
    /// Compiler-generated record equality would compare <see cref="Resources"/> by backing-array identity, so two documents
    /// decoded from byte-identical JSON would compare unequal. This override restores the same ordered content equality the
    /// mirrored <see cref="SecurityRequest"/> guarantees.
    /// </remarks>
    public bool Equals(JsonSecurityRequest? other) =>
        other is not null
        && Id == other.Id
        && Scope == other.Scope
        && ToolCallId == other.ToolCallId
        && Identity == other.Identity
        && Authorization == other.Authorization
        && Audience == other.Audience
        && Kind == other.Kind
        && Effect == other.Effect
        && Resources.AsSpan().SequenceEqual(other.Resources.AsSpan())
        && InputFingerprint == other.InputFingerprint
        && Deadline == other.Deadline
        && RequestedUses == other.RequestedUses;

    /// <summary>Computes a hash consistent with <see cref="Equals(JsonSecurityRequest?)"/>.</summary>
    /// <returns>A hash derived from every scalar member, nested document, and each ordered resource.</returns>
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
        foreach (var resource in Resources.AsSpan())
        {
            hash.Add(resource);
        }

        hash.Add(InputFingerprint);
        hash.Add(Deadline);
        hash.Add(RequestedUses);
        return hash.ToHashCode();
    }
}
