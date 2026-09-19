// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Runs one complete batch of raw tool-call requests through resolution, validation, authorization, scheduling, invocation, normalization, and recording.</summary>
/// <remarks>
/// This is the sole tool-execution surface the agent loop depends on; every other pipeline stage — resolver,
/// argument validator, execution-policy selector, security authority, recorder, scheduler, normalizer, projection
/// catalog, projector, hooks, and event sinks — is an internal collaborator composed behind it. An invoker performs
/// one already validated and authorized attempt; this interface owns the surrounding pipeline that gets a raw call
/// there and turns its raw evidence into one authoritative <see cref="ToolCallResult"/> per call, including a
/// pre-invocation rejection for an unknown, invalid, denied, or ambiguous call. Ship note: the normative shape also
/// carries a <c>HookDispatchContext hooks</c> parameter; that parameter is omitted here until the hook kernel
/// (workstream 2) publishes <c>HookDispatchContext</c>, and an implementation resolves the shared
/// <see cref="IHookDispatcher"/> directly in the interim.
/// </remarks>
public interface IToolExecutor
{
    /// <summary>Resolves, validates, authorizes, schedules, invokes, normalizes, and records every call in one batch.</summary>
    /// <param name="capture">The retained catalog capture every call in <paramref name="calls"/> resolves against.</param>
    /// <param name="calls">The raw provider-emitted call requests to execute together.</param>
    /// <param name="capability">The invocation-only session and budget capabilities bound to this batch.</param>
    /// <param name="cancellationToken">Signals cancellation; already-accepted calls still settle to a terminal result.</param>
    /// <returns>A task producing one authoritative terminal result per call in <paramref name="calls"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="capture"/>, <paramref name="calls"/>'s element, or <paramref name="capability"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="calls"/> is uninitialized.</exception>
    public Task<ToolBatchResult> ExecuteAsync(
        IToolCatalogCapture capture,
        ImmutableArray<ToolCallRequest> calls,
        ToolExecutionCapability capability,
        CancellationToken cancellationToken = default);
}
