// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Forwards one conversation while owning a synchronous composition lifetime.</summary>
/// <remarks>
/// Every conversation operation is forwarded directly, including live observation and session discovery. Disposal
/// is idempotent and disposes only the supplied owner; it does not dispose the inner conversation separately.
/// Callers must quiesce in-flight operations before disposal. Disposal is not synchronization with active calls, and
/// operations started after disposal begins are rejected.
/// </remarks>
public sealed class OwnedConversationSession: IConversationSession, IDisposable
{
    private readonly IConversationSession _inner;
    private IDisposable? _owner;

    /// <summary>Initializes a forwarding conversation with its owned synchronous lifetime.</summary>
    /// <param name="inner">The nonnull conversation whose operations are forwarded.</param>
    /// <param name="owner">The nonnull lifetime disposed exactly once.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public OwnedConversationSession(IConversationSession inner, IDisposable owner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(owner);
        _inner = inner;
        _owner = owner;
    }

    /// <inheritdoc/>
    /// <remarks>Forwards to the wrapped conversation; readable after disposal because it performs no work.</remarks>
    public SessionId? SessionId => _inner.SessionId;

    /// <inheritdoc/>
    /// <remarks>Forwards to the wrapped conversation; readable after disposal because it performs no work.</remarks>
    public BranchId? BranchId => _inner.BranchId;

    /// <inheritdoc/>
    public ValueTask<ConversationHistoryReadResult> ReadHistoryAsync(
        SessionSequence afterSequence,
        int maximumEntries,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_owner is null, this);
        return _inner.ReadHistoryAsync(afterSequence, maximumEntries, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<ConversationSessionOpenResult> OpenAsync(
        SessionId sessionId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_owner is null, this);
        return _inner.OpenAsync(sessionId, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<ConversationSessionListResult> ListAsync(
        SessionId? afterSessionId,
        int maximumResults,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_owner is null, this);
        return _inner.ListAsync(afterSessionId, maximumResults, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ConversationTurnResult> SendAsync(string userText, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_owner is null, this);
        return _inner.SendAsync(userText, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ConversationTurnResult> SendAsync(
        string userText,
        IConversationEventObserver observer,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_owner is null, this);
        return _inner.SendAsync(userText, observer, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<ToolPresentation?> PresentToolAsync(
        ContentPart part,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_owner is null, this);
        return _inner.PresentToolAsync(part, cancellationToken);
    }

    /// <summary>Disposes the owned lifetime exactly once.</summary>
    /// <remarks>Callers must complete or cancel all forwarded operations before invoking this method.</remarks>
    public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Dispose();
}
