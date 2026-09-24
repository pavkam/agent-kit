// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Scripted;

public sealed partial class ScriptedProcessExecutor
{
    /// <inheritdoc/>
    public ValueTask<ProcessStartResult> StartAsync(
        ResolvedProcessStart request,
        SecurityGrant grant,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(grant);
        return ScriptedProcessObservability.ObserveAsync(
            _logger,
            AgentKitActivityNames.ProcessRun,
            "start",
            request.Request.Id,
            grant.RequestId,
            token => StartCoreAsync(request, grant, token),
            static result => result switch
            {
                ProcessHandleStarted => "started",
                ProcessStartDenied => "denied",
                ProcessStartResolutionFailed => "resolution_failed",
                ProcessStartFailed => "failed",
                ProcessStartCancelled => "cancelled",
                ProcessStartSandboxUnavailable => "sandbox_unavailable",
                _ => "unknown",
            },
            static result => result is ProcessHandleStarted,
            cancellationToken);
    }
}
