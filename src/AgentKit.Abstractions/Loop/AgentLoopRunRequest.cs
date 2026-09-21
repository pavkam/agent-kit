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
/// This is a deliberately reduced stand-in for the fuller
/// <c>AgentRunInvocation</c> described by the agent-runtime architecture,
/// which additionally carries an <c>AgentDefinition</c>, an agent catalog
/// version, a hook dispatch context,
/// a compiled <c>AgentRunServices</c> bundle, an effective-configuration
/// snapshot, and a run policy snapshot. Until those packages exist, this
/// request carries the model, tool, and turn-limit choices directly, and
/// the caller is responsible for admitting any new input by appending it to
/// the session before starting a run — there is no queued-input admission
/// boundary yet.
/// </para>
/// </remarks>
public sealed record AgentLoopRunRequest
{
    /// <summary>Initializes a new instance of the <see cref="AgentLoopRunRequest"/> record.</summary>
    /// <param name="agentId">The agent this run belongs to.</param>
    /// <param name="sessionId">The session this run reads from and commits to.</param>
    /// <param name="branchId">The branch this run reads from and commits to.</param>
    /// <param name="runId">The stable identity of this run.</param>
    /// <param name="identity">The identity on whose behalf this run is performed.</param>
    /// <param name="authorization">The captured run-start authorization and configuration evidence.</param>
    /// <param name="sessionProfile">The immutable session profile selected for this invocation.</param>
    /// <param name="modelPolicy">The candidate and fallback policy used to choose this run's model.</param>
    /// <param name="modelRequirements">The portable behaviors this run's requests need.</param>
    /// <param name="instructions">The system and developer instructions to place first in every request.</param>
    /// <param name="tools">The tools available for the model to call during this run.</param>
    /// <param name="toolChoice">The tool-call selection policy.</param>
    /// <param name="settings">The effective sampling and output settings.</param>
    /// <param name="maxTurns">The maximum number of turns this run may take before it is halted.</param>
    /// <param name="attemptTimeout">The maximum duration allowed for a single model attempt.</param>
    /// <param name="extensions">Caller-specific or forward-compatible request data.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="identity"/>, <paramref name="authorization"/>,
    /// <paramref name="sessionProfile"/>, <paramref name="modelPolicy"/>,
    /// <paramref name="modelRequirements"/>, <paramref name="toolChoice"/>,
    /// <paramref name="settings"/>, or <paramref name="extensions"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="instructions"/> or <paramref name="tools"/> is a
    /// default, uninitialized array, or <paramref name="authorization"/> does
    /// not exactly match the supplied agent, session, run, and complete identity.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxTurns"/> is not positive, or
    /// <paramref name="attemptTimeout"/> is not positive.
    /// </exception>
    public AgentLoopRunRequest(
        AgentId agentId,
        SessionId sessionId,
        BranchId branchId,
        RunId runId,
        ExecutionIdentity identity,
        SecurityAuthorizationContext authorization,
        SessionProfileSnapshot sessionProfile,
        ModelSelectionPolicy modelPolicy,
        ModelRequirements modelRequirements,
        ImmutableArray<AgentMessage> instructions,
        ImmutableArray<LlmToolDefinition> tools,
        LlmToolChoice toolChoice,
        LlmRequestSettings settings,
        int maxTurns,
        TimeSpan attemptTimeout,
        ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentException.ThrowIfInvalidRunAuthorization(identity, agentId, sessionId, runId, authorization);
        ArgumentNullException.ThrowIfNull(sessionProfile);
        ArgumentNullException.ThrowIfNull(modelPolicy);
        ArgumentNullException.ThrowIfNull(modelRequirements);
        ArgumentException.ThrowIfDefault(instructions);
        ArgumentException.ThrowIfDefault(tools);
        ArgumentNullException.ThrowIfNull(toolChoice);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxTurns, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(attemptTimeout, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(extensions);

        BranchId = branchId;
        Identity = identity;
        Authorization = authorization;
        SessionProfile = sessionProfile;
        ModelPolicy = modelPolicy;
        ModelRequirements = modelRequirements;
        Instructions = instructions;
        Tools = tools;
        ToolChoice = toolChoice;
        Settings = settings;
        MaxTurns = maxTurns;
        AttemptTimeout = attemptTimeout;
        Extensions = extensions;
    }

    /// <summary>Initializes a reduced run request from exact definition and configuration evidence.</summary>
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
    /// <exception cref="ArgumentException">The definition or configuration revision differs from authorization evidence, or an inherited request invariant is violated.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An inherited identity, count, or timeout invariant is violated.</exception>
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
        : this(
            GetAgentId(agent),
            sessionId,
            branchId,
            runId,
            identity,
            authorization,
            sessionProfile,
            agent.Models,
            agent.ModelRequirements,
            agent.Instructions,
            agent.Tools,
            agent.ToolChoice,
            agent.Settings,
            maxTurns,
            attemptTimeout,
            extensions)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNotEqual(agent.Revision, authorization.AgentDefinitionRevision, nameof(agent));
        ArgumentException.ThrowIfNotEqual(configuration.Version, authorization.ConfigurationVersion);
        ArgumentException.ThrowIfNotEqual(configuration.Fingerprint, sessionProfile.ConfigurationFingerprint);
        Agent = agent;
        Output = agent.Output;
        BudgetLimits = agent.BudgetLimits;
        Configuration = configuration;
        HookProfile = agent.HookProfile;
    }

