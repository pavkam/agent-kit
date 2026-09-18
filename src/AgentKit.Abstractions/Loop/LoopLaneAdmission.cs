// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the durable execution lane an admission boundary accepted this run on.</summary>
/// <remarks>
/// <para>
/// When present on an <see cref="AgentRunRequest"/>, this bundle tells the loop it is driving a run that a
/// caller already admitted through the session lane protocol (<c>ProvisionLaneAsync</c>, <c>AdmitInputAsync</c>,
/// <c>AcceptRunAsync</c>), rather than one whose only durable record is whatever the loop itself appends. The loop
/// uses <see cref="ExecutionLaneId"/> for every session operation it performs so its commits advance the same
/// lane the acceptance installed, uses <see cref="AcceptedCorrelation"/>'s turn identity for the run's first turn
/// instead of minting an unrelated one, and releases the lane through <c>ReleaseRunAsync</c> using
/// <see cref="OperationStateRevision"/> once the run settles.
/// </para>
/// <para>
/// This is deliberately narrow evidence, not the fuller <c>SessionExecutionCapability</c>-carrying
/// <c>AgentRunServices</c> the agent-runtime architecture describes: it names the lane and the state it was
/// installed with, and leaves the loop to capture its own authorization for the release operation, exactly as it
/// already does for every other in-run operation.
/// </para>
/// </remarks>
public sealed record LoopLaneAdmission
{
    /// <summary>Initializes lane-admission evidence.</summary>
    /// <param name="executionLaneId">The non-default lane the loop's commits must advance.</param>
    /// <param name="acceptedCorrelation">
    /// The exact in-run correlation the admission boundary used to accept this run, whose non-null
    /// <see cref="InRunOperationCorrelation.TurnId"/> names the run's first turn.
    /// </param>
    /// <param name="operationStateRevision">The positive total-state revision installed by acceptance.</param>
    /// <exception cref="ArgumentNullException"><paramref name="acceptedCorrelation"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="executionLaneId"/> or <paramref name="operationStateRevision"/> is default.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="acceptedCorrelation"/> carries no turn identity.</exception>
    public LoopLaneAdmission(
        ExecutionLaneId executionLaneId,
        InRunOperationCorrelation acceptedCorrelation,
        OperationStateRevision operationStateRevision)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(executionLaneId, default);
        ArgumentNullException.ThrowIfNull(acceptedCorrelation);
        ArgumentException.ThrowIfNotEqual(acceptedCorrelation.TurnId.HasValue, true, nameof(acceptedCorrelation));
        ArgumentOutOfRangeException.ThrowIfEqual(operationStateRevision, default);
        ExecutionLaneId = executionLaneId;
        AcceptedCorrelation = acceptedCorrelation;
        OperationStateRevision = operationStateRevision;
    }

    /// <summary>Gets the lane the loop's commits must advance.</summary>
    /// <value>The non-default lane identity installed by acceptance.</value>
    public ExecutionLaneId ExecutionLaneId { get; }

    /// <summary>Gets the exact correlation the admission boundary accepted this run with.</summary>
    /// <value>An in-run correlation whose <see cref="InRunOperationCorrelation.TurnId"/> names the first turn.</value>
    public InRunOperationCorrelation AcceptedCorrelation { get; }

    /// <summary>Gets the total-state revision installed by acceptance.</summary>
    /// <value>The positive revision the loop presents unchanged when releasing the lane.</value>
    public OperationStateRevision OperationStateRevision { get; }
}
