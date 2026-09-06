// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Correlates one causal operation to the run boundary in which it
/// occurred: before any run started, while a run was active, or after the
/// causing run had already settled.
/// </summary>
/// <remarks>
/// <para>
/// This abstract record is a closed discriminated hierarchy: the only
/// concrete kinds are <see cref="BeforeRunOperationCorrelation"/>,
/// <see cref="InRunOperationCorrelation"/>, and
/// <see cref="AfterRunOperationCorrelation"/>. Consumers are expected to
/// switch over the concrete type (for example with a pattern-matching
/// switch expression) rather than adding new derived types outside this
/// package, since every downstream component that reasons about causality —
/// audit records, hooks, durable follow-up scheduling — depends on the
/// hierarchy staying closed.
/// </para>
/// <para>
/// Every derived record is an immutable value object with structural
/// equality over its fields, safe to share and compare across threads
/// without synchronization. Operations frequently outlive a single run (a
/// deferred approval requested during one run might resolve after that run
/// settles), so this correlation exists specifically to let a later
/// component say "this decision belongs to that operation" without forcing
/// every operation to pretend it happened inside exactly one run.
/// </para>
/// </remarks>
public abstract record OperationCorrelation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OperationCorrelation"/>
    /// record. This constructor is <see langword="private protected"/> so
    /// only the three derived kinds declared in this assembly can extend
    /// the hierarchy; consumers outside AgentKit.Abstractions cannot add a
    /// fourth correlation kind.
    /// </summary>
    /// <param name="operationId">The stable identity of the causal operation.</param>
    private protected OperationCorrelation(OperationId operationId) => OperationId = operationId;

    /// <summary>
    /// Gets the stable identity of the causal operation, unique across
    /// before-run, in-run, and after-run boundaries.
    /// </summary>
    public OperationId OperationId { get; init; }
}
