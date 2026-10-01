// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Describes why a result store refused an operation, with a safe message that carries no result content.</summary>
public sealed record EvaluationStoreFailure
{
    /// <summary>Initializes a validated failure.</summary>
    /// <param name="kind">The failure class.</param>
    /// <param name="safeMessage">The non-blank content-free explanation.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public EvaluationStoreFailure(EvaluationStoreFailureKind kind, string safeMessage)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        Kind = kind;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the failure class.</summary>
    public EvaluationStoreFailureKind Kind { get; }

    /// <summary>Gets the content-free explanation.</summary>
    public string SafeMessage { get; }
}
