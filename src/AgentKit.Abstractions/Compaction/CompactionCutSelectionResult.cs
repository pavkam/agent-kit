// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The closed outcome of one cut-selection attempt: exactly one of
/// <see cref="CompactionCutSelected"/>, <see cref="NoSafeCompactionCut"/>,
/// or <see cref="CompactionCutSelectionFailed"/>.
/// </summary>
/// <remarks>
/// This hierarchy is closed to first-party outcomes recognized by
/// <see cref="ICompactionCutSelector"/> implementations and their callers;
/// external assemblies cannot derive additional cases. Each instance is
/// immutable and safe to share across threads without synchronization.
/// </remarks>
public abstract record CompactionCutSelectionResult
{
    private protected CompactionCutSelectionResult()
    {
    }
}
