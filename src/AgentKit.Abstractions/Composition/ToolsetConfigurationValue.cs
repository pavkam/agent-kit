// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Retains one complete toolset publication as typed effective configuration.</summary>
public sealed record ToolsetConfigurationValue: ConfigurationSemanticValue
{
    /// <summary>Creates a typed toolset selection without discovering tools or activating invokers.</summary>
    /// <param name="publication">The nonnull exact toolset publication.</param>
    /// <exception cref="ArgumentNullException"><paramref name="publication"/> is null.</exception>
    public ToolsetConfigurationValue(ToolsetPublication publication)
    {
        ArgumentNullException.ThrowIfNull(publication);
        Publication = publication;
    }

    /// <summary>Gets the exact selected toolset publication.</summary>
    /// <value>Immutable version, policy, source-membership, and alias evidence.</value>
    public ToolsetPublication Publication { get; }
}
