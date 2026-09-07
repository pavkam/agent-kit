// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A bounded, provider-specific option bag scoped to one
/// <see cref="LlmModelRequest"/> attempt.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. Core request settings remain typed on
/// <see cref="LlmRequestSettings"/>; this type exists only for options a
/// specific concrete adapter defines and validates itself, such as a
/// vendor-specific beta feature flag.
/// </remarks>
public sealed record ProviderRequestOptions
{
    /// <summary>Gets the shared instance carrying no provider-specific options.</summary>
    public static ProviderRequestOptions Empty { get; } = new(ExtensionData.Empty);

    /// <summary>Initializes a new instance of the <see cref="ProviderRequestOptions"/> record.</summary>
    /// <param name="extensions">The provider-specific option values.</param>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    public ProviderRequestOptions(ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        Extensions = extensions;
    }

    /// <summary>Gets the provider-specific option values.</summary>
    public ExtensionData Extensions { get; init; }
}
