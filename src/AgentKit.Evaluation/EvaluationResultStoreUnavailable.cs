// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Reports that no store is registered under the requested key.</summary>
public sealed record EvaluationResultStoreUnavailable: EvaluationResultStoreSelection
{
    /// <summary>Initializes the outcome.</summary>
    /// <param name="key">The requested key.</param>
    /// <param name="safeMessage">The non-blank content-free explanation.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> or <paramref name="safeMessage"/> is blank.</exception>
    public EvaluationResultStoreUnavailable(EvaluationResultStoreKey key, string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        Key = key;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the requested key.</summary>
    public EvaluationResultStoreKey Key { get; }

    /// <summary>Gets the content-free explanation.</summary>
    public string SafeMessage { get; }
}
