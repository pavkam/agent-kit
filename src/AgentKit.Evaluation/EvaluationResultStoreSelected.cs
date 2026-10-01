// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Carries the store selected for a key.</summary>
/// <remarks>The selector retains ownership of the store; the caller borrows it for the operation and never disposes it.</remarks>
public sealed record EvaluationResultStoreSelected: EvaluationResultStoreSelection
{
    /// <summary>Initializes the selection.</summary>
    /// <param name="key">The key the store was selected under.</param>
    /// <param name="store">The borrowed store.</param>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
    public EvaluationResultStoreSelected(EvaluationResultStoreKey key, IEvaluationResultStore store)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(store);
        Key = key;
        Store = store;
    }

    /// <summary>Gets the key the store was selected under.</summary>
    public EvaluationResultStoreKey Key { get; }

    /// <summary>Gets the borrowed store.</summary>
    public IEvaluationResultStore Store { get; }
}
