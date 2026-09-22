// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records one resolved call after its raw arguments parsed and satisfied the descriptor's declared canonical schema.</summary>
/// <remarks>
/// Validation binds semantic argument evidence only: it neither authorizes invocation nor invokes the tool.
/// <see cref="Arguments"/> is an owned, cloned canonical value independent of any caller <c>JsonDocument</c> lifetime.
/// This type is an immutable value object with structural equality over its fields and is safe to share across
/// threads without synchronization.
/// </remarks>
public sealed record ValidatedToolCall
{
    /// <summary>Initializes a validated call bound to its parsed and schema-checked canonical arguments.</summary>
    /// <param name="agentId">The nondefault owning agent.</param>
    /// <param name="sessionId">The nondefault owning session.</param>
    /// <param name="runId">The nondefault active run.</param>
    /// <param name="turnId">The nondefault active turn.</param>
    /// <param name="operationId">The nondefault causal operation.</param>
    /// <param name="callId">The nondefault provider-correlated call identity.</param>
    /// <param name="authorization">The exact captured authorization matching every supplied identity.</param>
    /// <param name="catalogVersion">The nondefault catalog version the alias resolved against.</param>
    /// <param name="providerAlias">The nondefault requested provider-visible alias.</param>
    /// <param name="tool">The nonnull exact resolved descriptor.</param>
    /// <param name="toolVersion">The nondefault resolved version, equal to <paramref name="tool"/>'s declared version.</param>
    /// <param name="executionPolicy">The nonnull selected execution-policy reference for the resolved descriptor.</param>
    /// <param name="sourceOrdinal">The nonnegative provider-emitted source ordinal carried from the originating request.</param>
    /// <param name="arguments">The final, already schema-validated canonical arguments.</param>
    /// <param name="inputFingerprint">The nondefault fingerprint of <paramref name="arguments"/>.</param>
    /// <param name="requestedAt">The original request timestamp.</param>
    /// <param name="validatedAt">The timestamp validation completed.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An identity, <paramref name="catalogVersion"/>, <paramref name="providerAlias"/>, <paramref name="toolVersion"/>,
    /// or <paramref name="inputFingerprint"/> is default, or <paramref name="sourceOrdinal"/> is negative.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="authorization"/>, <paramref name="tool"/>, or <paramref name="executionPolicy"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="authorization"/> does not match the supplied agent/session/run/turn/operation, or
    /// <paramref name="toolVersion"/> does not equal <paramref name="tool"/>'s declared version.
    /// </exception>
    public ValidatedToolCall(
        AgentId agentId,
        SessionId sessionId,
        RunId runId,
        TurnId turnId,
        OperationId operationId,
        ToolCallId callId,
        SecurityAuthorizationContext authorization,
        ToolCatalogVersion catalogVersion,
        ToolAlias providerAlias,
        ToolDescriptor tool,
        ToolVersion toolVersion,
        ToolExecutionPolicyReference executionPolicy,
        int sourceOrdinal,
        JsonElement arguments,
        InputFingerprint inputFingerprint,
        DateTimeOffset requestedAt,
        DateTimeOffset validatedAt)
    {
        AcceptedToolCall.ValidateIdentities(agentId, sessionId, runId, turnId, operationId, callId);
        ArgumentNullException.ThrowIfNull(authorization);
        AcceptedToolCall.ValidateAuthorization(agentId, sessionId, runId, turnId, operationId, authorization);
        ArgumentOutOfRangeException.ThrowIfEqual(catalogVersion, default);
        ArgumentOutOfRangeException.ThrowIfEqual(providerAlias, default);
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentOutOfRangeException.ThrowIfEqual(toolVersion, default);
        ArgumentException.ThrowIfNotEqual(tool.Version, toolVersion, nameof(toolVersion));
        ArgumentNullException.ThrowIfNull(executionPolicy);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceOrdinal);
        ArgumentOutOfRangeException.ThrowIfEqual(inputFingerprint, default);

