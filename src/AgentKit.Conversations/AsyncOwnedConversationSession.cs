// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Forwards one conversation while owning an asynchronous composition lifetime.</summary>
/// <remarks>
/// Every conversation operation is forwarded directly. Callers must quiesce in-flight operations before disposal;
/// disposal is not synchronization with active calls. Once disposal begins, later operations are rejected. Repeated
/// disposal calls share the same completion and failure without repeating the owner's disposal effect.
/// </remarks>
public sealed class AsyncOwnedConversationSession: IConversationSession, IAsyncDisposable
{
    private readonly IConversationSession _inner;
    private readonly IAsyncDisposable _owner;
    private readonly Lock _disposeGate = new();
    private Task? _disposeTask;

    /// <summary>Initializes a forwarding conversation with its owned asynchronous lifetime.</summary>
    /// <param name="inner">The nonnull conversation whose operations are forwarded.</param>
    /// <param name="owner">The nonnull lifetime disposed exactly once.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public AsyncOwnedConversationSession(IConversationSession inner, IAsyncDisposable owner)
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
        ThrowIfDisposing();
        return _inner.ReadHistoryAsync(afterSequence, maximumEntries, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<ConversationSessionOpenResult> OpenAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposing();
        return _inner.OpenAsync(sessionId, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<ConversationSessionListResult> ListAsync(
        SessionId? afterSessionId,
        int maximumResults,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposing();
        return _inner.ListAsync(afterSessionId, maximumResults, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ConversationTurnResult> SendAsync(string userText, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposing();
        return _inner.SendAsync(userText, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ConversationTurnResult> SendAsync(
        string userText,
        IConversationEventObserver observer,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposing();
        return _inner.SendAsync(userText, observer, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<ToolPresentation?> PresentToolAsync(
        ContentPart part,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposing();
        return _inner.PresentToolAsync(part, cancellationToken);
    }

    /// <summary>Disposes the owned lifetime exactly once and shares its terminal outcome with every caller.</summary>
    /// <returns>A value task backed by the one asynchronous disposal operation.</returns>
    /// <remarks>Callers must complete or cancel all forwarded operations before invoking this method.</remarks>
    public ValueTask DisposeAsync()
    {
        lock (_disposeGate)
        {
            _disposeTask ??= DisposeOwnerAsync();
            return new ValueTask(_disposeTask);
        }
    }

    /// <summary>Awaits the single owned disposal operation.</summary>
    /// <returns>A task retaining the owner's completion or failure for every disposal caller.</returns>
    private async Task DisposeOwnerAsync() => await _owner.DisposeAsync().ConfigureAwait(false);

    /// <summary>Rejects operation admission after asynchronous disposal has begun.</summary>
    /// <exception cref="ObjectDisposedException">Disposal has begun.</exception>
    private void ThrowIfDisposing()
    {
        lock (_disposeGate)
        {
            ObjectDisposedException.ThrowIf(_disposeTask is not null, this);
        }
    }
}
