// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes one host-presentable, mutually exclusive human answer option.</summary>
public sealed record HumanQuestionOption
{
    /// <summary>Initializes one question option.</summary>
    /// <param name="id">The stable option identity returned by an answer.</param>
    /// <param name="label">The concise user-facing label.</param>
    /// <param name="description">The user-facing consequence or trade-off.</param>
    /// <exception cref="ArgumentException"><paramref name="label"/> or <paramref name="description"/> is blank.</exception>
    public HumanQuestionOption(QuestionOptionId id, string label, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Id = id;
        Label = label;
        Description = description;
    }

    /// <summary>Gets the stable option identity.</summary>
    public QuestionOptionId Id { get; init; }

    /// <summary>Gets the concise user-facing label.</summary>
    public string Label { get; init; }

    /// <summary>Gets the user-facing consequence or trade-off.</summary>
    public string Description { get; init; }
}
