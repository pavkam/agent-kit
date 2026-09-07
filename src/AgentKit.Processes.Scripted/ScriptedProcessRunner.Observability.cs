// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Scripted;

public sealed partial class ScriptedProcessRunner
{
    /// <inheritdoc/>
    public ValueTask<ProcessRunResult> RunAsync(
        ProcessRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ScriptedProcessObservability.ObserveAsync(
            _logger,
            AgentKitActivityNames.ProcessRun,
            "run",
            request.Intent.Request.Id,
            request.Grant.RequestId,
            token => RunCoreAsync(request, token),
            static result => result.Status.ToString(),
            static result => result.Status == ProcessRunStatus.Exited,
            cancellationToken);
    }
}
