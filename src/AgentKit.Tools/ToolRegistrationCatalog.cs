// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Collections.Frozen;

/// <summary>Selects authored toolsets and exact source providers from one immutable materialized registration view.</summary>
/// <remarks>This concurrently callable catalog owns neither providers nor captures. It validates all published source references at construction and performs no discovery, container lookup, alias inference, or authorization during selection.</remarks>
public sealed class ToolRegistrationCatalog: IToolRegistrationCatalog
{
    private readonly FrozenDictionary<ToolsetKey, ToolsetPublication> _toolsets;
    private readonly FrozenDictionary<ToolSourceId, ToolProviderBinding> _providers;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ToolRegistrationCatalog> _logger;

    /// <summary>Captures exact publications and borrowed provider bindings after validating the complete composition.</summary>
    /// <param name="toolsets">Nonnull publications with unique exact toolset keys, including valid empty toolsets.</param>
    /// <param name="providers">Nonnull borrowed bindings with unique exact source identities.</param>
    /// <param name="timeProvider">The nonnull clock used only for diagnostic duration.</param>
    /// <param name="logger">The nonnull type-specific safe logger.</param>
    /// <exception cref="ArgumentNullException">A dependency, input sequence, or sequence element is null.</exception>
    /// <exception cref="ArgumentException">A toolset or source key is duplicated, or a published source has no binding.</exception>
    /// <remarks>Enumerates each input once, copies registration maps, reads no live provider metadata, and neither discovers nor disposes a provider. Later mutations of input collections cannot alter this view.</remarks>
    public ToolRegistrationCatalog(IEnumerable<ToolsetPublication> toolsets, IEnumerable<ToolProviderBinding> providers,
        TimeProvider timeProvider, ILogger<ToolRegistrationCatalog> logger)
    {
        ArgumentNullException.ThrowIfNull(toolsets);
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        var capturedProviders = new Dictionary<ToolSourceId, ToolProviderBinding>();
        foreach (var binding in providers)
        {
            ArgumentNullException.ThrowIfNull(binding, nameof(providers));
            ArgumentException.ThrowIfNotEqual(capturedProviders.TryAdd(binding.SourceId, binding), true, nameof(providers));
        }
        var capturedToolsets = new Dictionary<ToolsetKey, ToolsetPublication>();
        foreach (var publication in toolsets)
        {
            ArgumentNullException.ThrowIfNull(publication, nameof(toolsets));
            ArgumentException.ThrowIfNotEqual(capturedToolsets.TryAdd(publication.Key, publication), true, nameof(toolsets));
            foreach (var source in publication.Sources)
            {
                ArgumentException.ThrowIfNotEqual(capturedProviders.ContainsKey(source.SourceId), true, nameof(toolsets));
            }
        }
        _toolsets = capturedToolsets.ToFrozenDictionary();
        _providers = capturedProviders.ToFrozenDictionary();
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public ToolDiscoverySelection ResolveSelection(ToolDiscoveryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        const string operation = AgentKitActivityNames.ToolRegistrationSelect;
        var started = TryGetTimestamp();
        using var scope = AgentKitActivityScope.Start(operation, ActivityKind.Internal, new ActivityTagsCollection
        {
            { AgentKitTagNames.GenAiOperationName, operation },
            { AgentKitTagNames.TenantId, request.Identity.TenantId.Value },
            { AgentKitTagNames.PrincipalId, request.Identity.PrincipalId.Value },
            { AgentKitTagNames.AgentId, request.AgentId.ToString() },
            { AgentKitTagNames.SessionId, request.SessionId.ToString() },
            { AgentKitTagNames.RunId, request.RunId.ToString() },
        });
        Observe(() => ToolLog.RegistrationSelectionStarted(_logger, request.Identity.TenantId, request.Identity.PrincipalId, request.AgentId, request.SessionId, request.RunId));
        var outcome = "failed";
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var toolsets = ImmutableArray.CreateBuilder<ToolsetPublication>(request.Toolsets.Length);
            var providers = ImmutableArray.CreateBuilder<ToolProviderBinding>();
            var selected = new HashSet<ToolSourceId>();
            foreach (var authored in request.Toolsets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!_toolsets.TryGetValue(authored.Key, out var publication) || publication.ExecutionPolicy.Key != authored.ExecutionPolicyKey)
                {
                    outcome = "unavailable";
                    throw new InvalidOperationException("The selected toolset or execution-policy family is unavailable in this registration view.");
                }
                toolsets.Add(publication);
                foreach (var source in publication.Sources)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (selected.Add(source.SourceId)) { providers.Add(_providers[source.SourceId]); }
                }
            }
            var selection = new ToolDiscoverySelection(request, toolsets.ToImmutable(), providers.ToImmutable());
            cancellationToken.ThrowIfCancellationRequested();
            outcome = "selected";
            return selection;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            outcome = "cancelled";
            throw;
        }
        finally { Complete(request, scope.Activity, outcome, started); }
    }

    private void Complete(ToolDiscoveryRequest request, Activity? activity, string outcome, long? started)
    {
        Debug.Assert(request is not null, "Selection validates its request before observation.");
        Debug.Assert(outcome is "selected" or "unavailable" or "cancelled" or "failed", "Selection has a closed terminal vocabulary.");
        var level = outcome switch { "selected" => LogLevel.Debug, "cancelled" => LogLevel.Information, "unavailable" => LogLevel.Warning, _ => LogLevel.Error };
        Observe(() => ToolLog.RegistrationSelectionCompleted(_logger, level, request.Identity.TenantId, request.Identity.PrincipalId, request.AgentId, request.SessionId, request.RunId, outcome));
        Observe(() =>
        {
            if (outcome == "selected") { activity.SetSuccessful(outcome); }
            else { activity.SetFailed(outcome, outcome); }
        });
        var tags = new TagList { { AgentKitTagNames.Outcome, outcome } };
        Observe(() => ToolRegistrationMetrics.Count.Add(1, tags));
        if (started is { } timestamp)
        {
            Observe(() =>
            {
                var elapsed = _timeProvider.GetElapsedTime(timestamp);
                if (elapsed >= TimeSpan.Zero) { ToolRegistrationMetrics.Duration.Record(elapsed.TotalSeconds, tags); }
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
        Debug.Assert(observation is not null, "The registration catalog supplies each observation callback.");
        try { observation(); }
        catch { /* Observation cannot change registration selection or provider ownership. */ }
    }
}
