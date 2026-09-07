// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes the material effect authorized for a protected resource.</summary>
public enum SecurityEffect
{
    /// <summary>Observes existing state without intentionally mutating it.</summary>
    Observe,
    /// <summary>Creates state that must not already exist.</summary>
    Create,
    /// <summary>Replaces state that must already exist.</summary>
    Replace,
    /// <summary>Creates or replaces state atomically according to target state.</summary>
    CreateOrReplace,
    /// <summary>Appends content without replacing prior content.</summary>
    Append,
    /// <summary>Removes existing state.</summary>
    Delete,
    /// <summary>Moves existing state between two exact resource identities.</summary>
    Move,
    /// <summary>Executes code or a host process.</summary>
    Execute,
    /// <summary>Sends classified data to a destination.</summary>
    Egress,
    /// <summary>Changes application-managed state.</summary>
    Mutate,
    /// <summary>Creates scoped child work.</summary>
    Delegate,
}
