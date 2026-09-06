// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One incremental fragment of provider-specific content that does not fit
/// any other portable <see cref="ContentDelta"/> kind, preserved so it is
/// not silently discarded.
/// </summary>
public sealed record ProviderContentDelta: ContentDelta
{
    /// <summary>Initializes a new instance of the <see cref="ProviderContentDelta"/> record.</summary>
    /// <param name="providerId">The provider that produced this fragment.</param>
    /// <param name="extensions">The raw provider-specific fragment data.</param>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    public ProviderContentDelta(ProviderId providerId, ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);

        ProviderId = providerId;
        Extensions = extensions;
    }

    /// <summary>Gets the provider that produced this fragment.</summary>
    public ProviderId ProviderId { get; init; }

    /// <summary>Gets the raw provider-specific fragment data.</summary>
    public ExtensionData Extensions { get; init; }
}
