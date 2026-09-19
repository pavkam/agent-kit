// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Fans out one invocation diagnostic to every registered <see cref="IHookDiagnosticSink"/>.</summary>
/// <remarks>
/// The engine-wide singleton implementation isolates each sink's failure so one faulty sink cannot suppress
/// diagnostics for the rest or affect the dispatch kernel's semantic outcome.
/// </remarks>
public interface IHookDiagnosticDispatcher
{
    /// <summary>Publishes one invocation diagnostic to every registered sink.</summary>
    /// <param name="diagnostic">The content-free invocation diagnostic.</param>
    /// <param name="cancellationToken">Cancels publication.</param>
    /// <exception cref="ArgumentNullException"><paramref name="diagnostic"/> is null.</exception>
    public ValueTask PublishAsync(
        HookInvocationDiagnostic diagnostic,
        CancellationToken cancellationToken);
}
