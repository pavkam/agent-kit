// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures one immutable, secret-free endpoint-profile publication.</summary>
/// <remarks>Construction retains configured evidence only. Branded publication and runtime validate destination safety, profile pairing, fingerprint authenticity, and transport support.</remarks>
public sealed record ProviderEndpointProfileSnapshot
{
    /// <summary>Initializes one exact endpoint-profile snapshot.</summary>
    /// <param name="reference">The non-null exact endpoint-profile reference.</param>
    /// <param name="providerId">The nondefault provider identity.</param>
    /// <param name="serviceSurface">The nondefault provider service surface.</param>
    /// <param name="endpointId">The nondefault configured endpoint identity.</param>
    /// <param name="baseAddress">The non-null configured endpoint URI, retained unchanged.</param>
    /// <param name="apiVersion">The optional nondefault API version.</param>
    /// <param name="configurationFingerprint">The nondefault captured configuration fingerprint.</param>
    /// <param name="extensions">The non-null immutable compatible evidence.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/>, <paramref name="baseAddress"/>, or <paramref name="extensions"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A required identity/fingerprint or present API version is default.</exception>
    public ProviderEndpointProfileSnapshot(
        ProviderEndpointProfileReference reference,
        ProviderId providerId,
        ProviderServiceSurfaceId serviceSurface,
        ProviderEndpointId endpointId,
        Uri baseAddress,
        ProviderApiVersion? apiVersion,
        ContentHash configurationFingerprint,
        ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentOutOfRangeException.ThrowIfEqual(providerId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(serviceSurface, default);
        ArgumentOutOfRangeException.ThrowIfEqual(endpointId, default);
        ArgumentNullException.ThrowIfNull(baseAddress);
        if (apiVersion is { } value)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, nameof(apiVersion));
        }

        ArgumentOutOfRangeException.ThrowIfEqual(configurationFingerprint, default);
        ArgumentNullException.ThrowIfNull(extensions);
        Reference = reference;
        ProviderId = providerId;
        ServiceSurface = serviceSurface;
        EndpointId = endpointId;
        BaseAddress = baseAddress;
        ApiVersion = apiVersion;
        ConfigurationFingerprint = configurationFingerprint;
        Extensions = extensions;
    }

    /// <summary>Gets the exact profile reference.</summary><value>A non-null exact reference.</value>
    public ProviderEndpointProfileReference Reference { get; }
    /// <summary>Gets the configured provider.</summary><value>A nondefault provider identity.</value>
    public ProviderId ProviderId { get; }
    /// <summary>Gets the configured provider service surface.</summary><value>A nondefault service-surface identity.</value>
    public ProviderServiceSurfaceId ServiceSurface { get; }
    /// <summary>Gets the configured endpoint identity.</summary><value>A nondefault endpoint identity.</value>
    public ProviderEndpointId EndpointId { get; }
    /// <summary>Gets the configured base URI exactly as published.</summary><value>A non-null URI whose runtime acceptance is validated by the branded owner.</value>
    public Uri BaseAddress { get; }
    /// <summary>Gets the optional configured API version.</summary><value>A nondefault version when present; otherwise null.</value>
    public ProviderApiVersion? ApiVersion { get; }
    /// <summary>Gets the captured configuration fingerprint.</summary><value>A nondefault hash retained without verification.</value>
    public ContentHash ConfigurationFingerprint { get; }
    /// <summary>Gets compatible immutable extension evidence.</summary><value>A non-null immutable bag.</value>
    public ExtensionData Extensions { get; }
}
