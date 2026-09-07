// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable base for the outcome of comparing a request's requirements
/// with a chosen model's declared capabilities.
/// </summary>
/// <remarks>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="CapabilitiesSupported"/>,
/// <see cref="CapabilitiesDowngraded"/>, and
/// <see cref="CapabilitiesUnsupported"/>. Its constructor is
/// <see langword="private protected"/>, so no assembly outside
/// AgentKit.Abstractions can add a fourth kind.
/// </remarks>
public abstract record CapabilityValidationResult
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="CapabilityValidationResult"/> record. This constructor is
    /// <see langword="private protected"/> so only the closed set of kinds
    /// declared in this assembly can extend the hierarchy.
    /// </summary>
    private protected CapabilityValidationResult()
    {
    }
}
