// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies one concrete entry in an exact workspace patch plan.</summary>
public enum WorkspacePatchEntryKind
{
    /// <summary>Creates bytes only when the target does not exist.</summary>
    Create,
    /// <summary>Replaces an existing exact content version.</summary>
    Replace,
    /// <summary>Deletes an existing exact content version.</summary>
    Delete,
    /// <summary>Moves an existing exact content version to an absent destination.</summary>
    Move,
}
