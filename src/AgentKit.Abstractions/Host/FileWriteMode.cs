// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>How a <see cref="FileWriteRequest"/> treats an existing file at its target path.</summary>
public enum FileWriteMode
{
    /// <summary>Create the file if it does not exist, or replace its entire content if it does.</summary>
    CreateOrOverwrite,

    /// <summary>Create the file only if it does not already exist; fail otherwise.</summary>
    CreateNew,

    /// <summary>Create the file if it does not exist, or append to its existing content if it does.</summary>
    Append
}
