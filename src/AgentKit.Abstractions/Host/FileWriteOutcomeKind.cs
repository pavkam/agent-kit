// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports which successful write outcome committed at the target.</summary>
public enum FileWriteOutcomeKind
{
    /// <summary>The target did not exist and was created.</summary>
    Created,

    /// <summary>An existing target was atomically replaced.</summary>
    Replaced,

    /// <summary>Payload bytes were appended once to an existing target.</summary>
    Appended,
}
