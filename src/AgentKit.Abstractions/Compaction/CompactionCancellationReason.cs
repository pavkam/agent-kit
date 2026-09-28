// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Why a compaction stage observed cancellation.</summary>
public enum CompactionCancellationReason
{
    /// <summary>The caller cancelled the attempt.</summary>
    CallerCancelled,

    /// <summary>The attempt deadline was exceeded.</summary>
    DeadlineExceeded,

    /// <summary>The engine is stopping.</summary>
    EngineStopping,
}
