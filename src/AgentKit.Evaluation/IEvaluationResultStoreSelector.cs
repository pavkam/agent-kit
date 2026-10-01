// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Resolves the result store a plan names, by its explicit key.</summary>
/// <remarks>Selection never depends on registration order and never falls back to another store. The first-party selector resolves the keyed registration made by <c>AddEvaluationResultStore</c> or an adapter package.</remarks>
public interface IEvaluationResultStoreSelector
{
    /// <summary>Selects the store registered under a key.</summary>
    /// <param name="key">The non-blank store key.</param>
    /// <param name="cancellationToken">Cancels the selection.</param>
    /// <returns>The selected store, or a typed unavailable outcome when none is registered.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public ValueTask<EvaluationResultStoreSelection> SelectAsync(EvaluationResultStoreKey key, CancellationToken cancellationToken = default);
}
