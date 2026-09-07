// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes;

public sealed partial class OperatingSystemProcessIntentResolver
{
    /// <inheritdoc/>
    public ValueTask<ProcessResolutionResult> ResolveAsync(
        ProcessResolveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ProcessObservability.ObserveAsync(
            _logger,
            AgentKitActivityNames.ProcessResolve,
            "resolve",
            request.Id,
            securityRequestId: null,
            token => ResolveCoreAsync(request, token),
            static result => result.Status.ToString(),
            static result => result.Status == ProcessResolutionStatus.Resolved,
            cancellationToken);
    }
}
