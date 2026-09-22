// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>States the atomic target-state behavior a write authorization requires.</summary>
public enum FileWriteAtomicityMode
{
    /// <summary>
    /// The implementation must provide the requested atomic state test and commit
    /// or report unsupported before mutation.
    /// </summary>
    Required,

    /// <summary>
    /// The caller accepts best-effort behavior when the capability cannot provide
    /// full atomicity.
    /// </summary>
    BestEffort,
}
