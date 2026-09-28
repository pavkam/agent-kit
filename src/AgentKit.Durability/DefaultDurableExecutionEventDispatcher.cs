// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

/// <summary>Publishes durable execution events to ordered sinks selected for the captured profile.</summary>
/// <remarks>
/// Observational sinks are isolated: an unavailable or throwing sink is logged and dispatch continues. A required sink
/// fails the publication closed with <see cref="InvalidOperationException"/> so a caller cannot treat unobserved
/// durable work as observed. Cancellation always propagates and stops later sinks.
/// </remarks>
/// <param name="declarations">The additive sink declarations captured at composition time.</param>
/// <param name="services">The container used to resolve each sink under its declared lifetime.</param>
/// <param name="logger">The optional logger for content-free dispatch diagnostics.</param>
internal sealed class DefaultDurableExecutionEventDispatcher(
    IEnumerable<DurableExecutionEventSinkDeclaration> declarations,
    IServiceProvider services,
    ILogger<DefaultDurableExecutionEventDispatcher>? logger = null): IDurableExecutionEventDispatcher
{
    private readonly ImmutableArray<DurableExecutionEventSinkDeclaration> _declarations =
        [.. declarations.OrderBy(static declaration => declaration.Registration.Order)];
    private readonly ILogger _logger = logger ?? (ILogger) NullLogger.Instance;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="executionEvent"/> is null.</exception>
    /// <exception cref="InvalidOperationException">A required sink is unavailable or failed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async ValueTask PublishAsync(
        DurableExecutionContext context,
        DurableExecutionEvent executionEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(executionEvent);
        cancellationToken.ThrowIfCancellationRequested();

        using var scope = AgentKitActivityScope.Start(AgentKitActivityNames.DurableDispatch, ActivityKind.Internal);
        foreach (var declaration in _declarations)
        {
            if (!declaration.Registration.Observes(context.ProfileKey))
            {
                continue;
            }

            var sinkId = declaration.Registration.Id.Value;
            var required = declaration.Registration.Delivery == DurableExecutionEventDelivery.Required;
            if (services.GetService(declaration.SinkType) is not IDurableExecutionEventSink sink)
            {
                if (required)
                {
                    Observe(() =>
                    {
                        DurabilityMetrics.RecordEventDispatch(DurableEventDispatchOutcome.RequiredSinkUnavailable);
                        DurabilityLog.RequiredEventSinkUnavailable(_logger, sinkId);
                    });
                    scope.Activity.SetFailed(
                        DurableEventDispatchOutcome.RequiredSinkUnavailable.ToStableValue(),
                        nameof(InvalidOperationException));
                    throw new InvalidOperationException(
                        $"Required durable execution event sink '{sinkId}' is not available.");
                }

                Observe(() =>
                {
                    DurabilityMetrics.RecordEventDispatch(DurableEventDispatchOutcome.SinkSkipped);
                    DurabilityLog.EventSinkSkipped(_logger, sinkId);
                });
                continue;
            }

            try
            {
                await sink.PublishAsync(executionEvent, cancellationToken).ConfigureAwait(false);
                Observe(static () => DurabilityMetrics.RecordEventDispatch(DurableEventDispatchOutcome.Published));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Observe(static () => DurabilityMetrics.RecordEventDispatch(DurableEventDispatchOutcome.Cancelled));
                scope.Activity.SetFailed(
                    DurableEventDispatchOutcome.Cancelled.ToStableValue(),
                    nameof(OperationCanceledException));
                throw;
            }
            catch (Exception exception) when (!required)
            {
                Observe(() =>
                {
                    DurabilityMetrics.RecordEventDispatch(DurableEventDispatchOutcome.SinkFailed);
                    DurabilityLog.EventSinkFailed(_logger, sinkId, exception.GetType().Name);
                });
            }
            catch (Exception exception)
            {
                Observe(() =>
                {
                    DurabilityMetrics.RecordEventDispatch(DurableEventDispatchOutcome.RequiredSinkFailed);
                    DurabilityLog.EventSinkFailed(_logger, sinkId, exception.GetType().Name);
                });
                scope.Activity.SetFailed(
                    DurableEventDispatchOutcome.RequiredSinkFailed.ToStableValue(),
                    exception.GetType().Name);
                throw new InvalidOperationException(
                    $"Required durable execution event sink '{sinkId}' failed.",
                    exception);
            }
        }

        scope.Activity.SetSuccessful(DurableEventDispatchOutcome.Published.ToStableValue());
    }

    /// <summary>Runs one observational emission so a failing logging or metrics provider cannot change dispatch.</summary>
    /// <param name="observation">The non-null emission to attempt.</param>
    private static void Observe(Action observation)
    {
        Debug.Assert(observation is not null, "An observational delegate is required.");
        try
        {
            observation();
        }
        catch
        {
            // Instrumentation is observational. A failing provider never changes whether a sink observed the event.
        }
    }
}
