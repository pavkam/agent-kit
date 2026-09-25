// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

/// <summary>Configuration for one keyed provider endpoint profile.</summary>
public sealed class ProviderEndpointProfileOptions
{
    /// <summary>Gets or sets the published profile version.</summary>
    public ProviderEndpointProfileVersion Version { get; set; } = new(1);

    /// <summary>Gets or sets the provider identity served by this endpoint.</summary>
    public ProviderId? ProviderId { get; set; }

    /// <summary>Gets or sets the service surface identity.</summary>
    public ProviderServiceSurfaceId? ServiceSurface { get; set; }

    /// <summary>Gets or sets the endpoint identity.</summary>
    public ProviderEndpointId? EndpointId { get; set; }

    /// <summary>Gets or sets the base address for this endpoint.</summary>
    public Uri? BaseAddress { get; set; }

    /// <summary>Gets or sets the optional API version.</summary>
    public ProviderApiVersion? ApiVersion { get; set; }
}
