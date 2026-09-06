// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable, provider-neutral envelope shared by every durable message
/// kind: <see cref="SystemMessage"/>, <see cref="DeveloperMessage"/>,
/// <see cref="UserMessage"/>, <see cref="AssistantMessage"/>,
/// <see cref="ToolMessage"/>, and <see cref="RuntimeMessage"/>. Concrete
/// kinds preserve distinct role semantics; the core never flattens them
/// into one role-plus-string shape.
/// </summary>
/// <remarks>
/// <para>
/// This is a closed discriminated hierarchy: its constructor is
/// <see langword="private protected"/>, so no assembly outside
/// AgentKit.Abstractions can introduce a seventh message kind. Every
/// concrete kind keeps <see cref="RunId"/> and <see cref="TurnId"/> at this
/// exact nullable type; a derived positional record cannot narrow an
/// inherited property's type, and in any case some kinds
/// (<see cref="SystemMessage"/>, <see cref="DeveloperMessage"/>) legitimately
/// have no run/turn because they may be recorded before any run exists. The
/// kinds that only ever occur inside a run
/// (<see cref="UserMessage"/>, <see cref="AssistantMessage"/>,
/// <see cref="ToolMessage"/>, <see cref="RuntimeMessage"/>) are always
/// constructed with non-null values by the component that commits them;
/// that is an enforced construction invariant of the owning component, not
/// a difference visible in the CLR shape.
/// </para>
/// <para>
/// Every message and everything it references transitively is deeply
/// immutable and safe to share across threads without synchronization.
/// Durable history is append-only, so once a message is committed with a
/// given <see cref="Id"/>, that instance's content never changes; a repair,
/// summary, or branch operation produces new messages or a new working
/// view rather than mutating this one.
/// </para>
/// </remarks>
public abstract record AgentMessage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentMessage"/> record.
    /// This constructor is <see langword="private protected"/> so only the
    /// closed set of message kinds declared in this assembly can extend the
    /// hierarchy.
    /// </summary>
    /// <param name="id">The stable identity of this message.</param>
    /// <param name="agentId">The agent that owns the session this message belongs to.</param>
    /// <param name="sessionId">The session that durably owns this message.</param>
    /// <param name="conversationId">
    /// The optional higher-level conversation grouping this message's
    /// session.
    /// </param>
    /// <param name="branchId">The session branch this message belongs to.</param>
    /// <param name="runId">
    /// The run that produced this message, when the message occurred during
    /// a run.
    /// </param>
    /// <param name="turnId">
    /// The turn that produced this message, when the message occurred
    /// during a run.
    /// </param>
    /// <param name="createdAt">
    /// The commit time from the injected <see cref="TimeProvider"/>.
    /// </param>
    /// <param name="state">The completion state of this message.</param>
    /// <param name="parts">The ordered, typed content of this message.</param>
    /// <param name="extensions">Provider-specific or forward-compatible data.</param>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="parts"/> is a default, uninitialized array.
    /// </exception>
    private protected AgentMessage(
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
        ExtensionData extensions)
    {
        ArgumentException.ThrowIfDefault(parts);
        ArgumentNullException.ThrowIfNull(extensions);

        Id = id;
        AgentId = agentId;
        SessionId = sessionId;
        ConversationId = conversationId;
        BranchId = branchId;
        RunId = runId;
        TurnId = turnId;
        CreatedAt = createdAt;
        State = state;
        Parts = parts;
        Extensions = extensions;
    }

    /// <summary>Gets the stable identity of this message.</summary>
    public MessageId Id { get; init; }

    /// <summary>Gets the agent that owns the session this message belongs to.</summary>
    public AgentId AgentId { get; init; }

    /// <summary>Gets the session that durably owns this message.</summary>
    public SessionId SessionId { get; init; }

    /// <summary>
    /// Gets the optional higher-level conversation grouping this message's
    /// session.
    /// </summary>
    public ConversationId? ConversationId { get; init; }

    /// <summary>Gets the session branch this message belongs to.</summary>
    public BranchId BranchId { get; init; }

    /// <summary>
    /// Gets the run that produced this message, when the message occurred
    /// during a run.
    /// </summary>
    public RunId? RunId { get; init; }

    /// <summary>
    /// Gets the turn that produced this message, when the message occurred
    /// during a run.
    /// </summary>
    public TurnId? TurnId { get; init; }

    /// <summary>
    /// Gets the commit time from the injected <see cref="TimeProvider"/>.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the completion state of this message.</summary>
    public MessageState State { get; init; }

    /// <summary>Gets the ordered, typed content of this message.</summary>
    public ImmutableArray<ContentPart> Parts { get; init; }

    /// <summary>Gets provider-specific or forward-compatible data.</summary>
    public ExtensionData Extensions { get; init; }
}
