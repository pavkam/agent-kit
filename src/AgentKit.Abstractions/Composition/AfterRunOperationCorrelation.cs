// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Correlates an operation, such as a durable follow-up action or a
/// deferred approval resolution, that occurred after its causing run had
/// already settled.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share and compare across threads without
/// synchronization. Some effects legitimately happen after a run is
/// completely finished — a scheduled retry, a human approval decided hours
/// later, a durable goal continuing independently — and this correlation
/// kind is what lets those later effects still point back to the run that
/// originally caused them, distinct from an
/// <see cref="InRunOperationCorrelation"/> whose run is still active.
/// </remarks>
public sealed record AfterRunOperationCorrelation: OperationCorrelation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AfterRunOperationCorrelation"/>
    /// record.
    /// </summary>
    /// <param name="operationId">The stable identity of the causal operation.</param>
    /// <param name="causalRunId">The already-settled run that caused this later operation.</param>
    public AfterRunOperationCorrelation(OperationId operationId, RunId causalRunId)
        : base(operationId) => CausalRunId = causalRunId;

    /// <summary>Gets the already-settled run that caused this later operation.</summary>
    public RunId CausalRunId { get; init; }
}
