// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Persists and reads recorded evaluation case results.</summary>
/// <remarks>
/// <para>
/// A result is identified by its evaluation run, case ordinal, and repetition. Appending is idempotent: repeating an identical
/// result replays the original acknowledgement, while a different result for the same identity, a second plan identity for the
/// same run, or a second case for the same ordinal is rejected as an identity conflict. The first result of a run pins that
/// run to one plan identity and version.
/// </para>
/// <para>
/// Reads page through one run ordered by case ordinal then repetition, so order never depends on append order. A store claims
/// only the guarantees its adapter proves: in-memory results are ephemeral, SQLite and JSON results survive reopen, and none
/// implies distributed leases, fencing, or atomicity with report exporters. Implementations are thread-safe.
/// </para>
/// </remarks>
public interface IEvaluationResultStore
{
    /// <summary>Appends one recorded result.</summary>
    /// <param name="result">The result to record.</param>
    /// <param name="cancellationToken">Cancels before the result is written.</param>
    /// <returns>An acknowledgement that distinguishes a fresh append from an idempotent replay, or a typed rejection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled before any state was written.</exception>
    public ValueTask<EvaluationStoreResult> AppendAsync(EvaluationCaseResult result, CancellationToken cancellationToken = default);

    /// <summary>Reads one page of the results recorded for an evaluation run.</summary>
    /// <param name="query">The run, page size, and continuation cursor.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A page ordered by case ordinal then repetition, empty for an unknown run, or a typed rejection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public ValueTask<EvaluationReadResult> ReadAsync(EvaluationResultQuery query, CancellationToken cancellationToken = default);
}
