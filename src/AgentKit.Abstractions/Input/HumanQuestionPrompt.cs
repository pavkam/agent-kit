// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Projects an authorized question to an application channel without exposing its security grant.</summary>
public sealed record HumanQuestionPrompt
{
    /// <summary>Initializes one grant-free application-channel projection.</summary>
    /// <param name="id">The stable question identity.</param>
    /// <param name="agentId">The asking agent.</param>
    /// <param name="sessionId">The containing session, when one exists.</param>
    /// <param name="toolCallId">The causing tool call.</param>
    /// <param name="correlation">The causal operation.</param>
    /// <param name="identity">The authenticated asking identity.</param>
    /// <param name="prompt">The user-facing question.</param>
    /// <param name="options">The mutually exclusive options.</param>
    /// <param name="allowsFreeText">Whether supplementary response text is accepted.</param>
    /// <param name="deadline">The exclusive response deadline.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="ArgumentException">The prompt or option array is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The option count is outside two through ten.</exception>
    public HumanQuestionPrompt(
        QuestionId id,
        AgentId agentId,
        SessionId? sessionId,
        ToolCallId toolCallId,
        OperationCorrelation correlation,
        ExecutionIdentity identity,
        string prompt,
        ImmutableArray<HumanQuestionOption> options,
        bool allowsFreeText,
        DateTimeOffset deadline)
    {
        ArgumentNullException.ThrowIfNull(correlation);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentException.ThrowIfContainsNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Length, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.Length, 10);
        ArgumentException.ThrowIfDuplicateQuestionOptionIds(options);
        Id = id;
        AgentId = agentId;
        SessionId = sessionId;
        ToolCallId = toolCallId;
        Correlation = correlation;
        Identity = identity;
        Prompt = prompt;
        Options = options;
        AllowsFreeText = allowsFreeText;
        Deadline = deadline;
    }

    /// <summary>Gets the stable question identity.</summary>
    public QuestionId Id { get; init; }
    /// <summary>Gets the asking agent.</summary>
    public AgentId AgentId { get; init; }
    /// <summary>Gets the containing session, when one exists.</summary>
    public SessionId? SessionId { get; init; }
    /// <summary>Gets the causing tool call.</summary>
    public ToolCallId ToolCallId { get; init; }
    /// <summary>Gets the causal operation.</summary>
    public OperationCorrelation Correlation { get; init; }
    /// <summary>Gets the authenticated asking identity.</summary>
    public ExecutionIdentity Identity { get; init; }
    /// <summary>Gets the user-facing question.</summary>
    public string Prompt { get; init; }
    /// <summary>Gets the mutually exclusive options.</summary>
    public ImmutableArray<HumanQuestionOption> Options { get; init; }
    /// <summary>Gets whether supplementary response text is accepted.</summary>
    public bool AllowsFreeText { get; init; }
    /// <summary>Gets the exclusive response deadline.</summary>
    public DateTimeOffset Deadline { get; init; }

    /// <inheritdoc/>
    public bool Equals(HumanQuestionPrompt? other) =>
        other is not null
        && Id == other.Id
        && AgentId == other.AgentId
        && SessionId == other.SessionId
        && ToolCallId == other.ToolCallId
        && Correlation.Equals(other.Correlation)
        && Identity.Equals(other.Identity)
        && Prompt == other.Prompt
        && Options.SequenceEqual(other.Options)
        && AllowsFreeText == other.AllowsFreeText
        && Deadline == other.Deadline;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(AgentId);
        hash.Add(SessionId);
        hash.Add(ToolCallId);
        hash.Add(Correlation);
        hash.Add(Identity);
        hash.Add(Prompt);
        foreach (var option in Options)
        {
            hash.Add(option);
        }

        hash.Add(AllowsFreeText);
        hash.Add(Deadline);
        return hash.ToHashCode();
    }
}
