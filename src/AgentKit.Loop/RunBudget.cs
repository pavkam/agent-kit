// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

/// <summary>
/// One run's budget scope and the loop's reservation discipline over it: reserve before an attempt, mark it started,
/// and commit the counted amount, mapping a refusal to a typed exhaustion.
/// </summary>
/// <remarks>
/// <para>
/// The loop counts attempts (turns, model requests, tool calls) at the moment it commits to them and accounts
/// provider-reported tokens and cost after each response. An attempt that is refused settles the run as a typed
/// <see cref="BudgetExhaustion"/>; usage that arrives after the fact is reserved and committed in one step so an
/// overrun is recorded fully and further work is refused by the authority's captured overrun policy.
/// </para>
/// <para>
/// This type is internal to the loop and holds a borrowed <see cref="IBudgetScope"/>; the authority owns the scope's
/// lifetime and reconciliation. Pre-effect estimation of unknown token cost is not attempted here.
/// </para>
/// </remarks>
internal sealed class RunBudget
{
    private static readonly BudgetUnit _count = new("count");
    private static readonly BudgetUnit _tokens = new("tokens");
    private static readonly BudgetUnit _usd = new("usd");

    private readonly IBudgetScope _scope;
    private readonly RunId _runId;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes the run budget over a created scope.</summary>
    /// <param name="scope">The run's budget scope.</param>
    /// <param name="runId">The run the scope belongs to, used to derive idempotency keys.</param>
    /// <param name="timeProvider">The clock used to bound reservations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="scope"/> or <paramref name="timeProvider"/> is null.</exception>
    public RunBudget(IBudgetScope scope, RunId runId, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _scope = scope;
        _runId = runId;
        _timeProvider = timeProvider;
    }

    /// <summary>Gets the underlying scope identity.</summary>
    public BudgetScopeId ScopeId => _scope.Id;

    /// <summary>Counts one attempt on a count dimension before the loop commits to it.</summary>
    /// <param name="dimension">The count dimension, such as <see cref="BudgetDimensions.Turns"/>.</param>
    /// <param name="operationId">The operation the attempt belongs to.</param>
    /// <param name="key">A stable suffix that makes the reservation idempotent for this attempt.</param>
    /// <param name="cancellationToken">Cancels the reservation; cancellation propagates.</param>
    /// <returns><see langword="null"/> when the attempt may proceed, otherwise the exhaustion the caller settles or reports with.</returns>
    public ValueTask<BudgetExhaustion?> CountAsync(
        BudgetDimension dimension, OperationId operationId, string key, CancellationToken cancellationToken) =>
        ReserveAndCommitAsync(dimension, _count, 1m, operationId, key, SideEffectCertainty.DefinitelyNotPerformed, cancellationToken);

    /// <summary>Accounts provider-reported usage for one response after the fact.</summary>
    /// <param name="usage">The reported usage; unreported dimensions are skipped, never estimated as zero.</param>
    /// <param name="operationId">The operation the response belongs to.</param>
    /// <param name="key">A stable suffix identifying the response.</param>
    /// <param name="cancellationToken">Cancels the accounting; cancellation propagates.</param>
    /// <returns><see langword="null"/> when every dimension fit, otherwise the first exhaustion, which stops further work.</returns>
    public async ValueTask<BudgetExhaustion?> AccountUsageAsync(
        ModelUsage usage, OperationId operationId, string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(usage);
        if (usage.ReportState == ModelUsageReportState.NotReported)
        {
            return null;
        }

        // The provider request that produced this usage already completed by the time it is accounted for.
        const SideEffectCertainty certainty = SideEffectCertainty.DefinitelyPerformed;
        BudgetExhaustion? exhausted = null;
        if (usage.InputTokens is > 0 and var input)
        {
            exhausted ??= await ReserveAndCommitAsync(BudgetDimensions.InputTokens, _tokens, input, operationId, $"{key}:input", certainty, cancellationToken).ConfigureAwait(false);
        }

        if (usage.OutputTokens is > 0 and var output)
        {
            exhausted ??= await ReserveAndCommitAsync(BudgetDimensions.OutputTokens, _tokens, output, operationId, $"{key}:output", certainty, cancellationToken).ConfigureAwait(false);
        }

        if (usage.ReasoningTokens is > 0 and var reasoning)
        {
            exhausted ??= await ReserveAndCommitAsync(BudgetDimensions.ReasoningTokens, _tokens, reasoning, operationId, $"{key}:reasoning", certainty, cancellationToken).ConfigureAwait(false);
        }

        if (usage.EstimatedCost is > 0 and var cost && string.Equals(usage.CostCurrency, "USD", StringComparison.OrdinalIgnoreCase))
        {
            exhausted ??= await ReserveAndCommitAsync(BudgetDimensions.Cost, _usd, cost, operationId, $"{key}:cost", certainty, cancellationToken).ConfigureAwait(false);
        }

        return exhausted;
    }

    private async ValueTask<BudgetExhaustion?> ReserveAndCommitAsync(
        BudgetDimension dimension,
        BudgetUnit unit,
        decimal amount,
        OperationId operationId,
        string key,
        SideEffectCertainty sideEffectCertainty,
        CancellationToken cancellationToken)
    {
        Debug.Assert(amount > 0, "Callers only account positive amounts.");
        var result = await _scope.ReserveAsync(
            new BudgetReservationRequest(
                _scope.Id, dimension, amount, unit, operationId,
                _timeProvider.GetUtcNow().AddMinutes(5),
                new IdempotencyKey($"run:{_runId}:{dimension.Value}:{key}")),
            cancellationToken).ConfigureAwait(false);
        switch (result)
        {
            case BudgetReserved reserved:
                _ = await reserved.Reservation.MarkStartedAsync(cancellationToken).ConfigureAwait(false);
                _ = await reserved.Reservation.CommitAsync(amount, cancellationToken).ConfigureAwait(false);
                return null;
            case BudgetRejected rejected:
                return new BudgetExhaustion(rejected.Failure.Dimension, rejected.Failure.SafeMessage, rejected.Failure, sideEffectCertainty);
            case BudgetHeld held:
                return new BudgetExhaustion(
                    held.Holds[0].Dimension,
                    $"The {held.Holds[0].Dimension.Value} budget overran and further reservations are held.",
                    Failure: null, sideEffectCertainty);
            default:
                return new BudgetExhaustion(
                    dimension, $"The budget authority returned an unsupported reservation outcome for {dimension.Value}.",
                    Failure: null, sideEffectCertainty);
        }
    }
}

/// <summary>
/// Names one budget dimension's exhaustion: the dimension and safe message every refusal carries, plus the
/// full <see cref="BudgetLimitFailure"/> evidence when the authority rejected the reservation outright.
/// </summary>
/// <param name="Dimension">The dimension that ran out.</param>
/// <param name="SafeMessage">A content-safe description of the exhaustion.</param>
/// <param name="Failure">The full limit evidence, when the reservation was rejected outright; otherwise <see langword="null"/>.</param>
/// <param name="SideEffectCertainty">What is known about the relevant external effect the refused attempt would have made.</param>
/// <remarks>
/// A <see cref="BudgetHeld"/> or unsupported reservation outcome carries only a dimension and a message, with no
/// scope, kind, configured value, or observed value to build a truthful <see cref="BudgetLimitFailure"/> from; this
/// type keeps that distinction visible to its caller instead of fabricating evidence the authority never reported.
/// </remarks>
internal sealed record BudgetExhaustion(BudgetDimension Dimension, string SafeMessage, BudgetLimitFailure? Failure, SideEffectCertainty SideEffectCertainty);
