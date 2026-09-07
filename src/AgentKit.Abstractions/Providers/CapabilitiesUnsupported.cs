// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The chosen model cannot satisfy the request, and policy does not permit
/// altering it.
/// </summary>
/// <remarks>
/// This outcome is produced before credentials, hooks, or network access, so
/// an incompatible request never reaches a provider. It is the fail-closed
/// alternative to guessing that a downgrade would have been acceptable.
/// </remarks>
public sealed record CapabilitiesUnsupported: CapabilityValidationResult
{
    private readonly ImmutableArray<UnsupportedCapability> _capabilities;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="CapabilitiesUnsupported"/> record.
    /// </summary>
    /// <param name="capabilities">
    /// Every requirement the model cannot meet. At least one is required,
    /// because an unsupported result with no reason is not actionable.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="capabilities"/> is uninitialized, empty, or contains
    /// <see langword="null"/>.
    /// </exception>
    public CapabilitiesUnsupported(ImmutableArray<UnsupportedCapability> capabilities)
    {
        ArgumentException.ThrowIfDefaultOrEmpty(capabilities);
        ArgumentException.ThrowIfContainsNull(capabilities);
        _capabilities = capabilities;
    }

    /// <summary>Gets every requirement the model cannot meet.</summary>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set an uninitialized, empty, or
    /// null-containing array.
    /// </exception>
    public ImmutableArray<UnsupportedCapability> Capabilities
    {
        get => _capabilities;
        init
        {
            ArgumentException.ThrowIfDefaultOrEmpty(value, nameof(Capabilities));
            ArgumentException.ThrowIfContainsNull(value, nameof(Capabilities));
            _capabilities = value;
        }
    }
}
