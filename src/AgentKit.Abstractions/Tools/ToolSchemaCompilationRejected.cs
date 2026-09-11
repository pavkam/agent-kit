// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports why canonical schema compilation cannot safely produce a validation handle.</summary>
public sealed record ToolSchemaCompilationRejected: ToolSchemaCompilationResult
{
    /// <summary>Creates a content-free configuration rejection.</summary>
    /// <param name="reason">A defined compilation failure classification.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reason"/> is undefined.</exception>
    public ToolSchemaCompilationRejected(ToolSchemaRejectionReason reason)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(reason);
        Reason = reason;
    }
    /// <summary>Gets the bounded reason no validation handle was produced.</summary>
    /// <value>A defined failure classification, not an invalid model argument or a repair attempt.</value>
    public ToolSchemaRejectionReason Reason { get; }
}
