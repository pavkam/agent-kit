// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Retains one provider-neutral model-selection policy as typed effective configuration.</summary>
public sealed record ModelSelectionConfigurationValue: ConfigurationSemanticValue
{
    /// <summary>Creates a typed model selection without selecting or contacting a provider.</summary>
    /// <param name="policy">The nonnull immutable selection policy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="policy"/> is null.</exception>
    public ModelSelectionConfigurationValue(ModelSelectionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        Policy = policy;
    }

    /// <summary>Gets the complete provider-neutral model policy.</summary>
    /// <value>Immutable ordered selection evidence; it is not a resolved provider binding.</value>
    public ModelSelectionPolicy Policy { get; }
}