    /// <summary>Gets the agent this run belongs to.</summary>
    /// <value>Read through <see cref="Authorization"/>'s bound scope, which every constructor validates against the supplied or derived agent identity; this request never stores a second, independently mutable copy of it.</value>
    public AgentId AgentId => Authorization.Scope.AgentId;

    /// <summary>Gets the exact admitted agent definition when supplied by the evidence-aware constructor.</summary>
    /// <value>The immutable definition, or null for a legacy reduced request.</value>
    public AgentDefinition? Agent { get; }

    /// <summary>Gets the session this run reads from and commits to.</summary>
    /// <value>Read through <see cref="Authorization"/>'s bound scope. Every constructor requires the scope's session to be present and equal to the supplied session, so this is never a second stored copy.</value>
    public SessionId SessionId => Authorization.Scope.SessionId!.Value;

    /// <summary>Gets the branch this run reads from and commits to.</summary>
    public BranchId BranchId { get; init; }

    /// <summary>Gets the stable identity of this run.</summary>
    /// <value>Read through <see cref="Authorization"/>'s bound in-run correlation, which every constructor requires to carry this exact run; this request never stores a second copy of it.</value>
    public RunId RunId => RunCorrelation.RunId;

    /// <summary>Gets the identity on whose behalf this run is performed.</summary>
    public ExecutionIdentity Identity { get; }

    /// <summary>Gets the captured run-start authorization evidence.</summary>
    /// <value>Immutable evidence matching this request's identity, agent, session, and run.</value>
    public SecurityAuthorizationContext Authorization { get; }

    /// <summary>Gets the exact effective configuration when supplied by the evidence-aware constructor.</summary>
    /// <value>The immutable snapshot, or null for a legacy reduced request.</value>
    public EffectiveConfigurationSnapshot? Configuration { get; }

    /// <summary>Gets the immutable session profile selected for this invocation.</summary>
    /// <value>The exact compiled profile supplied to session coordination.</value>
    public SessionProfileSnapshot SessionProfile { get; }

