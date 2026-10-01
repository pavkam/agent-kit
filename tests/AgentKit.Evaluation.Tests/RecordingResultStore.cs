// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

using System.Collections.Concurrent;

/// <summary>An <see cref="IEvaluationResultStore"/> that records appends and optionally scripts the answer.</summary>
internal sealed class RecordingResultStore: IEvaluationResultStore
{
    private readonly ConcurrentQueue<EvaluationCaseResult> _appended = new();

    /// <summary>Gets or sets a factory that replaces the answer to an append, or <see langword="null"/> to acknowledge it.</summary>
    public Func<EvaluationCaseResult, CancellationToken, ValueTask<EvaluationStoreResult>>? OnAppend { get; set; }

    /// <summary>Gets every result appended, in call order.</summary>
    public IReadOnlyCollection<EvaluationCaseResult> Appended => _appended;

    /// <summary>Gets every cancellation token an append received.</summary>
    public ConcurrentQueue<CancellationToken> Tokens { get; } = new();

    /// <inheritdoc/>
    public ValueTask<EvaluationStoreResult> AppendAsync(EvaluationCaseResult result, CancellationToken cancellationToken = default)
    {
        _appended.Enqueue(result);
        Tokens.Enqueue(cancellationToken);
        return OnAppend is { } script
            ? script(result, cancellationToken)
            : ValueTask.FromResult<EvaluationStoreResult>(
                new EvaluationStoreAppended(new EvaluationResultReceipt(result.EvaluationRunId, result.CaseId, result.CaseOrdinal, result.Repetition), false));
    }

    /// <inheritdoc/>
    public ValueTask<EvaluationReadResult> ReadAsync(EvaluationResultQuery query, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<EvaluationReadResult>(new EvaluationResultsRead([.. _appended], null));
}
