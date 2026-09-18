// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using System.Collections.Immutable;

/// <summary>
/// A minimal stateful <see cref="ISessionCoordinator"/> double: creates sessions with fresh identities, loads them by
/// address, reports an exact single-branch snapshot, and appends contiguous entries. Enough to drive an engine
/// admission end to end without a store adapter; branching, deletion, and lane operations are unsupported.
/// </summary>
/// <remarks>
/// Every member records its request so tests can assert on identities, idempotency keys, and ordering. Access is
/// serialized with a lock, so concurrent turns on different sessions can share one instance.
/// </remarks>
public sealed class InMemoryTestSessionCoordinator: ISessionCoordinator
{
    private readonly Lock _gate = new();
    private readonly Dictionary<SessionId, StoredSession> _sessions = [];

    /// <summary>Gets every create request received, in order.</summary>
    public List<SessionCreateRequest> CreateRequests { get; } = [];

    /// <summary>Gets every load context received, in order.</summary>
    public List<SessionOperationContext> LoadContexts { get; } = [];

    /// <summary>Gets every append request received, in order.</summary>
    public List<SessionAppendRequest> AppendRequests { get; } = [];

    /// <summary>Gets or sets a result that replaces the next create, or <see langword="null"/> to create normally.</summary>
    public SessionCreateResult? NextCreateResult { get; set; }

    /// <summary>Gets or sets a result that replaces every append, or <see langword="null"/> to append normally.</summary>
    public SessionAppendResult? AppendOverride { get; set; }

    /// <summary>Gets or sets a gate every append awaits before committing, for concurrency tests.</summary>
    public TaskCompletionSource? AppendGate { get; set; }

    /// <summary>Seeds an existing session the coordinator will load.</summary>
    /// <param name="descriptor">The session's descriptor.</param>
    public void Seed(SessionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        lock (_gate)
        {
            _sessions[descriptor.Address.SessionId] = new StoredSession(descriptor);
        }
    }

    /// <summary>Returns the entries committed to a session, in sequence order.</summary>
    /// <param name="sessionId">The session to inspect.</param>
    /// <returns>The committed entries, or empty when the session is unknown.</returns>
    public ImmutableArray<SessionEntry> EntriesOf(SessionId sessionId)
    {
        lock (_gate)
        {
            return _sessions.TryGetValue(sessionId, out var session) ? [.. session.Entries] : [];
        }
    }

    /// <inheritdoc/>
    public ValueTask<SessionCreateResult> CreateAsync(SessionCreateRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            CreateRequests.Add(request);
            if (NextCreateResult is { } scripted)
            {
                NextCreateResult = null;
                return ValueTask.FromResult(scripted);
            }

            var descriptor = new SessionDescriptor(
                new SessionAddress(request.AgentId, new SessionId(Guid.NewGuid())),
                request.ConversationId,
                request.Identity.TenantId,
                request.Identity.PrincipalId,
                profile.DefaultStoreKey,
                new BranchId(Guid.NewGuid()),
                new SessionVersion(0),
                SessionLifecycleState.Active,
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch,
                new SchemaVersion("1"),
                ExtensionData.Empty);
            _sessions[descriptor.Address.SessionId] = new StoredSession(descriptor);
            return ValueTask.FromResult<SessionCreateResult>(new SessionCreated(descriptor, existing: false));
        }
    }

    /// <inheritdoc/>
    public ValueTask<SessionLoadResult> LoadAsync(SessionOperationContext context, SessionProfileSnapshot profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            LoadContexts.Add(context);
            return ValueTask.FromResult<SessionLoadResult>(
                _sessions.TryGetValue(context.SessionId, out var session)
                    ? new SessionLoaded(session.Descriptor with { Version = session.Version })
                    : new SessionNotFound(context.ToAddress()));
        }
    }

    /// <inheritdoc/>
    public async ValueTask<SessionAppendResult> AppendAsync(SessionAppendRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (AppendGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken);
        }

        lock (_gate)
        {
            AppendRequests.Add(request);
            if (AppendOverride is { } scripted)
            {
                return scripted;
            }

            if (!_sessions.TryGetValue(request.Context.SessionId, out var session))
            {
                return new SessionAppendNotFound(request.Context.ToAddress());
            }

            if (request.ExpectedVersion != session.Version)
            {
                return new SessionAppendConflict(request.ExpectedVersion, session.Version);
            }

            session.Entries.AddRange(request.Entries);
            session.Version = new SessionVersion(session.Version.Value + 1);
            return new SessionAppended(session.Version, request.Entries);
        }
    }

    /// <inheritdoc/>
    public ValueTask<SessionPageResult> ReadAsync(SessionReadRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_sessions.TryGetValue(request.Context.SessionId, out var session))
            {
                return ValueTask.FromResult<SessionPageResult>(new SessionReadNotFound(request.Context.ToAddress()));
            }

            var upper = session.Entries.Count == 0 ? new SessionSequence(0) : session.Entries[^1].Sequence;
            var page = session.Entries
                .Where(entry => entry.Sequence.Value > request.FromSequenceExclusive.Value)
                .Take(request.PageSize)
                .ToImmutableArray();
            var through = page.IsEmpty ? request.FromSequenceExclusive : page[^1].Sequence;
            return ValueTask.FromResult<SessionPageResult>(new SessionPage(
                page,
                through,
                hasMore: through.Value < upper.Value,
                new SessionReadSnapshot(request.Context.ToAddress(), request.BranchId, session.Version, upper)));
        }
    }

    /// <inheritdoc/>
    public ValueTask<SessionBranchResult> BranchAsync(SessionBranchRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Branching is outside this double's scope.");

    /// <inheritdoc/>
    public ValueTask<SessionDeleteResult> DeleteAsync(SessionDeleteRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Deletion is outside this double's scope.");

    private sealed class StoredSession(SessionDescriptor descriptor)
    {
        public SessionDescriptor Descriptor { get; } = descriptor;

        public SessionVersion Version { get; set; } = descriptor.Version;

        public List<SessionEntry> Entries { get; } = [];
    }
}
