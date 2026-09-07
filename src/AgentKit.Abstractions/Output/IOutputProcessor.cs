// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Extracts and validates a terminal model response against an output definition, returning a typed decision.</summary>
/// <remarks>
/// <para>
/// This is a deliberately reduced stand-in for the fuller
/// <c>IOutputProcessor</c> described by the structured-output architecture,
/// which additionally accepts a live <c>HookDispatchContext</c> so hooks can
/// tighten validation, redact safe diagnostics, or replace bounded repair
/// text. Until the not-yet-implemented hook-context wiring reaches this
/// call site, a processor implementation performs no hook dispatch, mirroring
/// how <c>DefaultAgentLoop</c> also defers hook dispatch integration.
/// </para>
/// <para>
/// An implementation never calls the provider, loop, tool executor, or
/// output publisher itself; it returns exactly one typed decision — accept,
/// retry, or reject — and lets the caller act on it.
/// </para>
/// </remarks>
public interface IOutputProcessor
{
    /// <summary>Processes one terminal model response.</summary>
    /// <param name="request">The processing request.</param>
    /// <param name="cancellationToken">A token used to cancel processing.</param>
    /// <returns>A task producing the closed processing outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<OutputProcessingResult> ProcessAsync(
        OutputProcessingRequest request, CancellationToken cancellationToken = default);
}
