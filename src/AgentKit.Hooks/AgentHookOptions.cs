// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

/// <summary>
/// Host-level ceilings for the first-party <see cref="DefaultHookDispatcher"/>. Every value here is a hard bound
/// that composes monotonically with the per-call arguments of
/// <see cref="IHookDispatcher.DispatchAsync{THook,TArgs}"/>: a caller may tighten a dispatch beyond these values but
/// can never relax past them.
/// </summary>
/// <remarks>
/// <para>
/// The options are read once when the dispatcher is constructed, so changes after construction do not affect an
/// existing dispatcher. Register them through <c>AddAgentHooks(Action&lt;AgentHookOptions&gt;?)</c>, which validates
/// them at startup and rejects out-of-range values before any dispatch runs.
/// </para>
/// <para>
/// Only the members with a live consumer in the dispatcher are present; timeout, mutation-dispatch, and reload
/// settings described by the hooks architecture are added when the dispatcher honors them.
/// </para>
/// </remarks>
public sealed class AgentHookOptions
{
    /// <summary>
    /// Gets or sets the host's hard ceiling on how many dispatches of one hook point may be simultaneously active on
    /// a single call path.
    /// </summary>
    /// <value>
    /// A positive count. The default of 8 leaves the ordinary per-call default of 1 untouched while still bounding
    /// callers that opt into deeper reentrancy.
    /// </value>
    /// <remarks>
    /// The effective limit for one dispatch is the smaller of the caller's <c>maxReentrantDepth</c> argument and
    /// this value. A caller asking for more depth than the host permits is clamped to this ceiling and the dispatcher
    /// records a diagnostic; a caller asking for less keeps its own tighter limit. Values below 1 fail options
    /// validation and dispatcher construction.
    /// </remarks>
    public int MaximumInvocationDepth { get; set; } = 8;

    /// <summary>
    /// Gets or sets the least strict <see cref="HookFailureMode"/> the host permits for any dispatch.
    /// </summary>
    /// <value>
    /// A defined <see cref="HookFailureMode"/>. The default of <see cref="HookFailureMode.IsolateAndDiagnose"/> is the least
    /// strict mode, so it leaves every caller's requested mode unchanged.
    /// </value>
    /// <remarks>
    /// Failure modes form the strictness order <see cref="HookFailureMode.IsolateAndDiagnose"/> &lt;
    /// <see cref="HookFailureMode.FailOperation"/>. The effective mode for one dispatch is the stricter of the
    /// caller's <c>failureMode</c> argument and this value: setting <see cref="HookFailureMode.FailOperation"/> here
    /// escalates every isolating dispatch to fail the owning operation, while a caller that already requested
    /// <see cref="HookFailureMode.FailOperation"/> is never relaxed to isolation. Undefined values fail options
    /// validation and dispatcher construction.
    /// </remarks>
    public HookFailureMode MinimumFailureMode { get; set; } = HookFailureMode.IsolateAndDiagnose;

    /// <summary>
    /// Gets or sets the host's default maximum duration for one hook dispatch when the caller does not supply an
    /// earlier deadline.
    /// </summary>
    /// <value>A positive duration. The default is ten seconds.</value>
    /// <remarks>WS2-C12 enforces this ceiling during dispatch; options validation rejects non-positive values today.</remarks>
    public TimeSpan DefaultHookTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets or sets the mutation dispatch mode the host permits.</summary>
    /// <value>
    /// A defined <see cref="HookMutationDispatchMode"/>. The default is <see cref="HookMutationDispatchMode.Sequential"/>.
    /// </value>
    /// <remarks>Only sequential dispatch is implemented; selecting <see cref="HookMutationDispatchMode.Concurrent"/> fails validation.</remarks>
    public HookMutationDispatchMode MutationDispatchMode { get; set; } = HookMutationDispatchMode.Sequential;

    /// <summary>Gets or sets when profile reload may produce a newly captured catalog.</summary>
    /// <value>A defined <see cref="HookReloadBoundary"/>. The default is <see cref="HookReloadBoundary.NextRun"/>.</value>
    public HookReloadBoundary ReloadBoundary { get; set; } = HookReloadBoundary.NextRun;
}
