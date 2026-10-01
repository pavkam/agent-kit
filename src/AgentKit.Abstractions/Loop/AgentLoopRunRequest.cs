// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics;

/// <summary>One complete, immutable request to run an agent loop for one run.</summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// The request pins the exact admitted <see cref="AgentDefinition"/> and the exact
/// <see cref="EffectiveConfigurationSnapshot"/> captured with the run's authorization. Model policy, requirements,
/// instructions, request settings, output contract, hook profile, and budget profile are read from that definition,
/// so the request cannot carry a second, divergent copy. The tools a run may call come from the definition's authored
/// toolsets through a run-bound catalog capture, never from the request. The caller is responsible for admitting any
/// new input by appending it to the session before starting a run.
/// </para>
/// </remarks>
public sealed record AgentLoopRunRequest
{
    /// <summary>Initializes a run request from exact definition and configuration evidence.</summary>
    /// <param name="agent">The exact immutable agent definition admitted for this run.</param>
    /// <param name="sessionId">The session this run reads from and commits to.</param>
    /// <param name="branchId">The branch this run reads from and commits to.</param>
    /// <param name="runId">The stable identity of this run.</param>
    /// <param name="identity">The authenticated identity on whose behalf the run executes.</param>
    /// <param name="authorization">The captured run-start authorization evidence.</param>
    /// <param name="sessionProfile">The exact session profile selected for this invocation.</param>
    /// <param name="configuration">The exact effective configuration snapshot captured for the run.</param>
    /// <param name="maxTurns">The positive maximum turn count.</param>
    /// <param name="attemptTimeout">The positive model-attempt timeout.</param>
    /// <param name="extensions">Caller-specific immutable request data.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="authorization"/> does not exactly match the definition's agent, the session, the run, and the
    /// complete identity, or the definition revision, configuration version, or configuration fingerprint differs from
    /// the authorization or session-profile evidence.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxTurns"/> or <paramref name="attemptTimeout"/> is not positive.
    /// </exception>
    public AgentLoopRunRequest(
        AgentDefinition agent,
        SessionId sessionId,
        BranchId branchId,
        RunId runId,
        ExecutionIdentity identity,
        SecurityAuthorizationContext authorization,
        SessionProfileSnapshot sessionProfile,
        EffectiveConfigurationSnapshot configuration,
        int maxTurns,
        TimeSpan attemptTimeout,
        ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentException.ThrowIfInvalidRunAuthorization(identity, agent.Id, sessionId, runId, authorization);
        ArgumentNullException.ThrowIfNull(sessionProfile);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNotEqual(agent.Revision, authorization.AgentDefinitionRevision, nameof(agent));
        ArgumentException.ThrowIfNotEqual(configuration.Version, authorization.ConfigurationVersion);
        ArgumentException.ThrowIfNotEqual(configuration.Fingerprint, sessionProfile.ConfigurationFingerprint);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxTurns, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(attemptTimeout, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(extensions);

        Agent = agent;
        BranchId = branchId;
        Identity = identity;
        Authorization = authorization;
        SessionProfile = sessionProfile;
        Configuration = configuration;
        MaxTurns = maxTurns;
        AttemptTimeout = attemptTimeout;
        Extensions = extensions;
    }

    /// <summary>Gets the exact admitted agent definition this run executes.</summary>
    /// <value>The immutable definition; never null.</value>
    public AgentDefinition Agent { get; }

    /// <summary>Gets the agent this run belongs to.</summary>
    /// <value>The pinned definition's identity, which <see cref="Authorization"/>'s bound scope also names.</value>
    public AgentId AgentId => Agent.Id;

    /// <summary>Gets the session this run reads from and commits to.</summary>
    /// <value>Read through <see cref="Authorization"/>'s bound scope. Construction requires the scope's session to be present and equal to the supplied session, so this is never a second stored copy.</value>
    public SessionId SessionId => Authorization.Scope.SessionId!.Value;

    /// <summary>Gets the branch this run reads from and commits to.</summary>
    public BranchId BranchId { get; init; }

    /// <summary>Gets the stable identity of this run.</summary>
    /// <value>Read through <see cref="Authorization"/>'s bound in-run correlation, which construction requires to carry this exact run; this request never stores a second copy of it.</value>
    public RunId RunId => RunCorrelation.RunId;

    /// <summary>Gets the identity on whose behalf this run is performed.</summary>
    public ExecutionIdentity Identity { get; }

    /// <summary>Gets the captured run-start authorization evidence.</summary>
    /// <value>Immutable evidence matching this request's identity, agent, session, and run.</value>
    public SecurityAuthorizationContext Authorization { get; }

    /// <summary>Gets the exact effective configuration captured for this run.</summary>
    /// <value>The immutable snapshot whose version and fingerprint match the authorization and session profile.</value>
    public EffectiveConfigurationSnapshot Configuration { get; }

    /// <summary>Gets the immutable session profile selected for this invocation.</summary>
    /// <value>The exact compiled profile supplied to session coordination.</value>
    public SessionProfileSnapshot SessionProfile { get; }

    /// <summary>Gets the candidate and fallback policy used to choose this run's model.</summary>
    /// <value>The pinned definition's model selection policy.</value>
    public ModelSelectionPolicy ModelPolicy => Agent.Models;

    /// <summary>Gets the portable behaviors this run's requests need.</summary>
    /// <value>The pinned definition's model requirements, used to reject or downgrade an incompatible model before any provider request is sent.</value>
    public ModelRequirements ModelRequirements => Agent.Models.Requirements;

    /// <summary>Gets the effective sampling and output settings.</summary>
    /// <value>The pinned definition's request settings.</value>
    public LlmRequestSettings Settings => Agent.Models.RequestSettings;

    /// <summary>Gets the maximum number of turns this run may take before it is halted.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The initialized value is not positive.</exception>
    public int MaxTurns
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0, nameof(MaxTurns));
            field = value;
        }
    }

    /// <summary>Gets the maximum duration allowed for a single model attempt.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The initialized value is not positive.</exception>
    public TimeSpan AttemptTimeout
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero, nameof(AttemptTimeout));
            field = value;
        }
    }

    /// <summary>Gets caller-specific or forward-compatible request data.</summary>
    /// <exception cref="ArgumentNullException">The initialized value is null.</exception>
    public ExtensionData Extensions
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Extensions));
            field = value;
        }
    }

    /// <summary>Gets the structured-output contract this run must satisfy before it completes, when one is selected.</summary>
    /// <value>
    /// The pinned definition's output contract handed to the run's <see cref="IOutputProcessor"/> after each terminal
    /// assistant response, or <see langword="null"/> for a free-text run.
    /// </value>
    public OutputDefinition? Output => Agent.Output;

    /// <summary>Gets the named budget profile this run resolves when creating its scope.</summary>
    /// <value>The pinned definition's selected budget profile.</value>
    public BudgetProfileKey BudgetProfile => Agent.Components.BudgetProfile;

    /// <summary>Gets the hook profile whose captured catalog this run dispatches through.</summary>
    /// <value>The pinned definition's selected hook profile.</value>
    public HookProfileKey HookProfile => Agent.HookProfile;

    /// <summary>Gets the optional best-effort observer for provisional model and tool progress.</summary>
    /// <remarks>
    /// The observer is operational wiring rather than semantic request data, so it does not participate in
    /// structural equality or hashing. Its failures are isolated by the loop.
    /// </remarks>
    public IAgentRunObserver? Observer { get; init; }

    /// <summary>Gets the per-run tool allow-list, or <see langword="null"/> when the run exposes the definition's whole tool surface.</summary>
    /// <value>
    /// The tool identifiers the run may see and call. The loop intersects the run's captured catalog with this list, so a tool
    /// outside it is neither advertised nor resolvable and a call naming it fails closed as an unknown tool; an empty list
    /// exposes no tool.
    /// </value>
    public ImmutableArray<ToolId>? AllowedTools
    {
        get;
        init
        {
            if (value is { } allowed)
            {
                ArgumentException.ThrowIfDefault(allowed, nameof(AllowedTools));
            }

            field = value;
        }
    }

    /// <summary>Gets the existing budget scope the run's own scope is created beneath, or <see langword="null"/> for a root run scope.</summary>
    /// <value>
    /// A scope the budget authority already holds (for example a delegated goal's reserved child scope). The run's scope
    /// inherits that scope's remaining capacity, so exhausting the parent stops the run.
    /// </value>
    public BudgetScopeId? BudgetParentScopeId
    {
        get;
        init
        {
            if (value is { } parent)
            {
                ArgumentOutOfRangeException.ThrowIfEqual(parent, default, nameof(BudgetParentScopeId));
            }

            field = value;
        }
    }

    /// <summary>
    /// Gets the durable lane a caller already admitted this run on, when one was admitted through the session
    /// lane protocol.
    /// </summary>
    /// <value>
    /// The lane, accepted correlation, and installed state revision the loop must honor and release, or
    /// <see langword="null"/> when the caller committed this run's initial input directly and no lane exists to
    /// release.
    /// </value>
    /// <exception cref="ArgumentException">
    /// The initialized value's <see cref="LoopLaneAdmission.AcceptedCorrelation"/> names a different run than
    /// <see cref="RunId"/>.
    /// </exception>
    public LoopLaneAdmission? LaneAdmission
    {
        get;
        init
        {
            if (value is { } admission)
            {
                ArgumentException.ThrowIfNotEqual(admission.AcceptedCorrelation.RunId, RunId, nameof(LaneAdmission));
            }

            field = value;
        }
    }

    /// <inheritdoc/>
    public bool Equals(AgentLoopRunRequest? other) =>
        other is not null
        && Agent.Equals(other.Agent)
        && SessionId.Equals(other.SessionId)
        && BranchId.Equals(other.BranchId)
        && RunId.Equals(other.RunId)
        && Identity.Equals(other.Identity)
        && Authorization.Equals(other.Authorization)
        && Configuration.Equals(other.Configuration)
        && SessionProfile.Equals(other.SessionProfile)
        && MaxTurns == other.MaxTurns
        && AttemptTimeout == other.AttemptTimeout
        && Extensions.Equals(other.Extensions)
        && Equals(LaneAdmission, other.LaneAdmission);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Agent);
        hash.Add(SessionId);
        hash.Add(BranchId);
        hash.Add(RunId);
        hash.Add(Identity);
        hash.Add(Authorization);
        hash.Add(Configuration);
        hash.Add(SessionProfile);
        hash.Add(MaxTurns);
        hash.Add(AttemptTimeout);
        hash.Add(Extensions);
        hash.Add(LaneAdmission);
        return hash.ToHashCode();
    }

    /// <summary>Gets the validated in-run correlation bound by <see cref="Authorization"/>'s scope.</summary>
    /// <value>Construction requires the scope's correlation to be this exact kind before assignment.</value>
    private InRunOperationCorrelation RunCorrelation
    {
        get
        {
            Debug.Assert(
                Authorization.Scope.Correlation is InRunOperationCorrelation,
                "Constructor validation guarantees an in-run correlation.");
            return (InRunOperationCorrelation) Authorization.Scope.Correlation;
        }
    }
}
