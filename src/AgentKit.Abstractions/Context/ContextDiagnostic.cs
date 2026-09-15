// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Provides bounded, content-safe diagnostic evidence for context assembly.</summary>
/// <remarks>Severity describes evidence only and never overrides a contributor registration's failure policy.</remarks>
public sealed record ContextDiagnostic
{
    /// <summary>Creates immutable diagnostic evidence.</summary>
    /// <param name="severity">The defined diagnostic severity.</param>
    /// <param name="code">A nonblank stable machine-readable code.</param>
    /// <param name="safeMessage">A nonblank content-safe operator message.</param>
    /// <param name="source">The exact source publication when the diagnostic belongs to one.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="severity"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="code"/> or <paramref name="safeMessage"/> is blank.</exception>
    public ContextDiagnostic(ContextDiagnosticSeverity severity, string code, string safeMessage, ContextSourceReference? source = null)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(Enum.IsDefined(severity), true, nameof(severity));
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        Severity = severity;
        Code = code;
        SafeMessage = safeMessage;
        Source = source;
    }

    /// <summary>Gets the diagnostic severity.</summary><value>A defined severity.</value>
    public ContextDiagnosticSeverity Severity { get; }
    /// <summary>Gets the stable diagnostic code.</summary><value>A nonblank code.</value>
    public string Code { get; }
    /// <summary>Gets the content-safe operator message.</summary><value>A nonblank message safe for ordinary diagnostics.</value>
    public string SafeMessage { get; }
    /// <summary>Gets the source publication associated with the diagnostic.</summary><value>The exact source, or null for assembly-wide evidence.</value>
    public ContextSourceReference? Source { get; }
}
