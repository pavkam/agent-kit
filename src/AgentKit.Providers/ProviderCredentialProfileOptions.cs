// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

/// <summary>Configuration for one keyed provider credential profile.</summary>
public sealed class ProviderCredentialProfileOptions
{
    /// <summary>Gets or sets the published profile version.</summary>
    public ProviderCredentialProfileVersion Version { get; set; } = new(1);

    /// <summary>Gets or sets the provider identity authenticated by this profile.</summary>
    public ProviderId? ProviderId { get; set; }

    /// <summary>Gets or sets the service surface identity.</summary>
    public ProviderServiceSurfaceId? ServiceSurface { get; set; }

    /// <summary>Gets or sets the keyed credential source that serves this profile.</summary>
    public ProviderCredentialSourceKey? SourceKey { get; set; }

    /// <summary>Gets or sets the optional account identity.</summary>
    public ProviderAccountId? AccountId { get; set; }

    /// <summary>Gets or sets the refresh skew applied before credential expiry.</summary>
    public TimeSpan RefreshSkew { get; set; } = TimeSpan.FromMinutes(5);
}
