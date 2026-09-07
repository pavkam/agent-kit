// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies one patch entry after operation settlement.</summary>
public enum WorkspacePatchEntryStatus
{
    /// <summary>The entry's target effect committed.</summary>
    Committed,
    /// <summary>The entry remained unchanged.</summary>
    Unchanged,
    /// <summary>The host cannot prove whether the entry committed.</summary>
    Uncertain,
}
