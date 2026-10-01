// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using Microsoft.Extensions.Options;

/// <summary>Records accepted and terminal tool-call facts as durable session entries through the run's selected session capability.</summary>
/// <remarks>
/// <para>
/// The recorder is stateless and holds no coordinator: every write uses the <see cref="SessionExecutionCapability"/> and
/// <see cref="ToolCallSessionTarget"/> the executor supplies, so it never selects an unkeyed session coordinator or
/// rediscovers a store. Each write reads the branch tip, appends one entry under optimistic concurrency, and re-reads
/// after a conflict up to <see cref="ToolRuntimeOptions.MaximumRecordAppendAttempts"/> times. The accepted entry keeps one
/// identity across attempts; its idempotency key is the call identity, so an acknowledged append is never duplicated by
/// the store.
/// </para>
/// <para>
/// A terminal write whose result carries acceptance evidence first locates the accepted entry within the most recent
/// <see cref="ToolRuntimeOptions.AcceptedRecordLookupEntries"/> branch entries and verifies identity, effects, idempotency
/// key, admission, acceptance and grant, and policy coherence; a missing or incoherent accepted record rejects the write
/// as <see cref="ToolCallRecordRejectionKind.Conflict"/>. The persisted terminal entry holds no result content, usage, or
/// extension data (see <see cref="ToolCallTerminalSessionEntry"/>). Recording never invokes a tool.
/// </para>
/// </remarks>
public sealed class SessionToolCallRecorder: IToolCallRecorder
{
    private static readonly SchemaVersion _schemaVersion = new("1");

    private readonly IIdentifierGenerator<SessionEntryId> _entryIds;
    private readonly ToolRuntimeOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SessionToolCallRecorder> _logger;

