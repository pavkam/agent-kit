// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures coherent run, identity, authorization, configuration, and toolset evidence before source discovery.</summary>
/// <remarks>
/// Construction validates local correlation without contacting providers or resolving publications.
/// The catalog still performs source acquisition, schema/capability preflight, and collision policy;
/// protected discovery requires a fresh grant at its own effecting boundary.
/// </remarks>
public sealed record ToolDiscoveryRequest
{
    /// <summary>Creates immutable discovery input whose security evidence agrees with its complete run context.</summary>
    /// <param name="agentId">The nondefault owning agent.</param>
    /// <param name="sessionId">The nondefault owning session.</param>
    /// <param name="runId">The nondefault active run; before-run and after-run authorization are not valid discovery scope.</param>
    /// <param name="identity">The non-null complete authenticated identity, structurally equal to the authorization identity.</param>
    /// <param name="authorization">The non-null captured authorization matching agent, session, active run, identity, definition revision, and configuration version.</param>
    /// <param name="agentDefinitionRevision">The captured nonnegative definition revision, including zero.</param>
    /// <param name="configuration">The non-null immutable effective configuration selected for this request.</param>
    /// <param name="toolsets">Initialized ordered authored selections with unique toolset keys; an empty selection is valid.</param>
    /// <param name="modelCapabilities">The non-null model capability snapshot used by subsequent preflight.</param>
    /// <exception cref="ArgumentOutOfRangeException">An agent, session, or run identity is default.</exception>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="ArgumentException">Authorization disagrees with the request, or the toolset collection is uninitialized, contains null, or repeats a key.</exception>
    public ToolDiscoveryRequest(
        AgentId agentId,
        SessionId sessionId,
        RunId runId,
        ExecutionIdentity identity,
        SecurityAuthorizationContext authorization,
        AgentDefinitionRevision agentDefinitionRevision,
        EffectiveConfigurationSnapshot configuration,
        ImmutableArray<ToolsetReference> toolsets,
        ModelCapabilities modelCapabilities)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfContainsNull(toolsets);
        ArgumentNullException.ThrowIfNull(modelCapabilities);
        ArgumentException.ThrowIfNotEqual(authorization.Scope.AgentId, agentId, nameof(authorization));
        ArgumentException.ThrowIfNotEqual(authorization.Scope.SessionId, (SessionId?) sessionId, nameof(authorization));
        ArgumentException.ThrowIfNotEqual(
            authorization.Scope.Correlation is InRunOperationCorrelation correlation && correlation.RunId == runId,
            true, nameof(authorization));
        ArgumentException.ThrowIfNotEqual(authorization.Identity, identity, nameof(authorization));
        ArgumentException.ThrowIfNotEqual(authorization.AgentDefinitionRevision, agentDefinitionRevision, nameof(authorization));
        ArgumentException.ThrowIfNotEqual(authorization.ConfigurationVersion, configuration.Version, nameof(authorization));
        var keys = new HashSet<ToolsetKey>();
        foreach (var toolset in toolsets)
        {
            ArgumentException.ThrowIfNotEqual(keys.Add(toolset.Key), true, nameof(toolsets));
        }

        AgentId = agentId;
        SessionId = sessionId;
        RunId = runId;
        Identity = identity;
        Authorization = authorization;
        AgentDefinitionRevision = agentDefinitionRevision;
        Configuration = configuration;
        Toolsets = toolsets;
        ModelCapabilities = modelCapabilities;
    }

    /// <summary>Gets the agent whose selected toolsets are being discovered.</summary>
    /// <value>The nondefault identity shared with the authorization scope.</value>
    public AgentId AgentId { get; }
    /// <summary>Gets the session that owns the active run.</summary>
    /// <value>The nondefault identity shared with the authorization scope.</value>
    public SessionId SessionId { get; }
    /// <summary>Gets the active run for this discovery operation.</summary>
    /// <value>The nondefault identity retained by the in-run authorization correlation.</value>
    public RunId RunId { get; }
    /// <summary>Gets the complete authenticated identity used for principal-specific discovery.</summary>
    /// <value>Immutable identity evidence equal to the authorization identity; it grants no authority.</value>
    public ExecutionIdentity Identity { get; }
    /// <summary>Gets the coherent captured security selection and exact operation scope.</summary>
    /// <value>Immutable evidence to revalidate when authorizing protected discovery, not a live grant.</value>
    public SecurityAuthorizationContext Authorization { get; }
    /// <summary>Gets the definition revision that selected this discovery context.</summary>
    /// <value>The nonnegative revision shared with the authorization context.</value>
    public AgentDefinitionRevision AgentDefinitionRevision { get; }
    /// <summary>Gets the complete effective semantic configuration captured for discovery.</summary>
    /// <value>The immutable snapshot whose version equals the authorization configuration version.</value>
    public EffectiveConfigurationSnapshot Configuration { get; }
    /// <summary>Gets the ordered authored toolset selections without resolving their publications.</summary>
    /// <value>An initialized, possibly empty immutable sequence with unique exact toolset keys.</value>
    public ImmutableArray<ToolsetReference> Toolsets { get; }
    /// <summary>Gets the model capability snapshot used for source and schema preflight.</summary>
    /// <value>The supplied non-null immutable value; retaining it does not prove source support.</value>
    public ModelCapabilities ModelCapabilities { get; }

    /// <summary>Compares complete discovery evidence, including ordered toolset selections.</summary>
    /// <param name="other">The request to compare, or null.</param>
    /// <returns>True only when all scope, security, configuration, model, and ordered selection values agree.</returns>
    public bool Equals(ToolDiscoveryRequest? other) => other is not null
        && AgentId == other.AgentId && SessionId == other.SessionId && RunId == other.RunId
        && Identity == other.Identity && Authorization == other.Authorization
        && AgentDefinitionRevision == other.AgentDefinitionRevision && Configuration == other.Configuration
        && ModelCapabilities == other.ModelCapabilities && Toolsets.SequenceEqual(other.Toolsets);

    /// <summary>Computes a hash compatible with complete structural request equality.</summary>
    /// <returns>A hash over the captured scope, security, configuration, model, and ordered toolsets.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(AgentId); hash.Add(SessionId); hash.Add(RunId); hash.Add(Identity); hash.Add(Authorization);
        hash.Add(AgentDefinitionRevision); hash.Add(Configuration); hash.Add(ModelCapabilities);
        foreach (var toolset in Toolsets)
        {
            hash.Add(toolset);
        }
        return hash.ToHashCode();
    }
}
