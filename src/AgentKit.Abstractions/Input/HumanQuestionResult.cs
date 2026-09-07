// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents the closed result vocabulary returned by a human-question broker.</summary>
public abstract record HumanQuestionResult
{
    private protected HumanQuestionResult(QuestionId questionId) => QuestionId = questionId;

    /// <summary>Gets the question identity this result settles.</summary>
    public QuestionId QuestionId { get; init; }
}
