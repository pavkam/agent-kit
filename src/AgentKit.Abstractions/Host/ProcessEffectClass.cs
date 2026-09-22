// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares the intended side-effect class for one process start.</summary>
public enum ProcessEffectClass
{
    /// <summary>Read-only observation with no durable mutation.</summary>
    ReadOnlyObservation = 0,

    /// <summary>Workspace mutation within authorized bounds.</summary>
    WorkspaceMutation = 1,

    /// <summary>External side effects beyond workspace mutation.</summary>
    ExternalEffect = 2,
}
