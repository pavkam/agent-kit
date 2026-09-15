// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies the diagnostic significance of context contribution evidence.</summary>
public enum ContextDiagnosticSeverity
{
    /// <summary>Informational evidence that does not indicate degradation.</summary>
    Information,
    /// <summary>Evidence of a bounded degradation or recoverable concern.</summary>
    Warning,
    /// <summary>Evidence that the affected contribution could not be produced correctly.</summary>
    Error,
}
