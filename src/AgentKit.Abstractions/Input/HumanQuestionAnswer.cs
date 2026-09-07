// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents one authenticated human resolution of a question.</summary>
public sealed record HumanQuestionAnswer
{
    /// <summary>Initializes an immutable human answer.</summary>
    /// <param name="selectedOptionId">The single selected option.</param>
    /// <param name="freeText">Optional supplementary text.</param>
    /// <param name="respondent">The authenticated responding identity.</param>
    /// <param name="answeredAt">The response timestamp.</param>
    /// <exception cref="ArgumentNullException"><paramref name="respondent"/> is null.</exception>
    public HumanQuestionAnswer(
        QuestionOptionId selectedOptionId,
        string? freeText,
        ExecutionIdentity respondent,
        DateTimeOffset answeredAt)
    {
        ArgumentNullException.ThrowIfNull(respondent);
        SelectedOptionId = selectedOptionId;
        FreeText = freeText;
        Respondent = respondent;
        AnsweredAt = answeredAt;
    }

    /// <summary>Gets the single selected option.</summary>
    public QuestionOptionId SelectedOptionId { get; init; }
    /// <summary>Gets optional supplementary text.</summary>
    public string? FreeText { get; init; }
    /// <summary>Gets the authenticated responding identity.</summary>
    public ExecutionIdentity Respondent { get; init; }
    /// <summary>Gets the response timestamp.</summary>
    public DateTimeOffset AnsweredAt { get; init; }
}