    /// <summary>Gets the candidate and fallback policy used to choose this run's model.</summary>
    /// <value>
    /// The run states which models it may use; the configured selector
    /// resolves that policy against the engine-wide catalog. The run does not
    /// name a concrete provider adapter.
    /// </value>
    /// <exception cref="ArgumentNullException">The initialized value is null.</exception>
    /// <exception cref="ArgumentException">A record copy assigns a value different from the pinned <see cref="Agent"/>'s own, when <see cref="Agent"/> is not null.</exception>
    public ModelSelectionPolicy ModelPolicy
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(ModelPolicy));
            if (Agent is not null)
            {
                ArgumentException.ThrowIfNotEqual(Agent.Models, value, nameof(ModelPolicy));
            }

            field = value;
        }
    }

    /// <summary>Gets the portable behaviors this run's requests need.</summary>
    /// <value>
    /// Used to reject or downgrade an incompatible model before any provider
    /// request is sent.
    /// </value>
    /// <exception cref="ArgumentNullException">The initialized value is null.</exception>
    /// <exception cref="ArgumentException">A record copy assigns a value different from the pinned <see cref="Agent"/>'s own, when <see cref="Agent"/> is not null.</exception>
    public ModelRequirements ModelRequirements
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(ModelRequirements));
            if (Agent is not null)
            {
                ArgumentException.ThrowIfNotEqual(Agent.ModelRequirements, value, nameof(ModelRequirements));
            }

            field = value;
        }
    }

    /// <summary>Gets the system and developer instructions to place first in every request.</summary>
    /// <exception cref="ArgumentException">The initialized array is default, or a record copy assigns a value different from the pinned <see cref="Agent"/>'s own.</exception>
    public ImmutableArray<AgentMessage> Instructions
    {
        get;
        init
        {
            ArgumentException.ThrowIfDefault(value, nameof(Instructions));
            if (Agent is not null && !Agent.Instructions.SequenceEqual(value))
            {
                throw new ArgumentException(
                    "A record copy must not diverge from the pinned Agent's own instructions.", nameof(Instructions));
            }

            field = value;
        }
    }

    /// <summary>Gets the tools available for the model to call during this run.</summary>
    /// <exception cref="ArgumentException">The initialized array is default, or a record copy assigns a value different from the pinned <see cref="Agent"/>'s own.</exception>
    public ImmutableArray<LlmToolDefinition> Tools
    {
        get;
        init
        {
            ArgumentException.ThrowIfDefault(value, nameof(Tools));
            if (Agent is not null && !Agent.Tools.SequenceEqual(value))
            {
                throw new ArgumentException(
                    "A record copy must not diverge from the pinned Agent's own tools.", nameof(Tools));
            }

            field = value;
        }
    }

    /// <summary>Gets the tool-call selection policy.</summary>
    /// <exception cref="ArgumentNullException">The initialized value is null.</exception>
    /// <exception cref="ArgumentException">A record copy assigns a value different from the pinned <see cref="Agent"/>'s own, when <see cref="Agent"/> is not null.</exception>
    public LlmToolChoice ToolChoice
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(ToolChoice));
            if (Agent is not null)
            {
                ArgumentException.ThrowIfNotEqual(Agent.ToolChoice, value, nameof(ToolChoice));
            }

            field = value;
        }
    }

    /// <summary>Gets the effective sampling and output settings.</summary>
    /// <exception cref="ArgumentNullException">The initialized value is null.</exception>
    /// <exception cref="ArgumentException">A record copy assigns a value different from the pinned <see cref="Agent"/>'s own, when <see cref="Agent"/> is not null.</exception>
    public LlmRequestSettings Settings
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Settings));
            if (Agent is not null)
            {
                ArgumentException.ThrowIfNotEqual(Agent.Settings, value, nameof(Settings));
            }

            field = value;
        }
    }

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
    /// The definition handed to the run's <see cref="IOutputProcessor"/> after each terminal assistant response, or
    /// <see langword="null"/> for a free-text run. A pinned <see cref="Agent"/> supplies it; the explicit
    /// constructor leaves it unset and a record copy may select one.
    /// </value>
    /// <exception cref="ArgumentException">A record copy assigns a value different from the pinned <see cref="Agent"/>'s own.</exception>
    public OutputDefinition? Output
    {
        get;
        init
        {
            if (Agent is not null && !Equals(Agent.Output, value))
            {
                throw new ArgumentException(
                    "A record copy must not diverge from the pinned Agent's own output definition.", nameof(Output));
            }

            field = value;
        }
    }

    /// <summary>Gets the budget limits this run reserves against, when any are selected.</summary>
    /// <value>Empty for an unbudgeted run. A pinned <see cref="Agent"/> supplies them; a record copy may select them.</value>
    /// <exception cref="ArgumentException">
    /// An initializer assigns a default array or one containing null, or a record copy diverges from the pinned <see cref="Agent"/>'s own.
    /// </exception>
    public ImmutableArray<BudgetLimit> BudgetLimits
    {
        get;
        init
        {
            ArgumentException.ThrowIfDefault(value, nameof(BudgetLimits));
            ArgumentException.ThrowIfContainsNull(value, nameof(BudgetLimits));
            if (Agent is not null && !Agent.BudgetLimits.SequenceEqual(value))
            {
                throw new ArgumentException(
                    "A record copy must not diverge from the pinned Agent's own budget limits.", nameof(BudgetLimits));
            }

            field = value;
        }
    } = [];

    /// <summary>Gets the hook profile whose captured catalog this run dispatches through.</summary>
    /// <value>
    /// Defaults to <see cref="HookRegistrationDescriptors.DefaultProfileKey"/>. When <see cref="Agent"/> is pinned,
    /// a record copy must not diverge from the agent's own selection.
    /// </value>
    /// <exception cref="ArgumentException">A record copy assigns a value different from the pinned <see cref="Agent"/>'s own.</exception>
    public HookProfileKey HookProfile
    {
        get;
        init
        {
            if (Agent is not null && !Agent.HookProfile.Equals(value))
            {
                throw new ArgumentException(
                    "A record copy must not diverge from the pinned Agent's own hook profile.", nameof(HookProfile));
            }

            field = value;
        }
    } = HookRegistrationDescriptors.DefaultProfileKey;

    /// <summary>Gets the optional best-effort observer for provisional model and tool progress.</summary>
    /// <remarks>
    /// The observer is operational wiring rather than semantic request data, so it does not participate in
    /// structural equality or hashing. Its failures are isolated by the loop.
    /// </remarks>
    public IAgentRunObserver? Observer { get; init; }

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
        && AgentId.Equals(other.AgentId)
        && SessionId.Equals(other.SessionId)
        && BranchId.Equals(other.BranchId)
        && RunId.Equals(other.RunId)
        && Identity.Equals(other.Identity)
        && Authorization.Equals(other.Authorization)
        && Equals(Agent, other.Agent)
        && Equals(Configuration, other.Configuration)
        && SessionProfile.Equals(other.SessionProfile)
        && ModelPolicy.Equals(other.ModelPolicy)
        && ModelRequirements.Equals(other.ModelRequirements)
        && Instructions.SequenceEqual(other.Instructions)
        && Tools.SequenceEqual(other.Tools)
        && ToolChoice.Equals(other.ToolChoice)
        && Settings.Equals(other.Settings)
        && MaxTurns == other.MaxTurns
        && AttemptTimeout == other.AttemptTimeout
        && Equals(Output, other.Output)
        && BudgetLimits.SequenceEqual(other.BudgetLimits)
        && HookProfile.Equals(other.HookProfile)
        && Extensions.Equals(other.Extensions)
        && Equals(LaneAdmission, other.LaneAdmission);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(AgentId);
        hash.Add(SessionId);
        hash.Add(BranchId);
        hash.Add(RunId);
        hash.Add(Identity);
        hash.Add(Authorization);
        hash.Add(Agent);
        hash.Add(Configuration);
        hash.Add(SessionProfile);
        hash.Add(ModelPolicy);
        hash.Add(ModelRequirements);
        foreach (var message in Instructions)
        {
            hash.Add(message);
        }

        foreach (var tool in Tools)
        {
            hash.Add(tool);
        }

        hash.Add(ToolChoice);
        hash.Add(Settings);
        hash.Add(MaxTurns);
        hash.Add(AttemptTimeout);
        hash.Add(Output);
        foreach (var limit in BudgetLimits)
        {
            hash.Add(limit);
        }

        hash.Add(HookProfile);
        hash.Add(Extensions);
        hash.Add(LaneAdmission);
        return hash.ToHashCode();
    }

    private static AgentId GetAgentId(AgentDefinition agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return agent.Id;
    }

    /// <summary>Gets the validated in-run correlation bound by <see cref="Authorization"/>'s scope.</summary>
    /// <value>Every constructor requires the scope's correlation to be this exact kind before assignment.</value>
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
