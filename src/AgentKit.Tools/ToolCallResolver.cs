// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Diagnostics;

/// <summary>Resolves provider aliases against an exact captured catalog and acquires the matching invoker lease.</summary>
public sealed class ToolCallResolver: IToolResolver
{
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ToolCallResolver> _logger;

    /// <summary>Initializes the first-party tool resolver.</summary>
    /// <param name="timeProvider">The replaceable clock used for resolution timestamps.</param>
    /// <param name="logger">The type-specific structured logger.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public ToolCallResolver(TimeProvider timeProvider, ILogger<ToolCallResolver> logger)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask<ToolResolutionResult> ResolveAsync(
        IToolCatalogCapture capture,
        ToolCallRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(request);

        var snapshot = capture.Snapshot;
        if (request.CatalogVersion != snapshot.Version)
        {
            ToolLog.Unknown(_logger, request.CallId, new ToolId(request.ProviderAlias.Value));
            return new ToolCallUnresolved(
                request,
                ToolTerminalStatus.Unsupported,
                "The call's catalog version does not match the retained capture.");
        }

        if (!snapshot.ProviderAliases.TryGetValue(request.ProviderAlias, out var identity))
        {
            ToolLog.Unknown(_logger, request.CallId, new ToolId(request.ProviderAlias.Value));
            return new ToolCallUnresolved(
                request,
                ToolTerminalStatus.UnknownTool,
                $"Tool '{request.ProviderAlias.Value}' is not registered.");
        }

        ToolDescriptor? descriptor = null;
        foreach (var tool in snapshot.Tools)
        {
            if (tool.Id == identity.Id && tool.Version == identity.Version)
            {
                descriptor = tool;
                break;
            }
        }

        if (descriptor is null || !snapshot.ExecutionPolicies.TryGetValue(identity, out var executionPolicy))
        {
            Debug.Assert(descriptor is not null, "Catalog snapshots require policy evidence for every selected descriptor.");
            return new ToolCallUnresolved(
                request,
                ToolTerminalStatus.Unsupported,
                "The resolved tool is unavailable from the retained catalog.");
        }

        IToolInvokerLease? lease = null;
        try
        {
            var acquired = await capture.AcquireInvokerAsync(identity, cancellationToken).ConfigureAwait(false);
            if (acquired is not ToolInvokerAcquired success)
            {
                return new ToolCallUnresolved(
                    request,
                    ToolTerminalStatus.Unsupported,
                    "The resolved tool is unavailable from the retained catalog.");
            }

            lease = success.Lease;
            var resolvedAt = _timeProvider.GetUtcNow();
            var resolved = new ResolvedToolCall(
                request.AgentId,
                request.SessionId,
                request.RunId,
                request.TurnId,
                request.OperationId,
                request.CallId,
                request.Authorization,
                request.CatalogVersion,
                request.ProviderAlias,
                descriptor,
                descriptor.Version,
                executionPolicy,
                request.SourceOrdinal,
                request.RawArguments,
                request.RequestedAt,
                resolvedAt);
            return new ToolCallResolved(resolved, lease);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (lease is not null)
            {
                await lease.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }
        catch (Exception exception)
        {
            if (lease is not null)
            {
                await lease.DisposeAsync().ConfigureAwait(false);
            }

            ToolLog.Failed(_logger, request.CallId, descriptor.Id, exception.GetType().Name);
            return new ToolCallUnresolved(
                request,
                ToolTerminalStatus.Unsupported,
                "The tool could not be resolved against the retained catalog.");
        }
    }
}
