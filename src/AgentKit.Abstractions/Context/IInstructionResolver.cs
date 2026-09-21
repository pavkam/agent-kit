// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Resolves declared instruction sources into ordered instruction messages for one model request.</summary>
public interface IInstructionResolver
{
    /// <summary>Resolves instruction sources for one assembly evaluation.</summary>
    /// <param name="request">The immutable resolution evidence.</param>
    /// <param name="cancellationToken">Cancels resolution before it returns.</param>
    /// <returns>A closed resolution outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    public ValueTask<InstructionResolutionResult> ResolveAsync(
        InstructionResolutionRequest request,
        CancellationToken cancellationToken = default);
}
