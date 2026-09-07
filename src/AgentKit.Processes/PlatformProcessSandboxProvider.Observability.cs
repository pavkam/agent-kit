// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes;

public sealed partial class PlatformProcessSandboxProvider
{
    /// <inheritdoc/>
    public ValueTask<ProcessSandboxResult> PrepareAsync(
        ResolvedProcessIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);
        return ProcessObservability.ObserveAsync(
            _logger,
            AgentKitActivityNames.ProcessSandboxPrepare,
            "sandbox_prepare",
            intent.Request.Id,
            securityRequestId: null,
            token => PrepareCoreAsync(intent, token),
            static result => result.Status.ToString(),
            static result => result.Status == ProcessSandboxStatus.Ready,
            cancellationToken);
    }
}
