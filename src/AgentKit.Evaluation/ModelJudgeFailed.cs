// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Reports that a judge client produced no reply for a sample.</summary>
public sealed record ModelJudgeFailed: ModelJudgeResponse
{
    /// <summary>Initializes the failure.</summary>
    /// <param name="kind">The failure class.</param>
    /// <param name="safeMessage">The non-blank content-free explanation.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public ModelJudgeFailed(ModelJudgeFailureKind kind, string safeMessage)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        Kind = kind;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the failure class.</summary>
    public ModelJudgeFailureKind Kind { get; }

    /// <summary>Gets the content-free explanation.</summary>
    public string SafeMessage { get; }
}
