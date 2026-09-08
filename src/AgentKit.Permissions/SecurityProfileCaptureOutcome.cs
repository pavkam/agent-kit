// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Classifies the bounded terminal result of one exact security-profile capture.</summary>
internal enum SecurityProfileCaptureOutcome
{
    /// <summary>The exact publication was captured into fresh authorization evidence.</summary>
    Captured,

    /// <summary>No publication was available for the requested exact coordinates.</summary>
    Unavailable,

    /// <summary>The publication reader returned evidence with different coordinates.</summary>
    MismatchedPublication,

    /// <summary>The caller cancelled capture before it completed.</summary>
    Cancelled,

    /// <summary>The publication reader or capture operation faulted unexpectedly.</summary>
    Failed,
}
