// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies the caller's declared process-side-effect intent for policy and retry decisions.</summary>
public enum ProcessSideEffectClass
{
    /// <summary>The command is intended only to observe sandbox-visible state.</summary>
    ReadOnly,
    /// <summary>The command may mutate the explicitly exposed workspace.</summary>
    WorkspaceMutation,
    /// <summary>The command may produce effects not fully represented by workspace bytes.</summary>
    ExternalOrUnknown,
}
