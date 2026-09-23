// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes;

public sealed partial class PlatformProcessSandboxProvider
{
    /// <inheritdoc/>
    public ValueTask<ProcessSandboxResult> CreateAsync(
        ProcessSandboxRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ProcessObservability.ObserveAsync(
            _logger,
            AgentKitActivityNames.ProcessSandboxPrepare,
            "sandbox_prepare",
            request.OperationId,
            securityRequestId: null,
            token => PrepareCoreAsync(request, token),
            static result => result.Status.ToString(),
            static result => result.Status == ProcessSandboxStatus.Ready,
            cancellationToken);
    }
}
