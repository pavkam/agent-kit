// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Delivers immutable memory events to the sinks registered for a profile, in registration order.</summary>
/// <remarks>A sink that is not registered for the event's profile never receives it. A missing or failing sink is counted, never thrown: observational failures are isolated, and required failures are reported in the result so fail-closed callers decide. Cancellation always propagates.</remarks>
internal sealed class DefaultMemoryEventDispatcher: IMemoryEventDispatcher
{
    private readonly ImmutableArray<MemoryEventSinkDeclaration> _declarations;
    private readonly IServiceProvider _services;
    private readonly ILogger<DefaultMemoryEventDispatcher> _logger;

    /// <summary>Initializes the dispatcher.</summary>
    /// <param name="declarations">Every registered sink declaration.</param>
    /// <param name="services">The container the declared sink types are resolved from.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public DefaultMemoryEventDispatcher(
        IEnumerable<MemoryEventSinkDeclaration> declarations,
        IServiceProvider services,
        ILogger<DefaultMemoryEventDispatcher>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(declarations);
        ArgumentNullException.ThrowIfNull(services);
        _declarations = [.. declarations.OrderBy(static declaration => declaration.Registration.Order)];
        _services = services;
        _logger = logger ?? NullLogger<DefaultMemoryEventDispatcher>.Instance;
    }

    /// <inheritdoc/>
    public async ValueTask<MemoryEventDispatchResult> PublishAsync(MemoryProfileKey profile, MemoryEvent memoryEvent, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.Value, nameof(profile));
        ArgumentNullException.ThrowIfNull(memoryEvent);
        cancellationToken.ThrowIfCancellationRequested();
        if (_declarations.IsEmpty)
        {
            return MemoryEventDispatchResult.None;
        }

        var delivered = 0;
        var observationalFailures = 0;
        var requiredFailures = 0;
        using var scope = _services.CreateScope();
        foreach (var declaration in _declarations)
        {
            if (!declaration.Registration.Observes(profile))
            {
                continue;
            }

            var sinkId = declaration.Registration.Id.Value;
            var required = declaration.Registration.Delivery == MemoryEventDelivery.Required;
            if (scope.ServiceProvider.GetService(declaration.SinkType) is not IMemoryEventSink sink)
            {
                MemoryObservation.Safe(() => MemoryLog.EventSinkUnavailable(_logger, sinkId, required));
                Count(required, ref observationalFailures, ref requiredFailures);
                continue;
            }

            try
            {
                await sink.PublishAsync(memoryEvent, cancellationToken).ConfigureAwait(false);
                delivered++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                MemoryObservation.Safe(() => MemoryLog.EventSinkFailed(_logger, sinkId, exception.GetType().Name));
                Count(required, ref observationalFailures, ref requiredFailures);
            }
        }

        return new MemoryEventDispatchResult(delivered, observationalFailures, requiredFailures);
    }

    private static void Count(bool required, ref int observationalFailures, ref int requiredFailures)
    {
        if (required)
        {
            requiredFailures++;
        }
        else
        {
            observationalFailures++;
        }
    }
}
