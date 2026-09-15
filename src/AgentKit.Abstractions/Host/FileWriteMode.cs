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

    /// <summary>Append to the existing content of an existing file exactly once; fail without mutation if the target is missing.</summary>
    Append,

    /// <summary>Replace the entire content of an existing file; fail if the target is missing.</summary>
    ReplaceExisting
}
