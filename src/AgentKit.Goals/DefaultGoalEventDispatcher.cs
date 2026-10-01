// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Delivers committed goal events to the registered sinks, each activated inside its own scope.</summary>
/// <remarks>
/// Events describe changes that already committed, so delivery can never veto or roll one back. An observational sink that is
/// missing or fails is logged and skipped. A required sink that is missing or fails throws to the caller after the change has
/// committed, because required audit must never be silently dropped; callers treat that as a typed failure to report, not as
/// a failed change. Each sink is constructed inside a scope created for the delivery and disposed afterwards, which honors the
/// sink's declared lifetime; a singleton sink must itself be thread-safe.
/// </remarks>
/// <param name="declarations">Every registered sink declaration.</param>
/// <param name="services">The root provider sinks are activated from.</param>
/// <param name="logger">The optional content-free logger.</param>
internal sealed class DefaultGoalEventDispatcher(
    IEnumerable<GoalEventSinkDeclaration> declarations,
    IServiceProvider services,
    ILogger<DefaultGoalEventDispatcher>? logger = null): IGoalEventDispatcher
{
    private readonly ImmutableArray<GoalEventSinkDeclaration> _declarations =
        [.. declarations.OrderBy(static declaration => declaration.Registration.Order)];
    private readonly ILogger _logger = logger ?? (ILogger) NullLogger.Instance;

    /// <inheritdoc/>
    public async ValueTask PublishAsync(GoalEvent goalEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(goalEvent);
        cancellationToken.ThrowIfCancellationRequested();
        if (_declarations.IsEmpty)
        {
            return;
        }

        using var scope = services.CreateScope();
        foreach (var declaration in _declarations)
        {
            if (!declaration.Registration.Observes(goalEvent.ProfileKey))
            {
                continue;
            }

            var sinkId = declaration.Registration.Id.Value;
            var required = declaration.Registration.Delivery == GoalEventDelivery.Required;
            if (scope.ServiceProvider.GetService(declaration.SinkType) is not IGoalEventSink sink)
            {
                GoalCoordinationObservation.Safe(() => GoalLog.EventSinkUnavailable(_logger, sinkId, required));
                if (required)
                {
                    throw new InvalidOperationException($"Required goal event sink '{sinkId}' is not available.");
                }

                continue;
            }

            try
            {
                await sink.PublishAsync(goalEvent, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                GoalCoordinationObservation.Safe(() => GoalLog.EventSinkFailed(_logger, sinkId, exception.GetType().Name));
                if (required)
                {
                    throw new InvalidOperationException($"Required goal event sink '{sinkId}' failed.", exception);
                }
            }
        }
    }
}
