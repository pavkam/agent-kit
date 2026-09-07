// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Normalizes portable diagnostic severity without discarding provider-specific codes.</summary>
public enum LanguageDiagnosticSeverity
{
    /// <summary>An error that normally prevents correct compilation or execution.</summary>
    Error,
    /// <summary>A warning that deserves attention but may not prevent execution.</summary>
    Warning,
    /// <summary>Informational feedback.</summary>
    Information,
    /// <summary>A low-priority hint.</summary>
    Hint,
}
