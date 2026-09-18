// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json;

/// <summary>Defines how initialization treats a record log whose final append did not complete.</summary>
/// <remarks>
/// A torn trailing line can only occur when the process was lost mid-append, because a record is acknowledged only after its
/// terminating newline is flushed to disk. Discarding it therefore removes an operation that was never acknowledged to any
/// caller, while refusing to discard it keeps a damaged store readable for offline inspection.
/// </remarks>
public enum JsonStoreRecoveryMode
{
    /// <summary>Requires every record log to end on a complete record and fails closed otherwise.</summary>
    ValidateExact,

    /// <summary>Permits initialization to drop one incomplete trailing record by compacting the affected log.</summary>
    RecoverTornAppends,
}
