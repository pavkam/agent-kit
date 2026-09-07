// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The request can proceed, but only after declared changes were made because
/// the chosen model could not support everything asked for.
/// </summary>
/// <remarks>
/// This outcome is only reachable under
/// <see cref="CapabilityDowngradePolicy.AllowDeclaredAdjustments"/>. Under
/// <see cref="CapabilityDowngradePolicy.Reject"/> the same situation produces
/// <see cref="CapabilitiesUnsupported"/>, because the application asked not to
/// have its request altered.
/// </remarks>
public sealed record CapabilitiesDowngraded: CapabilityValidationResult
{
    private readonly ImmutableArray<CapabilityAdjustment> _adjustments;

    /// <summary>
    /// Initializes a new instance of the <see cref="CapabilitiesDowngraded"/>
    /// record.
    /// </summary>
    /// <param name="adjustments">
    /// Every change made to the request. At least one is required, since a
    /// downgrade with no adjustments would be indistinguishable from
    /// <see cref="CapabilitiesSupported"/>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="adjustments"/> is uninitialized, empty, or contains
    /// <see langword="null"/>.
    /// </exception>
    public CapabilitiesDowngraded(ImmutableArray<CapabilityAdjustment> adjustments)
    {
        ArgumentException.ThrowIfDefaultOrEmpty(adjustments);
        ArgumentException.ThrowIfContainsNull(adjustments);
        _adjustments = adjustments;
    }

    /// <summary>Gets every change made to the request.</summary>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set an uninitialized, empty, or
    /// null-containing array.
    /// </exception>
    public ImmutableArray<CapabilityAdjustment> Adjustments
    {
        get => _adjustments;
        init
        {
            ArgumentException.ThrowIfDefaultOrEmpty(value, nameof(Adjustments));
            ArgumentException.ThrowIfContainsNull(value, nameof(Adjustments));
            _adjustments = value;
        }
    }
}