        CallId = callId;
        Authorization = authorization;
        CatalogVersion = catalogVersion;
        ProviderAlias = providerAlias;
        Tool = tool;
        ToolVersion = toolVersion;
        ExecutionPolicy = executionPolicy;
        SourceOrdinal = sourceOrdinal;
        Arguments = arguments;
        InputFingerprint = inputFingerprint;
        RequestedAt = requestedAt;
        ValidatedAt = validatedAt;
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
    /// <value>A nondefault identity carried unchanged from the originating request.</value>
    public ToolCallId CallId { get; }

    /// <summary>Gets the exact captured authorization for this request's identity tuple.</summary>
    /// <value>Immutable evidence matching the complete agent/session/run/turn/operation correlation.</value>
    public SecurityAuthorizationContext Authorization { get; }

    /// <summary>Gets the catalog version the alias resolved against.</summary>
    /// <value>A nondefault version equal to the capture's exact snapshot version at resolution time.</value>
    public ToolCatalogVersion CatalogVersion { get; }

    /// <summary>Gets the requested provider-visible alias.</summary>
    /// <value>A nondefault alias resolved through the exact catalog snapshot named by <see cref="CatalogVersion"/>.</value>
    public ToolAlias ProviderAlias { get; }

    /// <summary>Gets the exact resolved descriptor.</summary>
    /// <value>The captured descriptor selected by resolution; untrusted metadata about the tool, not authority.</value>
    public ToolDescriptor Tool { get; }

    /// <summary>Gets the resolved tool version.</summary>
    /// <value>A nondefault version equal to <see cref="Tool"/>'s declared version.</value>
    public ToolVersion ToolVersion { get; }

    /// <summary>Gets the selected execution-policy reference.</summary>
    /// <value>The exact policy reference captured with the resolved descriptor in the catalog snapshot.</value>
    public ToolExecutionPolicyReference ExecutionPolicy { get; }

    /// <summary>Gets the provider-emitted source ordinal.</summary>
    /// <value>A nonnegative ordinal establishing publication order among sibling calls.</value>
    public int SourceOrdinal { get; }

    /// <summary>Gets the final canonical arguments.</summary>
    /// <value>Already parsed, bounded, and schema-validated arguments owned independently of any caller document.</value>
    public JsonElement Arguments { get; }

    /// <summary>Gets the fingerprint of the validated canonical arguments.</summary>
    /// <value>A nondefault normalized-input digest.</value>
    public InputFingerprint InputFingerprint { get; }

    /// <summary>Gets the original request timestamp.</summary>
    /// <value>A timestamp fact without ordering inference.</value>
    public DateTimeOffset RequestedAt { get; }

    /// <summary>Gets the timestamp validation completed.</summary>
    /// <value>A timestamp fact without ordering inference.</value>
    public DateTimeOffset ValidatedAt { get; }

    /// <summary>Determines complete structural validated-call equality.</summary>
    /// <param name="other">The record to compare, or null.</param>
    /// <returns>True when every scalar/reference field and the canonical argument text are equal.</returns>
    public bool Equals(ValidatedToolCall? other) =>
        other is not null
        && CallId == other.CallId
        && Authorization == other.Authorization
        && CatalogVersion == other.CatalogVersion
        && ProviderAlias == other.ProviderAlias
        && Tool == other.Tool
        && ToolVersion == other.ToolVersion
        && ExecutionPolicy == other.ExecutionPolicy
        && SourceOrdinal == other.SourceOrdinal
        && Arguments.GetRawText() == other.Arguments.GetRawText()
        && InputFingerprint == other.InputFingerprint
        && RequestedAt == other.RequestedAt
        && ValidatedAt == other.ValidatedAt;

    /// <summary>Returns a hash compatible with complete structural equality.</summary>
    /// <returns>A hash over every scalar/reference field and the canonical argument text.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(CallId);
        hash.Add(Authorization);
        hash.Add(CatalogVersion);
        hash.Add(ProviderAlias);
        hash.Add(Tool);
        hash.Add(ToolVersion);
        hash.Add(ExecutionPolicy);
        hash.Add(SourceOrdinal);
        hash.Add(Arguments.GetRawText());
        hash.Add(InputFingerprint);
        hash.Add(RequestedAt);
        hash.Add(ValidatedAt);
        return hash.ToHashCode();
    }
}
