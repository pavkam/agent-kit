// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Contains one bounded provider diagnostic tied to an observed source location.</summary>
public sealed record LanguageDiagnostic
{
    /// <summary>Initializes one normalized diagnostic.</summary>
    /// <param name="severity">The portable severity.</param>
    /// <param name="message">The bounded non-empty untrusted provider message.</param>
    /// <param name="code">The bounded provider code when available.</param>
    /// <param name="source">The bounded provider source when available.</param>
    /// <param name="location">The document range and version observed.</param>
    /// <exception cref="ArgumentException"><paramref name="message"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="severity"/> is undefined.</exception>
    public LanguageDiagnostic(
        LanguageDiagnosticSeverity severity,
        string message,
        string? code,
        string? source,
        LanguageLocation location)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(severity);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Severity = severity;
        Message = message;
        Code = code;
        Source = source;
        Location = location;
    }

    /// <summary>Gets the portable severity.</summary>
    public LanguageDiagnosticSeverity Severity { get; }
    /// <summary>Gets the bounded untrusted diagnostic message.</summary>
    public string Message { get; }
    /// <summary>Gets the provider diagnostic code when available.</summary>
    public string? Code { get; }
    /// <summary>Gets the provider diagnostic source when available.</summary>
    public string? Source { get; }
    /// <summary>Gets the observed document location.</summary>
    public LanguageLocation Location { get; }
}
