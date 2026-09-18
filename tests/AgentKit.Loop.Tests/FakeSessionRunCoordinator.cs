// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

/// <summary>
/// A minimal <see cref="ISessionRunCoordinator"/> test double. <see cref="DefaultAgentLoop"/>'s lane release only
/// needs a non-null instance to compose a valid <see cref="SessionExecutionCapability"/>; it never calls
/// <see cref="AcquireAsync"/>, so this fake throws if that assumption ever changes.
/// </summary>
internal sealed class FakeSessionRunCoordinator: ISessionRunCoordinator
{
    /// <inheritdoc/>
    public ValueTask<SessionRunLeaseResult> AcquireAsync(
        SessionRunLeaseRequest request, SessionExecutionCapability session, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This fake is not expected to be used for lease acquisition.");
}
