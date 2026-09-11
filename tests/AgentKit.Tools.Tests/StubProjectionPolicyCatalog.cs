// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

internal sealed class StubProjectionPolicyCatalog: IToolResultProjectionPolicyCatalog
{
    public ValueTask<ToolResultProjectionPolicyResolution> ResolveAsync(ToolResultProjectionPolicyReference reference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<ToolResultProjectionPolicyResolution>(new ToolResultProjectionPolicyUnavailable(reference));
    }
}
