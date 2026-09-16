// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;

/// <summary>Supplies immutable reservation identity evidence without implementing budget behavior.</summary>
internal sealed class TestBudgetReservation: IBudgetReservation
{
    /// <inheritdoc/>
    public BudgetReservationId Id { get; } = new(Guid.Parse("90000000-0000-0000-0000-000000000009"));

    /// <inheritdoc/>
    public BudgetScopeId ScopeId { get; } = new(Guid.Parse("a0000000-0000-0000-0000-00000000000a"));

    /// <inheritdoc/>
    public BudgetDimension Dimension { get; } = new("tests.requests");

    /// <inheritdoc/>
    public decimal Reserved => 1m;

    /// <inheritdoc/>
    public ValueTask<BudgetStartResult> MarkStartedAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<BudgetCommitResult> CommitAsync(decimal actual, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<BudgetCorrectionResult> CorrectAsync(decimal correctedActual, long revision, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
