// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Binds one explicit source identity to its exact borrowed discovery provider.</summary>
/// <remarks>This immutable binding has reference identity and owns no provider resource. The host keeps the provider alive through every discovery capture and lease; selection never resolves a replacement from a container.</remarks>
public sealed class ToolProviderBinding
{
    /// <summary>Validates source identity before publishing a borrowed provider binding.</summary>
    /// <param name="sourceId">The nondefault exact source registration key.</param>
    /// <param name="provider">The nonnull concurrently callable provider declaring that stable source identity.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sourceId"/> is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
    /// <exception cref="ArgumentException">The provider declares a different or default source identity.</exception>
    /// <remarks>Reads provider identity once, without discovery or disposal. Providers must implement this metadata accessor without I/O.</remarks>
    public ToolProviderBinding(ToolSourceId sourceId, IToolProvider provider)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(sourceId, default);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNotEqual(provider.SourceId, sourceId, nameof(provider));
        SourceId = sourceId;
        Provider = provider;
    }

    /// <summary>Gets the source identity captured at binding construction.</summary>
    /// <value>The exact nondefault registration key; reading it does not call the provider.</value>
    public ToolSourceId SourceId { get; }

    /// <summary>Gets the exact provider selected by the registration.</summary>
    /// <value>A borrowed instance whose lifetime remains with the original host.</value>
    public IToolProvider Provider { get; }
}
