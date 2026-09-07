// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>States what the host can prove about possible process effects at settlement.</summary>
public enum ProcessSideEffectCertainty
{
    /// <summary>No operating-system process was created.</summary>
    NotStarted,
    /// <summary>A process started and may have produced effects before settlement.</summary>
    MayHaveOccurred,
    /// <summary>The process reached a normally observed exit after all captured output drained.</summary>
    Completed,
}
