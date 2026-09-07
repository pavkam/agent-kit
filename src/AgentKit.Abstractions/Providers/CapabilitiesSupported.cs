// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The chosen model supports everything the request requires, with no
/// semantic loss.
/// </summary>
/// <remarks>
/// This outcome carries no adjustments by construction. If any behavior had
/// to change, the correct result is
/// <see cref="CapabilitiesDowngraded"/> instead, so that a caller can rely on
/// this type meaning "executed exactly as asked".
/// </remarks>
public sealed record CapabilitiesSupported: CapabilityValidationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CapabilitiesSupported"/>
    /// record.
    /// </summary>
    public CapabilitiesSupported()
    {
    }
}
