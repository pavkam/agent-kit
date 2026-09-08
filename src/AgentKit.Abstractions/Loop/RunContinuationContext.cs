// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures the immutable, safe-boundary evidence from which a policy may propose the next transition for one open run.</summary>
/// <remarks>
/// This snapshot identifies the installed operation, the portion of session state
/// it observed, and the causes eligible for continuation. Its values are
/// revalidation evidence only: they neither prove that a referenced record was
/// committed nor authorize a model request, session mutation, or other effect.
/// The session owner must recheck the relevant evidence before it accepts a
/// policy proposal. The instance is immutable and can safely cross an
/// asynchronous policy boundary.
/// </remarks>
public sealed record RunContinuationContext
{
    /// <summary>Initializes a continuation evaluation snapshot.</summary>
    /// <param name="agentId">The agent owning the operation.</param>
    /// <param name="sessionId">The session owning durable state.</param>
    /// <param name="executionLaneId">The lane whose installed operation is evaluated.</param>
    /// <param name="operationId">The installed operation identity.</param>
    /// <param name="runId">The open run identity.</param>
    /// <param name="state">The captured total run state.</param>
    /// <param name="operationStateRevision">The positive revision of that total state.</param>
    /// <param name="branchCursor">The exact captured branch tip.</param>
    /// <param name="inputPromotionCutoff">The latest admission sequence considered by this evaluation.</param>
    /// <param name="configurationVersion">The effective configuration version.</param>
    /// <param name="policyVersion">The immutable run-policy version.</param>
    /// <param name="boundary">The safe evaluation boundary.</param>
    /// <param name="requiredStopOutcome">A committed primary non-success stop, or null.</param>
    /// <param name="causes">All pending continuation causes in captured order.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity, revision, version, or enum is default or undefined.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="branchCursor"/> or <paramref name="boundary"/> is null.</exception>
    /// <exception cref="ArgumentException">Causes are default or contain null, a required stop is successful, or evidence correlation is inconsistent.</exception>
    public RunContinuationContext(
        AgentId agentId,
        SessionId sessionId,
        ExecutionLaneId executionLaneId,
        OperationId operationId,
        RunId runId,
        AgentRunState state,
        OperationStateRevision operationStateRevision,
        SessionBranchCursor branchCursor,
        SessionSequence inputPromotionCutoff,
        ConfigurationVersion configurationVersion,
        RunPolicyVersion policyVersion,
        RunContinuationBoundary boundary,
        AgentRunOutcome? requiredStopOutcome,
        ImmutableArray<RunContinuationCause> causes)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(executionLaneId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(operationId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(state);
        ArgumentOutOfRangeException.ThrowIfEqual(operationStateRevision, default);
        ArgumentNullException.ThrowIfNull(branchCursor);
        ArgumentOutOfRangeException.ThrowIfEqual(configurationVersion, default);
        ArgumentOutOfRangeException.ThrowIfEqual(policyVersion, default);
        ArgumentNullException.ThrowIfNull(boundary);
        ArgumentException.ThrowIfContainsNull(causes);
        if (requiredStopOutcome is not null)
        {
            ArgumentException.ThrowIfSuccessfulRunOutcome(requiredStopOutcome);
        }

        ArgumentException.ThrowIfInconsistentContinuationEvidence(
            agentId, sessionId, executionLaneId, operationId, runId,
            operationStateRevision, branchCursor, inputPromotionCutoff, boundary, causes);

        AgentId = agentId;
        SessionId = sessionId;
        ExecutionLaneId = executionLaneId;
        OperationId = operationId;
        RunId = runId;
        State = state;
        OperationStateRevision = operationStateRevision;
        BranchCursor = branchCursor;
        InputPromotionCutoff = inputPromotionCutoff;
        ConfigurationVersion = configurationVersion;
        PolicyVersion = policyVersion;
        Boundary = boundary;
        RequiredStopOutcome = requiredStopOutcome;
        Causes = causes;
    }

    /// <summary>Gets the identity of the agent definition that owns the evaluated operation.</summary>
    /// <value>A non-default identity that scopes the continuation proposal to one configured agent.</value>
    public AgentId AgentId { get; }
    /// <summary>Gets the identity of the session containing the operation's durable state.</summary>
    /// <value>A non-default session identity used with the operation and lane identities during revalidation.</value>
    public SessionId SessionId { get; }
    /// <summary>Gets the execution lane containing the installed operation being evaluated.</summary>
    /// <value>A non-default lane identity; changes in unrelated lanes do not alone stale this context.</value>
    public ExecutionLaneId ExecutionLaneId { get; }
    /// <summary>Gets the identity of the accepted operation whose next state is proposed.</summary>
    /// <value>A non-default operation identity that prevents a proposal from advancing a successor operation.</value>
    public OperationId OperationId { get; }
    /// <summary>Gets the identity of the open logical run associated with the operation.</summary>
    /// <value>A non-default run identity preserved through retries and deferred work until settlement.</value>
    public RunId RunId { get; }
    /// <summary>Gets the total durable state observed at this continuation boundary.</summary>
    /// <value>A defined state value that the session owner compares before applying a proposal.</value>
    public AgentRunState State { get; }
    /// <summary>Gets the positive revision of <see cref="State"/> observed for the installed operation.</summary>
    /// <value>A revision scoped to the operation state rather than a session-wide append version.</value>
    public OperationStateRevision OperationStateRevision { get; }
    /// <summary>Gets the exact selected-branch cursor that supplied continuation evidence.</summary>
    /// <value>A non-null immutable cursor revalidated with the operation state to detect relevant history changes.</value>
    public SessionBranchCursor BranchCursor { get; }
    /// <summary>Gets the latest admission sequence considered for promotion at this boundary.</summary>
    /// <value>The captured cutoff used to determine whether newly admitted input invalidates the proposal.</value>
    public SessionSequence InputPromotionCutoff { get; }
    /// <summary>Gets the version of effective configuration captured for this evaluation.</summary>
    /// <value>A non-default version identifying the configuration snapshot; it does not provide mutable configuration access.</value>
    public ConfigurationVersion ConfigurationVersion { get; }
    /// <summary>Gets the immutable run-policy version captured for this evaluation.</summary>
    /// <value>A non-default value identifying the policy rules under which the proposal was made.</value>
    public RunPolicyVersion PolicyVersion { get; }
    /// <summary>Gets the boundary shape that determines which turn or operation evidence is available.</summary>
    /// <value>A non-null committed-turn, retry, deferred, or idle boundary; it never fabricates a completed response.</value>
    public RunContinuationBoundary Boundary { get; }
    /// <summary>Gets the primary committed non-success stop outcome, when one already prevents continuation.</summary>
    /// <value>A non-success outcome, or <see langword="null"/> when no required stop was observed.</value>
    public AgentRunOutcome? RequiredStopOutcome { get; }
    /// <summary>Gets every pending continuation cause in the captured precedence-preserving order.</summary>
    /// <value>A non-default immutable array with no null entries; these causes are evidence, not requests the policy may execute.</value>
    public ImmutableArray<RunContinuationCause> Causes { get; }
}
