// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that no answer arrived before the question deadline.</summary>
public sealed record HumanQuestionTimedOut: HumanQuestionResult
{
    /// <summary>Initializes a timed-out result.</summary>
    /// <param name="questionId">The unsettled question.</param>
    public HumanQuestionTimedOut(QuestionId questionId)
        : base(questionId)
    {
    }
}
