// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Delivers immutable artifact events to the sinks registered for one coordinator, in deterministic order.</summary>
/// <remarks>
/// Sinks are ordered by registration order then identity and resolved per delivery: singleton sinks come from the root provider and
/// scoped or transient sinks from a scope created for the delivery, so a coordinator never captures a scoped sink. A missing or
/// failing sink is counted and logged, never thrown, and never changes the lifecycle outcome. Cancellation always propagates.
/// </remarks>
internal sealed class ArtifactEventDispatcher: IArtifactEventDispatcher
{
    private readonly ImmutableArray<ArtifactEventSinkDeclaration> _declarations;
    private readonly IServiceProvider _services;
    private readonly ILogger<ArtifactEventDispatcher> _logger;

    /// <summary>Initializes the dispatcher.</summary>
    /// <param name="declarations">Every registered sink declaration.</param>
    /// <param name="services">The container the declared sink types are resolved from.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public ArtifactEventDispatcher(
        IEnumerable<ArtifactEventSinkDeclaration> declarations,
        IServiceProvider services,
        ILogger<ArtifactEventDispatcher>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(declarations);
        ArgumentNullException.ThrowIfNull(services);
        _declarations =
        [
            .. declarations
                .OrderBy(static declaration => declaration.Registration.Order)
                .ThenBy(static declaration => declaration.Registration.Id.Value, StringComparer.Ordinal),
        ];
        _services = services;
        _logger = logger ?? NullLogger<ArtifactEventDispatcher>.Instance;
    }

    /// <inheritdoc/>
    public async ValueTask PublishAsync(
        ComponentKey<IArtifactCoordinator> coordinatorKey,
        ArtifactEvent artifactEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(coordinatorKey.Value, nameof(coordinatorKey));
        ArgumentNullException.ThrowIfNull(artifactEvent);
        cancellationToken.ThrowIfCancellationRequested();
        var applicable = _declarations.Where(declaration => declaration.CoordinatorKey == coordinatorKey).ToArray();
        if (applicable.Length == 0)
        {
            return;
        }

        using var scope = _services.CreateScope();
        foreach (var declaration in applicable)
        {
            var sinkId = declaration.Registration.Id.Value;
            var provider = declaration.Registration.Lifetime == ServiceLifetime.Singleton ? _services : scope.ServiceProvider;
            if (provider.GetService(declaration.SinkType) is not IArtifactEventSink sink)
            {
                ArtifactObservability.Safe(() => ArtifactLog.EventSinkUnavailable(_logger, coordinatorKey.Value, sinkId));
                ArtifactObservability.Safe(() => ArtifactMetrics.RecordSinkFailure("unavailable"));
                continue;
            }

            try
            {
                await sink.PublishAsync(artifactEvent, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                ArtifactObservability.Safe(() => ArtifactLog.EventSinkFailed(_logger, coordinatorKey.Value, sinkId, exception.GetType().Name));
                ArtifactObservability.Safe(() => ArtifactMetrics.RecordSinkFailure("failed"));
            }
        }
    }
}
