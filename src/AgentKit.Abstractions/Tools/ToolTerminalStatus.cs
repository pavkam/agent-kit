// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records the exact terminal stage of a tool call.</summary>
/// <remarks>Numeric values are persisted as evidence. Readers retain unrecognized values and project them conservatively; callers must not use enum validation to rewrite them.</remarks>
public enum ToolTerminalStatus
{
    /// <summary>The invocation and normalization completed.</summary>
    Succeeded = 1,
    /// <summary>The requested alias was unknown.</summary>
    UnknownTool = 2,
    /// <summary>The arguments were invalid.</summary>
    InvalidArguments = 3,
    /// <summary>The request was unsupported.</summary>
    Unsupported = 4,
    /// <summary>Authorization denied invocation.</summary>
    Denied = 5,
    /// <summary>Approval denied invocation.</summary>
    ApprovalDenied = 6,
    /// <summary>Approval expired before invocation.</summary>
    ApprovalExpired = 7,
    /// <summary>The invocation failed.</summary>
    InvocationFailed = 8,
    /// <summary>The invocation timed out.</summary>
    TimedOut = 9,
    /// <summary>The invocation was cancelled.</summary>
    Cancelled = 10,
    /// <summary>The operation was interrupted.</summary>
    Interrupted = 11,
    /// <summary>Result normalization failed.</summary>
    ResultNormalizationFailed = 12,
    /// <summary>Result serialization failed.</summary>
    ResultSerializationFailed = 13,
    /// <summary>Protocol mapping failed.</summary>
    ProtocolFailed = 14,
    /// <summary>Argument schema validation could not determine validity because a bounded resource limit was exceeded.</summary>
    ResourceLimitExceeded = 15,
}
