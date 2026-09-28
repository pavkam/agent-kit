// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Extracts and validates a terminal model response against an output definition, returning a typed decision.</summary>
/// <remarks>
/// An implementation never calls the provider, loop, tool executor, or output publisher itself; it returns exactly
/// one typed decision — accept, retry, or reject — and lets the caller act on it. When a
/// <see cref="HookDispatchContext"/> is supplied, the processor may dispatch
/// <see cref="AgentHookPoints.OutputValidating"/> before accepting a candidate.
/// </remarks>
public interface IOutputProcessor
{
    /// <summary>Processes one terminal model response.</summary>
    /// <param name="request">The processing request.</param>
    /// <param name="cancellationToken">A token used to cancel processing.</param>
    /// <returns>A task producing the closed processing outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<OutputProcessingResult> ProcessAsync(
        OutputProcessingRequest request, CancellationToken cancellationToken = default) =>
        ProcessAsync(request, hooks: null, cancellationToken);

    /// <summary>Processes one terminal model response with optional hook dispatch.</summary>
    /// <param name="request">The processing request.</param>
    /// <param name="hooks">The hook dispatch context for this validation, when the run activated hooks.</param>
    /// <param name="cancellationToken">A token used to cancel processing.</param>
    /// <returns>A task producing the closed processing outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<OutputProcessingResult> ProcessAsync(
        OutputProcessingRequest request,
        HookDispatchContext? hooks,
        CancellationToken cancellationToken = default);
}
