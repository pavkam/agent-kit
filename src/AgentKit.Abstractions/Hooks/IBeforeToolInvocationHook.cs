// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Runs at <see cref="AgentHookPoints.BeforeToolInvocation"/>, before each tool call is validated, authorized, and invoked.</summary>
/// <remarks>
/// A hook may rewrite the arguments or veto the call with a safe reason. It cannot change the call identity or the
/// tool, and it cannot grant or bypass authorization: rewritten arguments still pass schema validation and the
/// security authority. The loop dispatches this point with <see cref="HookFailureMode.FailOperation"/>: a throwing
/// hook fails the turn. Register implementations additively as <see cref="IBeforeToolInvocationHook"/> singletons.
/// </remarks>
public interface IBeforeToolInvocationHook: IHook
{
    /// <summary>Inspects, rewrites, or vetoes the call about to be invoked.</summary>
    /// <param name="args">The call and its writable arguments and veto.</param>
    /// <param name="cancellationToken">Cancels the hook; cancellation propagates to the run.</param>
    /// <returns>A task that completes when the hook has finished.</returns>
    public ValueTask OnBeforeToolInvocationAsync(BeforeToolInvocationEventArgs args, CancellationToken cancellationToken = default);
}
