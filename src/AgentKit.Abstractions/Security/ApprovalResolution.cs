// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies an authenticated terminal human approval decision.</summary>
public enum ApprovalResolution
{
    /// <summary>The bound operation was denied.</summary>
    Denied,
    /// <summary>The bound operation was approved.</summary>
    Approved,
}
