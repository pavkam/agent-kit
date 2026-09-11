// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Publishes explicitly configured immutable tool bindings through independent source captures.</summary>
/// <remarks>
/// This concurrently callable provider publishes the same principal-independent metadata for every
/// request. It performs no discovery I/O, source filtering, alias inference, or tool invocation. The
/// catalog decides whether this source is selected and whether its schemas and capabilities can be
/// exposed. Invokers are borrowed: their host owner must keep them alive until every capture and lease
/// has closed. Choose a scoped or dynamic provider when per-request instances or protected discovery
/// are needed. A new publication requires a new provider; existing captures never change.
/// </remarks>
public sealed class StaticToolProvider: IToolProvider
{
    private readonly ToolProviderBindings _bindings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StaticToolProvider> _logger;
    private readonly ILogger<ToolProviderCapture> _captureLogger;

    /// <summary>Validates an explicit source publication and its complete borrowed binding graph before discovery is possible.</summary>
    /// <param name="snapshot">The nonnull immutable source publication with its explicit source version.</param>
    /// <param name="invokers">Exactly one nonnull borrowed binding for each published identity; comparers cannot weaken exact identity.</param>
    /// <param name="timeProvider">The nonnull replaceable clock used only for diagnostic duration.</param>
    /// <param name="logger">The nonnull discovery logger, containing safe correlation only.</param>
    /// <param name="captureLogger">The nonnull source-capture logger used by each independent returned capture.</param>
    /// <exception cref="ArgumentNullException">A required reference or a supplied invoker is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A binding identity in <paramref name="invokers"/> is default.</exception>
    /// <exception cref="ArgumentException">Bindings contain duplicate normalized identities or differ from the publication keyset.</exception>
    /// <remarks>Construction never invokes or disposes a supplied invoker and does not read the diagnostic clock or emit logs.</remarks>
    public StaticToolProvider(ToolProviderSnapshot snapshot, ImmutableDictionary<ToolIdentity, IToolInvoker> invokers,
        TimeProvider timeProvider, ILogger<StaticToolProvider> logger, ILogger<ToolProviderCapture> captureLogger)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(invokers);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(captureLogger);
        _bindings = new ToolProviderBindings(snapshot, invokers);
        _timeProvider = timeProvider;
        _logger = logger;
        _captureLogger = captureLogger;
    }

    /// <summary>Creates a provider over bindings already validated by the registration boundary.</summary>
    /// <param name="bindings">The nonnull immutable borrowed binding graph.</param>
    /// <param name="timeProvider">The nonnull diagnostic clock.</param>
    /// <param name="logger">The nonnull discovery logger.</param>
    /// <param name="captureLogger">The nonnull source-capture logger.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    internal StaticToolProvider(ToolProviderBindings bindings, TimeProvider timeProvider,
        ILogger<StaticToolProvider> logger, ILogger<ToolProviderCapture> captureLogger)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(captureLogger);
        _bindings = bindings;
        _timeProvider = timeProvider;
        _logger = logger;
        _captureLogger = captureLogger;
    }

    /// <inheritdoc/>
    public ToolSourceId SourceId => _bindings.Snapshot.SourceId;

    /// <inheritdoc/>
    /// <remarks>Completes synchronously with a fresh capture sharing only immutable bindings. Cancellation is checked before capture creation; later cancellation does not revoke the transfer.</remarks>
    public ValueTask<IToolProviderCapture> DiscoverAsync(ToolDiscoveryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        const string operation = AgentKitActivityNames.ToolProviderDiscover;
        var started = TryGetTimestamp();
        using var scope = AgentKitActivityScope.Start(operation, ActivityKind.Internal, new ActivityTagsCollection
        {
            { AgentKitTagNames.GenAiOperationName, operation },
            { AgentKitTagNames.TenantId, request.Identity.TenantId.Value },
            { AgentKitTagNames.PrincipalId, request.Identity.PrincipalId.Value },
            { AgentKitTagNames.AgentId, request.AgentId.ToString() },
            { AgentKitTagNames.SessionId, request.SessionId.ToString() },
            { AgentKitTagNames.RunId, request.RunId.ToString() },
            { AgentKitTagNames.ToolSourceId, SourceId.Value },
            { AgentKitTagNames.ToolSourceVersion, _bindings.Snapshot.SourceVersion.Value },
        });
        Observe(() => ToolLog.ProviderDiscoveryStarted(_logger, SourceId, request.Identity.TenantId, request.Identity.PrincipalId, request.AgentId, request.SessionId, request.RunId));
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            IToolProviderCapture capture = new ToolProviderCapture(_bindings, null, _timeProvider, _captureLogger);
            Complete(request, scope.Activity, "discovered", started);
            return ValueTask.FromResult(capture);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Complete(request, scope.Activity, "cancelled", started);
            throw;
        }
    }

    private void Complete(ToolDiscoveryRequest request, Activity? activity, string outcome, long? started)
    {
        Debug.Assert(request is not null, "Discovery validated its immutable input before diagnostics.");
        Debug.Assert(outcome is "discovered" or "cancelled", "Static discovery has a closed outcome vocabulary.");
        var level = outcome == "discovered" ? LogLevel.Debug : LogLevel.Information;
        Observe(() => ToolLog.ProviderDiscoveryCompleted(_logger, level, SourceId, _bindings.Snapshot.SourceVersion, request.Identity.TenantId, request.Identity.PrincipalId, request.AgentId, request.SessionId, request.RunId, outcome));
        Observe(() =>
        {
            if (outcome == "discovered") { activity.SetSuccessful(outcome); }
            else { activity.SetFailed(outcome, outcome); }
        });
        var tags = new TagList { { AgentKitTagNames.Outcome, outcome } };
        Observe(() => ToolProviderDiscoveryMetrics.Count.Add(1, tags));
        if (started is { } timestamp)
        {
            Observe(() =>
            {
                var elapsed = _timeProvider.GetElapsedTime(timestamp);
                if (elapsed >= TimeSpan.Zero) { ToolProviderDiscoveryMetrics.Duration.Record(elapsed.TotalSeconds, tags); }
            });
        }
    }

    private long? TryGetTimestamp()
    {
        try { return _timeProvider.GetTimestamp(); }
        catch { return null; }
    }

    private static void Observe(Action observation)
    {
        Debug.Assert(observation is not null, "The provider supplies every observation callback.");
        try { observation(); }
        catch { /* Observers cannot change publication or capture ownership. */ }
    }
}
