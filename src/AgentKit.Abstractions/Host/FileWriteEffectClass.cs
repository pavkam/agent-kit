// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies the declared effect class for one authorized file write.</summary>
public enum FileWriteEffectClass
{
    /// <summary>The write affects only workspace byte content.</summary>
    WorkspaceBytes,

    /// <summary>The write may produce effects not fully represented by workspace bytes.</summary>
    ExternalOrUnknown,
}
