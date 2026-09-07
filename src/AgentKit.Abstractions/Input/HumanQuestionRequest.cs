// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries one exact authorized question from a tool to an application-owned human interaction channel.</summary>
public sealed record HumanQuestionRequest
{
    /// <summary>Initializes a complete human-question publication request.</summary>
    /// <param name="id">The stable question identity.</param>
    /// <param name="agentId">The asking agent.</param>
    /// <param name="sessionId">The containing session, when one exists.</param>
    /// <param name="toolCallId">The causing tool call.</param>
    /// <param name="correlation">The causal operation.</param>
    /// <param name="identity">The authenticated identity asking the question.</param>
    /// <param name="prompt">The user-facing question.</param>
    /// <param name="options">Between two and ten mutually exclusive options with unique identities.</param>
    /// <param name="allowsFreeText">Whether an answer may supplement its selected option with free text.</param>
    /// <param name="deadline">The exclusive deadline for an answer.</param>
    /// <param name="grant">The exact single-use grant the broker must consume before publication.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="ArgumentException">The prompt is blank, options are uninitialized, contain null, or duplicate identities.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The option count is outside the supported range.</exception>
    public HumanQuestionRequest(
        QuestionId id,
        AgentId agentId,
        SessionId? sessionId,
        ToolCallId toolCallId,
        OperationCorrelation correlation,
        ExecutionIdentity identity,
        string prompt,
        ImmutableArray<HumanQuestionOption> options,
        bool allowsFreeText,
        DateTimeOffset deadline,
        SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(correlation);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentException.ThrowIfContainsNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Length, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.Length, 10);
        ArgumentException.ThrowIfDuplicateQuestionOptionIds(options);
        ArgumentNullException.ThrowIfNull(grant);

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
        Grant = grant;
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
    /// <summary>Gets the authenticated identity asking the question.</summary>
    public ExecutionIdentity Identity { get; init; }
    /// <summary>Gets the user-facing question.</summary>
    public string Prompt { get; init; }
    /// <summary>Gets the mutually exclusive answer options.</summary>
    public ImmutableArray<HumanQuestionOption> Options { get; init; }
    /// <summary>Gets whether an answer may include supplementary free text.</summary>
    public bool AllowsFreeText { get; init; }
    /// <summary>Gets the exclusive response deadline.</summary>
    public DateTimeOffset Deadline { get; init; }
    /// <summary>Gets the exact grant the broker must consume before publishing.</summary>
    public SecurityGrant Grant { get; init; }

    /// <inheritdoc/>
    public bool Equals(HumanQuestionRequest? other) =>
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
        && Deadline == other.Deadline
        && Grant.Equals(other.Grant);

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
        hash.Add(Grant);
        return hash.ToHashCode();
    }
}
