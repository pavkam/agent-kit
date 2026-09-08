// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures every immutable observation needed to propose one continuation transition.</summary>
/// <remarks>The values are revalidation evidence, not proof of commit or authority. The session owner compares them before accepting a proposal.</remarks>
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

    /// <summary>Gets the owning agent identity.</summary>
    public AgentId AgentId { get; }
    /// <summary>Gets the owning session identity.</summary>
    public SessionId SessionId { get; }
    /// <summary>Gets the execution lane identity.</summary>
    public ExecutionLaneId ExecutionLaneId { get; }
    /// <summary>Gets the installed operation identity.</summary>
    public OperationId OperationId { get; }
    /// <summary>Gets the open run identity.</summary>
    public RunId RunId { get; }
    /// <summary>Gets the captured total run state.</summary>
    public AgentRunState State { get; }
    /// <summary>Gets the captured operation-state revision.</summary>
    public OperationStateRevision OperationStateRevision { get; }
    /// <summary>Gets the exact branch cursor.</summary>
    public SessionBranchCursor BranchCursor { get; }
    /// <summary>Gets the admission cutoff already considered.</summary>
    public SessionSequence InputPromotionCutoff { get; }
    /// <summary>Gets the effective configuration version.</summary>
    public ConfigurationVersion ConfigurationVersion { get; }
    /// <summary>Gets the selected run-policy version.</summary>
    public RunPolicyVersion PolicyVersion { get; }
    /// <summary>Gets the safe evaluation boundary.</summary>
    public RunContinuationBoundary Boundary { get; }
    /// <summary>Gets the committed primary stop outcome, when present.</summary>
    public AgentRunOutcome? RequiredStopOutcome { get; }
    /// <summary>Gets every pending continuation cause in captured order.</summary>
    public ImmutableArray<RunContinuationCause> Causes { get; }
}
