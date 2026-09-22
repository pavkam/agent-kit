// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The immutable raw call request that an <see cref="IToolExecutor"/> resolves, validates, authorizes, and invokes.</summary>
/// <remarks>
/// This is the spec-shaped call request consumed by the tool-executor pipeline. It is distinct from the reduced
/// <see cref="LegacyToolCallRequest"/> still consumed by <see cref="ILegacyToolCallOrchestrator"/> until
/// every tool package migrates onto this runtime. <see cref="RawArguments"/> are bounded, provider-supplied,
/// unparsed bytes; bounding, parsing, canonical schema validation, and authorization all happen downstream in the
/// executor pipeline described by the tool-call lifecycle contract. This type is an immutable value object with
/// structural equality over its fields; it carries no mutable state and is safe to share across threads.
/// </remarks>
public sealed record ToolCallRequest
{
    /// <summary>Initializes an immutable raw tool-call request.</summary>
    /// <param name="agentId">The nondefault owning agent.</param>
    /// <param name="sessionId">The nondefault owning session.</param>
    /// <param name="runId">The nondefault active run.</param>
    /// <param name="turnId">The nondefault active turn.</param>
    /// <param name="operationId">The nondefault causal operation.</param>
    /// <param name="callId">The nondefault provider-correlated call identity.</param>
    /// <param name="authorization">The exact captured authorization matching every supplied identity.</param>
    /// <param name="catalogVersion">The nondefault catalog version this call was dispatched against.</param>
    /// <param name="sourceOrdinal">The nonnegative provider-emitted source ordinal used for publication order.</param>
    /// <param name="providerAlias">The nondefault requested provider-visible alias.</param>
    /// <param name="rawArguments">The initialized bounded raw argument bytes as supplied by the provider.</param>
    /// <param name="requestedAt">The request timestamp.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An identity, <paramref name="catalogVersion"/>, or <paramref name="providerAlias"/> is default, or
    /// <paramref name="sourceOrdinal"/> is negative.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="authorization"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="authorization"/> does not match the supplied agent/session/run/turn/operation, or
    /// <paramref name="rawArguments"/> is uninitialized.
    /// </exception>
    public ToolCallRequest(
        AgentId agentId,
        SessionId sessionId,
        RunId runId,
        TurnId turnId,
        OperationId operationId,
        ToolCallId callId,
        SecurityAuthorizationContext authorization,
        ToolCatalogVersion catalogVersion,
        int sourceOrdinal,
        ToolAlias providerAlias,
        ImmutableArray<byte> rawArguments,
        DateTimeOffset requestedAt)
    {
        AcceptedToolCall.ValidateIdentities(agentId, sessionId, runId, turnId, operationId, callId);
        ArgumentNullException.ThrowIfNull(authorization);
        AcceptedToolCall.ValidateAuthorization(agentId, sessionId, runId, turnId, operationId, authorization);
        ArgumentOutOfRangeException.ThrowIfEqual(catalogVersion, default);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceOrdinal);
        ArgumentOutOfRangeException.ThrowIfEqual(providerAlias, default);
        ArgumentException.ThrowIfDefault(rawArguments);

        CallId = callId;
        Authorization = authorization;
        CatalogVersion = catalogVersion;
        SourceOrdinal = sourceOrdinal;
        ProviderAlias = providerAlias;
        RawArguments = rawArguments;
        RequestedAt = requestedAt;
    }

    /// <summary>Gets the owning agent.</summary>
    /// <value>Read through <see cref="Authorization"/>'s bound scope, which the constructor validates against the supplied identity; never a second stored copy.</value>
    public AgentId AgentId => Authorization.Scope.AgentId;

    /// <summary>Gets the owning session.</summary>
    /// <value>Read through <see cref="Authorization"/>'s bound scope, which the constructor requires to be present and equal to the supplied identity; never a second stored copy.</value>
    public SessionId SessionId => Authorization.Scope.SessionId!.Value;

    /// <summary>Gets the owning run.</summary>
    /// <value>Read through <see cref="Authorization"/>'s bound in-run correlation; never a second stored copy.</value>
    public RunId RunId => AcceptedToolCall.RequireCorrelation(Authorization).RunId;

    /// <summary>Gets the owning turn.</summary>
    /// <value>Read through <see cref="Authorization"/>'s bound in-run correlation; never a second stored copy.</value>
    public TurnId TurnId => AcceptedToolCall.RequireCorrelation(Authorization).TurnId!.Value;

    /// <summary>Gets the causal operation.</summary>
    /// <value>Read through <see cref="Authorization"/>'s bound in-run correlation; never a second stored copy.</value>
    public OperationId OperationId => AcceptedToolCall.RequireCorrelation(Authorization).OperationId;

    /// <summary>Gets the provider-correlated call identity.</summary>
    /// <value>A nondefault identity.</value>
    public ToolCallId CallId { get; }

    /// <summary>Gets the exact captured authorization for this request's identity tuple.</summary>
    /// <value>Immutable evidence matching the complete agent/session/run/turn/operation correlation.</value>
    public SecurityAuthorizationContext Authorization { get; }

    /// <summary>Gets the catalog version this call was dispatched against.</summary>
    /// <value>A nondefault version used by resolution to detect a catalog change after dispatch.</value>
    public ToolCatalogVersion CatalogVersion { get; }

    /// <summary>Gets the provider-emitted source ordinal.</summary>
    /// <value>A nonnegative ordinal establishing publication order among sibling calls.</value>
    public int SourceOrdinal { get; }

    /// <summary>Gets the requested provider-visible alias.</summary>
    /// <value>A nondefault alias resolved only through the exact catalog snapshot named by <see cref="CatalogVersion"/>.</value>
    public ToolAlias ProviderAlias { get; }

    /// <summary>Gets the bounded raw argument bytes.</summary>
    /// <value>Initialized, unparsed, provider-supplied evidence; not yet bounded, parsed, or validated.</value>
    public ImmutableArray<byte> RawArguments { get; }

    /// <summary>Gets the request timestamp.</summary>
    /// <value>A timestamp fact without ordering inference.</value>
    public DateTimeOffset RequestedAt { get; }

    /// <summary>Determines complete structural request equality.</summary>
    /// <param name="other">The record to compare, or null.</param>
    /// <returns>True when every scalar/reference field and the raw-argument byte sequence are equal.</returns>
    public bool Equals(ToolCallRequest? other) =>
        other is not null
        && CallId == other.CallId
        && Authorization == other.Authorization
        && CatalogVersion == other.CatalogVersion
        && SourceOrdinal == other.SourceOrdinal
        && ProviderAlias == other.ProviderAlias
        && RawArguments.SequenceEqual(other.RawArguments)
        && RequestedAt == other.RequestedAt;

    /// <summary>Returns a hash compatible with complete structural equality.</summary>
    /// <returns>A hash over every scalar/reference field and the raw-argument byte sequence.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(CallId);
        hash.Add(Authorization);
        hash.Add(CatalogVersion);
        hash.Add(SourceOrdinal);
        hash.Add(ProviderAlias);
        foreach (var b in RawArguments)
        {
            hash.Add(b);
        }

        hash.Add(RequestedAt);
        return hash.ToHashCode();
    }
}
