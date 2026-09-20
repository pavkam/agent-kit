// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Internal;

using System.Collections.Immutable;

using AgentKit;

/// <summary>Reports that run-plan compilation failed before any session mutation.</summary>
/// <remarks>Diagnostics are safe to log. They are not a run identity and they do not disclose caller content.</remarks>
internal sealed record InvalidAgentRunPlan: AgentRunPlanCompilationResult
{
    /// <summary>Captures the compilation diagnostics.</summary>
    /// <param name="diagnostics">The non-default, non-empty diagnostic list.</param>
    /// <exception cref="ArgumentException"><paramref name="diagnostics"/> is default, empty, or contains null.</exception>
    internal InvalidAgentRunPlan(ImmutableArray<CompositionDiagnostic> diagnostics)
    {
        ArgumentException.ThrowIfDefaultOrEmpty(diagnostics);
        ArgumentException.ThrowIfContainsNull(diagnostics);
        Diagnostics = diagnostics;
    }

    /// <summary>Gets the reasons compilation failed.</summary>
    /// <value>A non-empty immutable list of safe diagnostics.</value>
    internal ImmutableArray<CompositionDiagnostic> Diagnostics { get; }
}
