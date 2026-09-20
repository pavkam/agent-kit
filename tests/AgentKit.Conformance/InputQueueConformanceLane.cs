// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Names one of the two already-accepted lanes an input-queue fixture prepares in one session.</summary>
/// <remarks>
/// Both lanes are real provisioned branches with an accepted run. Promotion of one lane must not consume the other
/// lane's pending input.
/// </remarks>
public enum InputQueueConformanceLane
{
    /// <summary>The session's first accepted lane, bound to the session's active branch.</summary>
    Primary,

    /// <summary>A second accepted lane on its own empty fork of the active branch.</summary>
    Secondary,
}
