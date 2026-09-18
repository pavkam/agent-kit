// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One user turn submitted to an <see cref="Agent"/> through its engine: who is speaking, what they said, which
/// session it belongs to, and how the run may be narrowed and observed.
/// </summary>
/// <remarks>
/// <para>
/// This is the engine-level admission unit. The engine creates the session when <see cref="SessionId"/> is
/// <see langword="null"/>, otherwise loads it and verifies that the agent, tenant, and principal own it; serializes
/// or rejects concurrent turns on the same session according to the pinned session profile's busy behavior;
/// appends the user message; and runs the agent. The result names the session and run so a caller can resume.
/// </para>
/// <para>
/// Overrides may only narrow the definition's limits. The observer is operational wiring and does not participate in
/// equality.
/// </para>
/// </remarks>
public sealed record AgentSendRequest
{
    /// <summary>Initializes a request carrying arbitrary content parts.</summary>
    /// <param name="identity">The authenticated identity speaking; it owns the session that is created or must own the one that is opened.</param>
    /// <param name="parts">The user message content, at least one part and no null elements.</param>
    /// <param name="sessionId">The session to continue, or <see langword="null"/> to create one.</param>
    /// <param name="maxTurns">A narrower per-run turn limit, or <see langword="null"/> for the definition's default.</param>
    /// <param name="attemptTimeout">A narrower per-attempt timeout, or <see langword="null"/> for the definition's default.</param>
    /// <param name="observer">A best-effort observer of provisional model and tool progress, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="identity"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="parts"/> is default, empty, or contains a null element.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="sessionId"/> is the default identity, <paramref name="maxTurns"/> is not positive, or
    /// <paramref name="attemptTimeout"/> is not positive.
    /// </exception>
    public AgentSendRequest(
        ExecutionIdentity identity,
        ImmutableArray<ContentPart> parts,
        SessionId? sessionId = null,
        int? maxTurns = null,
        TimeSpan? attemptTimeout = null,
        IAgentRunObserver? observer = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfDefaultOrEmpty(parts);
        ArgumentException.ThrowIfContainsNull(parts);
        if (sessionId is { } session)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(session, default, nameof(sessionId));
        }

        if (maxTurns is { } turns)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(turns, nameof(maxTurns));
        }

        if (attemptTimeout is { } timeout)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero, nameof(attemptTimeout));
        }

        Identity = identity;
        Parts = parts;
        SessionId = sessionId;
        MaxTurns = maxTurns;
        AttemptTimeout = attemptTimeout;
        Observer = observer;
    }

    /// <summary>Initializes a request carrying one plain-text user message.</summary>
    /// <param name="identity">The authenticated identity speaking.</param>
    /// <param name="text">The user's message text.</param>
    /// <param name="sessionId">The session to continue, or <see langword="null"/> to create one.</param>
    /// <param name="maxTurns">A narrower per-run turn limit, or <see langword="null"/>.</param>
    /// <param name="attemptTimeout">A narrower per-attempt timeout, or <see langword="null"/>.</param>
    /// <param name="observer">A best-effort progress observer, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="identity"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="text"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity or limit argument is out of range, as on the parts constructor.</exception>
    public AgentSendRequest(
        ExecutionIdentity identity,
        string text,
        SessionId? sessionId = null,
        int? maxTurns = null,
        TimeSpan? attemptTimeout = null,
        IAgentRunObserver? observer = null)
        : this(identity, PartsFor(text), sessionId, maxTurns, attemptTimeout, observer)
    {
    }

    /// <summary>Gets the authenticated identity speaking.</summary>
    public ExecutionIdentity Identity { get; }

    /// <summary>Gets the user message content.</summary>
    /// <value>Never default or empty.</value>
    public ImmutableArray<ContentPart> Parts { get; }

    /// <summary>Gets the session to continue, or <see langword="null"/> when the engine should create one.</summary>
    public SessionId? SessionId { get; }

    /// <summary>Gets the narrower per-run turn limit, when supplied.</summary>
    public int? MaxTurns { get; }

    /// <summary>Gets the narrower per-attempt timeout, when supplied.</summary>
    public TimeSpan? AttemptTimeout { get; }

    /// <summary>Gets the best-effort progress observer, when supplied. Excluded from equality.</summary>
    public IAgentRunObserver? Observer { get; init; }

    /// <inheritdoc/>
    public bool Equals(AgentSendRequest? other) =>
        other is not null
        && Identity.Equals(other.Identity)
        && Parts.SequenceEqual(other.Parts)
        && SessionId.Equals(other.SessionId)
        && MaxTurns == other.MaxTurns
        && AttemptTimeout == other.AttemptTimeout;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Identity);
        foreach (var part in Parts)
        {
            hash.Add(part);
        }

        hash.Add(SessionId);
        hash.Add(MaxTurns);
        hash.Add(AttemptTimeout);
        return hash.ToHashCode();
    }

    private static ImmutableArray<ContentPart> PartsFor(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)];
    }
}
