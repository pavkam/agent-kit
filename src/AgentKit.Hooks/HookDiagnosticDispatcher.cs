// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

/// <summary>
/// Fans out content-free hook invocation diagnostics to every registered <see cref="IHookDiagnosticSink"/> while
/// isolating sink faults from the dispatch kernel.
/// </summary>
public sealed class HookDiagnosticDispatcher: IHookDiagnosticDispatcher
{
    private readonly IEnumerable<IHookDiagnosticSink> _sinks;

    /// <summary>Initializes a dispatcher with zero or more sinks.</summary>
    /// <param name="sinks">Every sink registered through <c>AddHookDiagnosticSink&lt;T&gt;</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sinks"/> is null.</exception>
    public HookDiagnosticDispatcher(IEnumerable<IHookDiagnosticSink> sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        _sinks = sinks;
    }

    /// <inheritdoc/>
    public async ValueTask PublishAsync(HookInvocationDiagnostic diagnostic, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var sink in _sinks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await sink.PublishAsync(diagnostic, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
            }
        }
    }
}
