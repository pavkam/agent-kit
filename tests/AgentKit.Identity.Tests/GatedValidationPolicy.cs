// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;

internal sealed class GatedValidationPolicy(AsyncGate gate): IIdentityValidationPolicy
{
    public async ValueTask<IdentityValidationResult> ValidateAsync(ExecutionIdentity identity, CancellationToken cancellationToken = default) { _ = gate.Entered.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); return null!; }
}