    /// <summary>Initializes the session-backed recorder.</summary>
    /// <param name="entryIds">The identifier generator for appended session entries.</param>
    /// <param name="options">The configured append-attempt and accepted-lookup bounds.</param>
    /// <param name="timeProvider">The replaceable clock used for entry commit times and observation.</param>
    /// <param name="logger">The type-specific structured logger.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public SessionToolCallRecorder(
        IIdentifierGenerator<SessionEntryId> entryIds,
        IOptions<ToolRuntimeOptions> options,
        TimeProvider timeProvider,
        ILogger<SessionToolCallRecorder> logger)
    {
        ArgumentNullException.ThrowIfNull(entryIds);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _entryIds = entryIds;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask<ToolCallRecordResult> RecordAcceptedAsync(
        AcceptedToolCall accepted,
        SessionExecutionCapability session,
        ToolCallSessionTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accepted);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();
        using var observation = new ToolRecordObservation(
            "accepted", accepted.AgentId, accepted.SessionId, accepted.RunId, accepted.TurnId, accepted.CallId, _timeProvider, _logger);
        var result = await AppendAsync(
            session,
            target,
            accepted.Authorization,
            accepted.CallId,
            "accepted",
            observation,
            parentId: null,
            (id, address, correlation, sequence, parent, now) => new ToolCallAcceptedSessionEntry(
                id, address, correlation, target.BranchId, sequence, parent, now, _schemaVersion, accepted),
            cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc/>
    public async ValueTask<ToolCallRecordResult> RecordTerminalAsync(
        ToolCallResult result,
        SessionExecutionCapability session,
        ToolCallSessionTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();
        using var observation = new ToolRecordObservation(
            "terminal", result.AgentId, result.SessionId, result.RunId, result.TurnId, result.CallId, _timeProvider, _logger);
        SessionEntryId? parentId = null;
        if (result.Acceptance is not null)
        {
            var lookup = await FindAcceptedAsync(session, target, result, cancellationToken).ConfigureAwait(false);
            if (lookup.Rejection is { } rejection)
            {
                observation.Complete(OutcomeOf(rejection));
                return rejection;
            }

            parentId = lookup.EntryId;
        }

        var evidence = ToolCallResultComposer.WithoutContent(result);
        return await AppendAsync(
            session,
            target,
            result.Authorization,
            result.CallId,
            "terminal",
            observation,
            parentId,
            (id, address, correlation, sequence, parent, now) => new ToolCallTerminalSessionEntry(
                id, address, correlation, target.BranchId, sequence, parent, now, _schemaVersion, evidence),
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ToolCallRecordResult> AppendAsync(
        SessionExecutionCapability session,
        ToolCallSessionTarget target,
        SecurityAuthorizationContext authorization,
        ToolCallId callId,
        string stage,
        ToolRecordObservation observation,
        SessionEntryId? parentId,
        Func<SessionEntryId, SessionAddress, InRunOperationCorrelation, SessionSequence, SessionEntryId?, DateTimeOffset, SessionEntry> createEntry,
        CancellationToken cancellationToken)
    {
        Debug.Assert(authorization.Scope.Correlation is InRunOperationCorrelation, "Accepted and terminal records are in-run facts.");
        try
        {
            var correlation = (InRunOperationCorrelation) authorization.Scope.Correlation;
            var address = new SessionAddress(authorization.Scope.AgentId, authorization.Scope.SessionId!.Value);
            var context = new SessionOperationContext(
                address.AgentId, address.SessionId, target.ExecutionLaneId, correlation, authorization.Identity, authorization);
            var entryId = _entryIds.Create();
            var idempotencyKey = new IdempotencyKey($"agentkit.tool-call:{callId}:{stage}");
            for (var attempt = 1; attempt <= _options.MaximumRecordAppendAttempts; attempt++)
            {
                var tip = await ReadTipAsync(session, context, target, cancellationToken).ConfigureAwait(false);
                if (tip is null)
                {
                    return Reject(observation, ToolCallRecordRejectionKind.Unavailable, "The session branch could not be read to record the tool call.");
                }

                var entry = createEntry(
                    entryId, address, correlation, new SessionSequence(tip.UpperSequence.Value + 1), parentId, _timeProvider.GetUtcNow());
                var append = await session.Coordinator.AppendAsync(
                    new SessionAppendRequest(context, target.BranchId, tip.Version, idempotencyKey, [entry]),
                    session.Profile,
                    cancellationToken).ConfigureAwait(false);
                switch (append)
                {
                    case SessionAppended:
                        observation.Complete("recorded");
                        return new ToolCallRecorded();
                    case SessionAppendConflict when attempt < _options.MaximumRecordAppendAttempts:
                        continue;
                    case SessionAppendConflict:
                        return Reject(observation, ToolCallRecordRejectionKind.Conflict, "The session advanced concurrently and the tool-call record could not be appended.");
                    default:
                        return Reject(observation, ToolCallRecordRejectionKind.Unavailable, "The session store did not commit the tool-call record.");
                }
            }

            return Reject(observation, ToolCallRecordRejectionKind.Conflict, "The session advanced concurrently and the tool-call record could not be appended.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            observation.Complete("cancelled");
            throw;
        }
        catch (ArgumentException)
        {
            return Reject(observation, ToolCallRecordRejectionKind.Invalid, "The tool-call evidence cannot be recorded in the session.");
        }
        catch (Exception)
        {
            return Reject(observation, ToolCallRecordRejectionKind.Unavailable, "The session coordinator failed while recording the tool call.");
        }
    }

    private static async ValueTask<SessionReadSnapshot?> ReadTipAsync(
        SessionExecutionCapability session,
        SessionOperationContext context,
        ToolCallSessionTarget target,
        CancellationToken cancellationToken)
    {
        var page = await session.Coordinator.ReadAsync(
            new SessionReadRequest(context, target.BranchId, new SessionSequence(0), 1),
            session.Profile,
            cancellationToken).ConfigureAwait(false);
        return page is SessionPage { Snapshot: { } snapshot } ? snapshot : null;
    }

    private async ValueTask<AcceptedLookup> FindAcceptedAsync(
        SessionExecutionCapability session,
        ToolCallSessionTarget target,
        ToolCallResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            var authorization = result.Authorization;
            var correlation = (InRunOperationCorrelation) authorization.Scope.Correlation;
            var context = new SessionOperationContext(
                result.AgentId, result.SessionId, target.ExecutionLaneId, correlation, authorization.Identity, authorization);
            var tip = await ReadTipAsync(session, context, target, cancellationToken).ConfigureAwait(false);
            if (tip is null)
            {
                return AcceptedLookup.Rejected(ToolCallRecordRejectionKind.Unavailable, "The session branch could not be read to find the accepted tool-call record.");
            }

            var pageSize = Math.Max(1, Math.Min(_options.AcceptedRecordLookupEntries, session.Profile.MaximumPageSize));
            var cursor = new SessionSequence(Math.Max(0, tip.UpperSequence.Value - _options.AcceptedRecordLookupEntries));
            while (cursor.Value < tip.UpperSequence.Value)
            {
                var read = await session.Coordinator.ReadAsync(
                    new SessionReadRequest(context, target.BranchId, cursor, pageSize, tip),
                    session.Profile,
                    cancellationToken).ConfigureAwait(false);
                if (read is not SessionPage page)
                {
                    return AcceptedLookup.Rejected(ToolCallRecordRejectionKind.Unavailable, "The session branch could not be read to find the accepted tool-call record.");
                }

                foreach (var entry in page.Entries)
                {
                    if (entry is ToolCallAcceptedSessionEntry accepted && accepted.Call.CallId == result.CallId)
                    {
                        return Coherent(accepted.Call, result)
                            ? AcceptedLookup.Found(accepted.Id)
                            : AcceptedLookup.Rejected(ToolCallRecordRejectionKind.Conflict, "The terminal record does not match the accepted record of the same call.");
                    }
                }

                if (!page.HasMore || page.Entries.IsEmpty)
                {
                    break;
                }

                cursor = page.ThroughSequence;
            }

            return AcceptedLookup.Rejected(ToolCallRecordRejectionKind.Conflict, "The accepted record of the call was not found among the recent branch entries.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return AcceptedLookup.Rejected(ToolCallRecordRejectionKind.Unavailable, "The session coordinator failed while finding the accepted tool-call record.");
        }
    }

    private static bool Coherent(AcceptedToolCall accepted, ToolCallResult result) =>
        accepted.ProviderAlias == result.ProviderAlias
        && result.ToolId == accepted.ToolId
        && result.ToolVersion == accepted.ToolVersion
        && result.Effects == accepted.Effects
        && result.ExternalIdempotencyKey == accepted.ExternalIdempotencyKey
        && result.Admission == accepted.Admission
        && result.Acceptance == accepted.Acceptance
        && result.GrantId == accepted.Acceptance.InvocationGrantId
        && result.Normalization.ExecutionPolicy == accepted.Normalization.ExecutionPolicy
        && result.ProjectionPolicy == accepted.ProjectionPolicy;

    private static string OutcomeOf(ToolCallRecordRejected rejection) => rejection.Kind switch
    {
        ToolCallRecordRejectionKind.Conflict => "conflict",
        ToolCallRecordRejectionKind.Invalid => "invalid",
        ToolCallRecordRejectionKind.Unavailable => "unavailable",
        _ => "unavailable",
    };

    private static ToolCallRecordRejected Reject(ToolRecordObservation observation, ToolCallRecordRejectionKind kind, string reason)
    {
        var rejection = new ToolCallRecordRejected(kind, reason);
        observation.Complete(OutcomeOf(rejection));
        return rejection;
    }

    private sealed record AcceptedLookup(SessionEntryId? EntryId, ToolCallRecordRejected? Rejection)
    {
        internal static AcceptedLookup Found(SessionEntryId id) => new(id, null);

        internal static AcceptedLookup Rejected(ToolCallRecordRejectionKind kind, string reason) =>
            new(null, new ToolCallRecordRejected(kind, reason));
    }
}
