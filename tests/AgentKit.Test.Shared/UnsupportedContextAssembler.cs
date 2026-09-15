// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>
/// An <see cref="IContextAssembler"/> test double that throws
/// <see cref="NotSupportedException"/> whenever context assembly is
/// requested.
/// </summary>
/// <remarks>
/// Useful as a placeholder collaborator for tests whose scripted
/// <see cref="IAgentLoop"/> never actually reaches context assembly, so the
/// composed <see cref="AgentRunServices"/> bundle can still be resolved
/// without a behaviorally meaningful fake.
/// </remarks>
public sealed class UnsupportedContextAssembler: IContextAssembler
{
    /// <inheritdoc/>
    public Task<ContextAssemblyResult> AssembleAsync(
        ContextAssemblyRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This test double does not support context assembly.");
}
