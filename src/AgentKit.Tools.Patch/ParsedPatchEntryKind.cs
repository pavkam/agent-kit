// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Patch;

/// <summary>Classifies one syntactically parsed patch entry before host-state planning.</summary>
internal enum ParsedPatchEntryKind
{
    /// <summary>Adds one absent file.</summary>
    Add,
    /// <summary>Updates one existing text file.</summary>
    Update,
    /// <summary>Deletes one existing file.</summary>
    Delete,
    /// <summary>Moves one existing file without changing its bytes.</summary>
    Move,
}
