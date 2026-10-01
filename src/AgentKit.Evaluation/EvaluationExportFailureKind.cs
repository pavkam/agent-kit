// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Classifies why a report export did not publish.</summary>
public enum EvaluationExportFailureKind
{
    /// <summary>The destination is unavailable or refused the report.</summary>
    Unavailable = 0,

    /// <summary>Cancellation stopped the export before it completed.</summary>
    Cancelled = 1,

    /// <summary>The exporter threw an unexpected exception; the runner isolated it.</summary>
    Faulted = 2,
}
