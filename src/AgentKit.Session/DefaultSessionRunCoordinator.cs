// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

using System.Collections.Concurrent;

using Microsoft.Extensions.Options;

/// <summary>
/// The default, process-local <see cref="ISessionRunCoordinator"/>: one
/// mutating run may hold a session's lease at a time, enforced by an
/// in-process semaphore per session address.
/// </summary>
/// <remarks>
/// This implementation makes no distributed-safety claim: two instances of
/// this class running in two different processes have no way to observe
/// each other's leases. A durable execution adapter that needs cross-process
/// ownership replaces this registration with one backed by fenced,
/// distributed leases.
/// </remarks>
internal sealed class DefaultSessionRunCoordinator: ISessionRunCoordinator
{
    private readonly ConcurrentDictionary<SessionAddress, Slot> _slots = new();
    private readonly IIdentifierGenerator<SessionLeaseId> _leaseIds;
    private readonly AgentSessionOptions _options;
    private readonly ILogger<DefaultSessionRunCoordinator> _logger;

    /// <summary>Initializes a new instance of the <see cref="DefaultSessionRunCoordinator"/> class.</summary>
    /// <param name="leaseIds">Generates the identity of each acquired lease.</param>
    /// <param name="options">The validated session coordination options.</param>
    /// <param name="logger">
    /// The optional logger that receives safe lease diagnostics; a Microsoft
    /// null logger is used when omitted.
    /// </param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public DefaultSessionRunCoordinator(
        IIdentifierGenerator<SessionLeaseId> leaseIds,
        IOptions<AgentSessionOptions> options,
        ILogger<DefaultSessionRunCoordinator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(leaseIds);
        ArgumentNullException.ThrowIfNull(options);

        _leaseIds = leaseIds;
        _options = options.Value;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultSessionRunCoordinator>.Instance;
    }

    /// <inheritdoc/>
    public async ValueTask<SessionRunLeaseResult> AcquireAsync(
        SessionRunLeaseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var activity = AgentKitDiagnostics.Activities.StartActivity(
            AgentKitActivityNames.SessionLeaseAcquire,
            ActivityKind.Internal,
            parentContext: Activity.Current?.Context ?? default,
            tags: new ActivityTagsCollection
            {
                { AgentKitTagNames.GenAiOperationName, AgentKitActivityNames.SessionLeaseAcquire },
                { AgentKitTagNames.AgentId, request.AgentId.ToString() },
                { AgentKitTagNames.SessionId, request.SessionId.ToString() },
                { AgentKitTagNames.RunId, request.RunId.ToString() },
                { AgentKitTagNames.SessionOperation, AgentKitActivityNames.SessionLeaseAcquire },
            });
        SessionLog.OperationStarted(
            _logger,
            AgentKitActivityNames.SessionLeaseAcquire,
            request.AgentId,
            request.SessionId);

        var address = new SessionAddress(request.AgentId, request.SessionId);
        var slot = _slots.GetOrAdd(address, static _ => new Slot());

        var waitTimeout = _options.BusyBehavior == SessionBusyBehavior.Wait
            ? _options.BusyWaitTimeout
            : TimeSpan.Zero;
        bool acquired;
        try
        {
            acquired = await slot.Semaphore.WaitAsync(waitTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", "cancellation");
            SessionMetrics.Operations.Add(
                1,
                new KeyValuePair<string, object?>(AgentKitTagNames.SessionOperation, AgentKitActivityNames.SessionLeaseAcquire),
                new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, "cancelled"));
            SessionLog.OperationCancelled(
                _logger,
                AgentKitActivityNames.SessionLeaseAcquire,
                request.AgentId,
                request.SessionId);
            throw;
        }

        if (!acquired)
        {
            activity.SetFailed("busy", "session_busy");
            SessionMetrics.Operations.Add(
                1,
                new KeyValuePair<string, object?>(AgentKitTagNames.SessionOperation, AgentKitActivityNames.SessionLeaseAcquire),
                new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, "busy"));
            SessionLog.OperationCompleted(
                _logger,
                AgentKitActivityNames.SessionLeaseAcquire,
                request.AgentId,
                request.SessionId,
                "busy");
            return new SessionRunBusy(slot.ActiveRunId ?? request.RunId);
        }

        slot.ActiveRunId = request.RunId;
        activity.SetSuccessful("acquired");
        SessionMetrics.Operations.Add(
            1,
            new KeyValuePair<string, object?>(AgentKitTagNames.SessionOperation, AgentKitActivityNames.SessionLeaseAcquire),
            new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, "acquired"));
        SessionLog.OperationCompleted(
            _logger,
            AgentKitActivityNames.SessionLeaseAcquire,
            request.AgentId,
            request.SessionId,
            "acquired");
        return new SessionRunLeaseAcquired(
            new SessionRunLease(this, _leaseIds.Create(), request.AgentId, request.SessionId, request.RunId));
    }

    /// <summary>Releases the active-run slot for <paramref name="address"/>.</summary>
    /// <param name="address">The session whose slot should be released.</param>
    internal void Release(SessionAddress address)
    {
        if (_slots.TryGetValue(address, out var slot))
        {
            slot.ActiveRunId = null;
            _ = slot.Semaphore.Release();
        }
    }

    private sealed class Slot
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public RunId? ActiveRunId { get; set; }
    }
}
