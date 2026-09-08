// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares optional session-store operations supported before a protected mutation starts.</summary>
/// <remarks>Core creation, load, conditional append, and forward reads remain part of <see cref="ISessionStore"/>. A caller validates these advertised optional capabilities during composition rather than discovering absence through a partial effect.</remarks>
[Flags]
public enum SessionStoreCapabilities
{
    /// <summary>Declares no optional capabilities.</summary>
    None = 0,
    /// <summary>Supports durable branch creation and branch-tip transitions.</summary>
    Branching = 1 << 0,
    /// <summary>Supports writing and verifying replayable snapshots.</summary>
    Snapshots = 1 << 1,
    /// <summary>Supports retention, archival, legal-hold, and deletion policy enforcement.</summary>
    Retention = 1 << 2,
    /// <summary>Supports atomic session transitions that coordinate multiple typed state records.</summary>
    Transactions = 1 << 3,
}
