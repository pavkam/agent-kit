// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Declares what happens when a chosen model cannot support a behavior the
/// request asked for.
/// </summary>
/// <remarks>
/// Silent downgrade is never an option. Even
/// <see cref="AllowDeclaredAdjustments"/> requires each loss to be reported as
/// an explicit <see cref="CapabilityAdjustment"/>, so a caller can always tell
/// that its request was altered.
/// </remarks>
public enum CapabilityDowngradePolicy
{
    /// <summary>
    /// Any unsupported requirement fails validation before I/O. The request is
    /// executed exactly as asked or not at all.
    /// </summary>
    Reject,

    /// <summary>
    /// Unsupported behavior may be adjusted, provided every adjustment is
    /// declared in the validation result.
    /// </summary>
    AllowDeclaredAdjustments,
}
