// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Observes content-free hook invocation diagnostics.</summary>
/// <remarks>
/// Hosts register zero or more sinks (for example, a metrics exporter or an audit log) through
/// <c>AddHookDiagnosticSink&lt;T&gt;</c>. A sink's failure never mutates a hook invocation's semantic outcome; the
/// dispatcher that owns publication is responsible for isolating sink faults.
/// </remarks>
public interface IHookDiagnosticSink
{
    /// <summary>Publishes one invocation diagnostic to this sink.</summary>
    /// <param name="diagnostic">The content-free invocation diagnostic.</param>
    /// <param name="cancellationToken">Cancels publication.</param>
    /// <exception cref="ArgumentNullException"><paramref name="diagnostic"/> is null.</exception>
    public ValueTask PublishAsync(
        HookInvocationDiagnostic diagnostic,
        CancellationToken cancellationToken);
}
