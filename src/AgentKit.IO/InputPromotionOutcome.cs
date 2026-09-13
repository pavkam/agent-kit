// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Names the finite terminal outcomes of one coordinated input promotion.</summary>
internal enum InputPromotionOutcome
{
    /// <summary>The selected queue atomically committed the promotion.</summary>
    Promoted,

    /// <summary>The request's revalidation evidence was stale, so nothing was committed.</summary>
    Conflict,

    /// <summary>Promotion was refused before any mutation was attempted.</summary>
    Rejected,

    /// <summary>The caller cancelled the wait before a terminal promotion outcome.</summary>
    Cancelled,

    /// <summary>Promotion ended with an unexpected failure.</summary>
    Failed,
}
