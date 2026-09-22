// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>States how a write treats an existing regular file at its target.</summary>
public enum FileWriteDisposition
{
    /// <summary>Create a missing target and report created; conflict when present.</summary>
    CreateOnly,

    /// <summary>Replace an existing target atomically; not found when missing.</summary>
    ReplaceExisting,

    /// <summary>Create or replace atomically with an explicit dual outcome.</summary>
    CreateOrReplace,

    /// <summary>Append once to an existing target; not found when missing.</summary>
    Append,
}
