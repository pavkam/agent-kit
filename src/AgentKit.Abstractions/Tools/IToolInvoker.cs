// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Performs one already validated and authorized tool invocation attempt for a resolved descriptor.
/// </summary>
/// <remarks>
/// A spec-shaped invoker never discovers tools, authorizes calls, or records terminal session state.
/// It receives a bounded <see cref="ToolInvocationContext"/> and returns raw <see cref="ToolInvocationResult"/>
/// evidence for the executor to normalize into an authoritative <see cref="ToolCallResult"/>.
/// An <see cref="OperationCanceledException"/> propagates only when the caller-supplied cancellation token
/// is signaled; an implementation-thrown cancellation without caller cancellation is normalized as a failed
/// invocation by the caller.
/// </remarks>
public interface IToolInvoker
{
    /// <summary>Invokes one authorized attempt for the resolved tool named in <paramref name="context"/>.</summary>
    /// <param name="context">The restricted immutable context for this attempt.</param>
    /// <param name="cancellationToken">Propagates caller cancellation for this attempt.</param>
    /// <returns>The raw terminal outcome and normalized content parts for this attempt.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> is canceled before the attempt settles.
    /// </exception>
    public ValueTask<ToolInvocationResult> InvokeAsync(
        ToolInvocationContext context,
        CancellationToken cancellationToken = default);
}
