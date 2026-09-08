// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

/// <summary>Selects only explicitly injected session stores by a profile or directory-pinned key.</summary>
/// <remarks>The selector freezes the composed store set during construction. It does not query directories, inspect service providers, probe fallback stores, or perform migrations.</remarks>
public sealed class DefaultSessionStoreSelector: ISessionStoreSelector
{
    private readonly FrozenDictionary<SessionStoreKey, SessionStoreBinding> _stores;
    private readonly ILogger<DefaultSessionStoreSelector> _logger;

    /// <summary>Initializes a selector from explicitly injected store implementations.</summary>
    /// <param name="stores">The non-null composed stores to capture once.</param>
    /// <param name="logger">The non-null diagnostics logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stores"/>, <paramref name="logger"/>, or a store entry is null.</exception>
    /// <exception cref="ArgumentException">A captured descriptor is null or two captured descriptors reuse a store key.</exception>
    public DefaultSessionStoreSelector(
        IEnumerable<ISessionStore> stores,
        ILogger<DefaultSessionStoreSelector> logger)
        : this(CreateSnapshot(stores, logger), logger)
    {
    }

    /// <summary>Initializes a selector over the shared immutable composition snapshot.</summary>
    /// <param name="snapshot">The non-null exactly-once captured binding snapshot.</param>
    /// <param name="logger">The non-null diagnostics logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> or <paramref name="logger"/> is null.</exception>
    internal DefaultSessionStoreSelector(
        SessionStoreBindingSnapshot snapshot,
        ILogger<DefaultSessionStoreSelector> logger)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _stores = snapshot.Bindings
            .ToFrozenDictionary(static binding => binding.Descriptor.Key);
    }

    /// <summary>Validates every public constructor argument before capturing observable store descriptors.</summary>
    /// <param name="stores">The caller-supplied store sequence.</param>
    /// <param name="logger">The caller-supplied diagnostics logger.</param>
    /// <returns>A validated immutable binding snapshot captured only after both arguments pass their guards.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stores"/> or <paramref name="logger"/> is null.</exception>
    private static SessionStoreBindingSnapshot CreateSnapshot(
        IEnumerable<ISessionStore> stores,
        ILogger<DefaultSessionStoreSelector> logger)
    {
        ArgumentNullException.ThrowIfNull(stores);
        ArgumentNullException.ThrowIfNull(logger);
        return new SessionStoreBindingSnapshot(stores);
    }

    /// <inheritdoc/>
    public ValueTask<SessionStoreSelectionResult> SelectForCreateAsync(
        SessionStoreCreateSelectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SelectAsync(request.Request.AgentId, null, request.Profile.DefaultStoreKey, request.Profile, null, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<SessionStoreSelectionResult> ResolveExistingAsync(
        SessionStoreSelectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var mismatch = request.Context.ToAddress() != request.Location.Address
            || request.Context.Identity.TenantId != request.Location.TenantId
            ? new SessionStoreSelectionRejected(
                SessionStoreSelectionRejectionReason.IdentityAddressOrConfigurationMismatch,
                "The session routing evidence is inconsistent.")
            : null;
        return SelectAsync(request.Context.AgentId, request.Context.SessionId, request.Location.StoreKey, request.Profile, mismatch, cancellationToken);
    }

    private ValueTask<SessionStoreSelectionResult> SelectAsync(
        AgentId agentId,
        SessionId? sessionId,
        SessionStoreKey key,
        SessionProfileSnapshot profile,
        SessionStoreSelectionResult? forcedResult,
        CancellationToken cancellationToken)
    {
        Debug.Assert(agentId != default, "Public selection requests validate the agent identity.");
        Debug.Assert(!string.IsNullOrWhiteSpace(key.Value), "Public selection requests validate the selected store key.");
        Debug.Assert(profile is not null, "Public selection requests validate the compiled profile.");
        Activity? activity = null;
        TryObserve(() =>
        {
            activity = AgentKitDiagnostics.Activities.StartActivity(
                AgentKitActivityNames.SessionStoreOperation,
                ActivityKind.Internal,
                parentContext: Activity.Current?.Context ?? default,
                tags: new ActivityTagsCollection
                {
                    { AgentKitTagNames.GenAiOperationName, AgentKitActivityNames.SessionStoreOperation },
                    { AgentKitTagNames.AgentId, agentId.ToString() },
                    { AgentKitTagNames.SessionId, sessionId?.ToString() },
                    { AgentKitTagNames.SessionOperation, "select" },
                });
        });
        TryObserve(() => SessionLog.OperationStarted(_logger, "session.store.select", agentId, sessionId));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = forcedResult ?? Select(key, profile);
            ObserveTerminal(activity, agentId, sessionId, ResultOutcome(result), result is SessionStoreSelected);
            return ValueTask.FromResult(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ObserveCancellation(activity, agentId, sessionId);
            throw;
        }
        catch (Exception exception)
        {
            ObserveFault(activity, agentId, sessionId, exception);
            throw;
        }
        finally
        {
            TryObserve(() => activity?.Dispose());
        }
    }

    private SessionStoreSelectionResult Select(SessionStoreKey key, SessionProfileSnapshot profile)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(key.Value), "Only validated store keys reach selection.");
        Debug.Assert(profile is not null, "Only validated profiles reach selection.");
        return !_stores.TryGetValue(key, out var binding)
            ? new SessionStoreSelectionRejected(
                SessionStoreSelectionRejectionReason.MissingStoreKey,
                "The selected session store is unavailable.")
            : profile.RequiresDurableStore && !binding.Descriptor.Durable
            ? new SessionStoreSelectionRejected(
                SessionStoreSelectionRejectionReason.IncompatibleDurability,
                "The selected session store cannot satisfy the profile's durability requirement.")
            : (profile.RequiresDistributedFencing && !binding.Descriptor.SupportsDistributedFencing)
                || (binding.Descriptor.Capabilities & profile.RequiredStoreCapabilities) != profile.RequiredStoreCapabilities
                ? new SessionStoreSelectionRejected(
                SessionStoreSelectionRejectionReason.IncompatibleCapabilities,
                "The selected session store cannot satisfy the profile's capability requirement.")
                : new SessionStoreSelected(binding.Store, binding.Descriptor);
    }

    private void ObserveTerminal(
        Activity? activity,
        AgentId agentId,
        SessionId? sessionId,
        string outcome,
        bool successful)
    {
        TryObserve(() =>
        {
            _ = activity?.SetTag(AgentKitTagNames.Outcome, outcome);
            _ = activity?.SetStatus(successful ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        });
        TryObserve(() => SessionMetrics.Operations.Add(
            1,
            new KeyValuePair<string, object?>(AgentKitTagNames.SessionOperation, "select"),
            new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome)));
        TryObserve(() => SessionLog.OperationCompleted(_logger, "session.store.select", agentId, sessionId, outcome));
    }

    private void ObserveCancellation(Activity? activity, AgentId agentId, SessionId? sessionId)
    {
        TryObserve(() =>
        {
            _ = activity?.SetTag(AgentKitTagNames.Outcome, "cancelled");
            _ = activity?.SetStatus(ActivityStatusCode.Error, "cancellation");
        });
        TryObserve(() => SessionMetrics.Operations.Add(
            1,
            new KeyValuePair<string, object?>(AgentKitTagNames.SessionOperation, "select"),
            new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, "cancelled")));
        TryObserve(() => SessionLog.OperationCancelled(_logger, "session.store.select", agentId, sessionId));
    }

    private void ObserveFault(Activity? activity, AgentId agentId, SessionId? sessionId, Exception exception)
    {
        Debug.Assert(exception is not null, "The exception being reported is required.");
        var errorType = exception.GetType().FullName ?? exception.GetType().Name;
        TryObserve(() =>
        {
            _ = activity?.SetTag(AgentKitTagNames.Outcome, "faulted");
            _ = activity?.SetTag(AgentKitTagNames.ErrorType, errorType);
            _ = activity?.SetStatus(ActivityStatusCode.Error, errorType);
        });
        TryObserve(() => SessionMetrics.Operations.Add(
            1,
            new KeyValuePair<string, object?>(AgentKitTagNames.SessionOperation, "select"),
            new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, "faulted")));
        TryObserve(() => SessionLog.OperationFaulted(_logger, "session.store.select", agentId, sessionId, errorType));
    }

    private static void TryObserve(Action? observation)
    {
        try
        {
            observation?.Invoke();
        }
        catch
        {
            // Diagnostics are observational and cannot alter a routing outcome.
        }
    }

    private static string ResultOutcome(SessionStoreSelectionResult result) => result switch
    {
        SessionStoreSelected => "selected",
        SessionStoreSelectionRejected { Reason: SessionStoreSelectionRejectionReason.MissingStoreKey } => "missing_store_key",
        SessionStoreSelectionRejected { Reason: SessionStoreSelectionRejectionReason.IncompatibleDurability } => "incompatible_durability",
        SessionStoreSelectionRejected { Reason: SessionStoreSelectionRejectionReason.IncompatibleCapabilities } => "incompatible_capabilities",
        SessionStoreSelectionRejected => "routing_mismatch",
        _ => "unknown",
    };
}
