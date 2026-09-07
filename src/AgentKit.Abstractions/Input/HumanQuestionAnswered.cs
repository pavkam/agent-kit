// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that the question received one authenticated answer.</summary>
public sealed record HumanQuestionAnswered: HumanQuestionResult
{
    /// <summary>Initializes an answered result.</summary>
    /// <param name="questionId">The settled question.</param>
    /// <param name="answer">The authenticated answer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="answer"/> is null.</exception>
    public HumanQuestionAnswered(QuestionId questionId, HumanQuestionAnswer answer)
        : base(questionId)
    {
        ArgumentNullException.ThrowIfNull(answer);
        Answer = answer;
    }

    /// <summary>Gets the authenticated answer.</summary>
    public HumanQuestionAnswer Answer { get; init; }
}
