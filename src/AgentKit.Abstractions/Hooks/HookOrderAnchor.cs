// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A coarse ordering anchor a <see cref="HookOrder"/> may declare in addition to explicit before/after/depends-on edges.</summary>
/// <remarks>
/// At most one registration per hook point, profile, and catalog may declare <see cref="First"/>, and at most one
/// may declare <see cref="Last"/>; the order resolver rejects a catalog with more than one of either, the same way
/// it rejects a cycle or a self-reference.
/// </remarks>
public enum HookOrderAnchor
{
    /// <summary>No coarse anchor; ordering is decided entirely by explicit constraints and registration order.</summary>
    Normal,

    /// <summary>This registration must run before every <see cref="Normal"/>-anchored registration at the same point.</summary>
    First,

    /// <summary>This registration must run after every <see cref="Normal"/>-anchored registration at the same point.</summary>
    Last
}
