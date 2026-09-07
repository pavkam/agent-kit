// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that the configured interaction channel cannot accept or resolve the question.</summary>
public sealed record HumanQuestionUnavailable: HumanQuestionResult
{
    /// <summary>Initializes an unavailable result with safe user-facing detail.</summary>
    /// <param name="questionId">The unsettled question.</param>
    /// <param name="safeMessage">A non-sensitive explanation suitable for model projection.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public HumanQuestionUnavailable(QuestionId questionId, string safeMessage)
        : base(questionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the safe user-facing explanation.</summary>
    public string SafeMessage { get; init; }
}
