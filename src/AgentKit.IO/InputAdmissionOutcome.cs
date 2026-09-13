// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Names the finite terminal outcomes of one coordinated input admission.</summary>
internal enum InputAdmissionOutcome
{
    /// <summary>The selected queue durably accepted the input or replayed its existing receipt.</summary>
    Accepted,

    /// <summary>The same input identity was reused with different content.</summary>
    Conflict,

    /// <summary>The selected queue refused admission because a capacity limit was reached.</summary>
    CapacityExceeded,

    /// <summary>Admission was refused before any durable append was attempted.</summary>
    Rejected,

    /// <summary>The caller cancelled the wait before a terminal admission outcome.</summary>
    Cancelled,

    /// <summary>Admission ended with an unexpected failure.</summary>
    Failed,
}
