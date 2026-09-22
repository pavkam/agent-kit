// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Collections.Immutable;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Discovers application-registered <see cref="IToolInvoker"/> instances from composition markers and keyed
/// registrations under <see cref="ApplicationToolSources.Default"/>.
/// </summary>
internal sealed class ApplicationToolProvider: IToolProvider
{
    internal static readonly ToolSourceVersion DefaultSourceVersion = new("1");

    private readonly IServiceProvider _services;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ToolProviderCapture> _captureLogger;

    /// <summary>Initializes the application tool provider.</summary>
    /// <param name="services">The composition root used to resolve keyed invokers during discovery.</param>
    /// <param name="timeProvider">The replaceable clock used for capture timestamps.</param>
    /// <param name="captureLogger">The logger for provider capture diagnostics.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    internal ApplicationToolProvider(
        IServiceProvider services,
        TimeProvider timeProvider,
        ILogger<ToolProviderCapture> captureLogger)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(captureLogger);
        _services = services;
        _timeProvider = timeProvider;
        _captureLogger = captureLogger;
    }

    /// <inheritdoc/>
    public ToolSourceId SourceId => ApplicationToolSources.Default;

    /// <inheritdoc/>
    public ValueTask<IToolProviderCapture> DiscoverAsync(ToolDiscoveryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var registrations = _services.GetServices<RegisteredToolInvoker>().ToArray();
        if (registrations.Length == 0)
        {
            var emptySnapshot = new ToolProviderSnapshot(
                ApplicationToolSources.Default,
                DefaultSourceVersion,
                []);
            var emptyBindings = ImmutableDictionary<ToolIdentity, IToolInvoker>.Empty;
            return ValueTask.FromResult<IToolProviderCapture>(
                new ToolProviderCapture(
                    new ToolProviderBindings(emptySnapshot, emptyBindings),
                    lifetime: null,
                    _timeProvider,
                    _captureLogger));
        }

        var descriptors = ImmutableArray.CreateBuilder<ToolDescriptor>(registrations.Length);
        var invokers = ImmutableDictionary.CreateBuilder<ToolIdentity, IToolInvoker>();
        foreach (var registration in registrations)
        {
            descriptors.Add(registration.Descriptor);
            invokers.Add(registration.Identity, _services.GetRequiredKeyedService<IToolInvoker>(registration.Identity));
        }

        var snapshot = new ToolProviderSnapshot(
            ApplicationToolSources.Default,
            DefaultSourceVersion,
            descriptors.ToImmutable());
        var bindings = new ToolProviderBindings(snapshot, invokers.ToImmutable());
        return ValueTask.FromResult<IToolProviderCapture>(
            new ToolProviderCapture(bindings, lifetime: null, _timeProvider, _captureLogger));
    }
}

