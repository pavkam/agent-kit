// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A message representing a committed model response. A streaming response
/// becomes an <see cref="AssistantMessage"/> only after its terminal event,
/// content parts, usage, and stop reason all validate; an interrupted or
/// malformed stream is never presented as one.
/// </summary>
/// <remarks>
/// The committing component always populates <see cref="AgentMessage.RunId"/>
/// and <see cref="AgentMessage.TurnId"/> for this kind because an assistant
/// response only ever occurs while a run's turn is active.
/// </remarks>
public sealed record AssistantMessage: AgentMessage
{
    /// <summary>Initializes a new instance of the <see cref="AssistantMessage"/> record.</summary>
    /// <param name="id">The stable identity of this message.</param>
    /// <param name="agentId">The agent that owns the session this message belongs to.</param>
    /// <param name="sessionId">The session that durably owns this message.</param>
    /// <param name="conversationId">
    /// The optional higher-level conversation grouping this message's
    /// session.
    /// </param>
    /// <param name="branchId">The session branch this message belongs to.</param>
    /// <param name="runId">The run that produced this response.</param>
    /// <param name="turnId">The turn that produced this response.</param>
    /// <param name="createdAt">
    /// The commit time from the injected <see cref="TimeProvider"/>.
    /// </param>
    /// <param name="state">The completion state of this message.</param>
    /// <param name="parts">The ordered, typed content of this response.</param>
    /// <param name="response">
    /// Provider, usage, and stop-reason metadata for the committed
    /// response.
    /// </param>
    /// <param name="extensions">Provider-specific or forward-compatible data.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="response"/> or <paramref name="extensions"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="parts"/> is a default, uninitialized array or contains a null element.
    /// </exception>
    public AssistantMessage(
        MessageId id,
        AgentId agentId,
        SessionId sessionId,
        ConversationId? conversationId,
        BranchId branchId,
        RunId? runId,
        TurnId? turnId,
        DateTimeOffset createdAt,
        MessageState state,
        ImmutableArray<ContentPart> parts,
        AssistantResponseMetadata response,
        ExtensionData extensions)
        : base(
            id,
            agentId,
            sessionId,
            conversationId,
            branchId,
            runId,
            turnId,
            createdAt,
            state,
            parts,
            extensions)
    {
        ArgumentNullException.ThrowIfNull(response);
        Response = response;
    }

    /// <summary>
    /// Gets provider, usage, and stop-reason metadata for the committed
    /// response.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// The value assigned during initialization or non-destructive mutation is null.
    /// </exception>
    public AssistantResponseMetadata Response
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }
}
