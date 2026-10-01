// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a tool-call record could not be made durable.</summary>
/// <remarks>A rejection before invocation prevents the effect; a rejection after it never repeats the effect.</remarks>
public sealed record ToolCallRecordRejected: ToolCallRecordResult
{
    /// <summary>Initializes a typed recording rejection.</summary>
    /// <param name="kind">The defined rejection class.</param>
    /// <param name="safeReason">A nonblank bounded description that contains no arguments, results, paths, or exception text.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeReason"/> is null, empty, or whitespace.</exception>
    public ToolCallRecordRejected(ToolCallRecordRejectionKind kind, string safeReason)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        Kind = kind;
        SafeReason = safeReason;
    }

    /// <summary>Gets the rejection class.</summary>
    /// <value>A defined <see cref="ToolCallRecordRejectionKind"/>.</value>
    public ToolCallRecordRejectionKind Kind { get; }

    /// <summary>Gets the bounded safe reason.</summary>
    /// <value>Nonblank text safe for logs and model-visible errors.</value>
    public string SafeReason { get; }
}
