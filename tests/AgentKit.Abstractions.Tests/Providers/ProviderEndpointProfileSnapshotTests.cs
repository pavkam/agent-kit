// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ProviderEndpointProfileSnapshot behavior and contracts.</summary>
public sealed class ProviderEndpointProfileSnapshotTests
{
    [Fact]
    public void EndpointSnapshot_WhenRequiredValuesInvalid_ThrowsExactParameter()
    {
        var nullUri = Should.Throw<ArgumentNullException>(() => new ProviderEndpointProfileSnapshot(EndpointReference(), new ProviderId("provider"), new ProviderServiceSurfaceId("chat"), new ProviderEndpointId("endpoint"), null!, new ProviderApiVersion("2026-01"), new ContentHash("sha256:endpoint"), ExtensionData.Empty));
        nullUri.ParamName.ShouldBe("baseAddress");
        var defaultVersion = Should.Throw<ArgumentOutOfRangeException>(() => Endpoint(apiVersion: default(ProviderApiVersion)));
        defaultVersion.ParamName.ShouldBe("apiVersion");
        var defaultHash = Should.Throw<ArgumentOutOfRangeException>(() => new ProviderEndpointProfileSnapshot(EndpointReference(), new ProviderId("provider"), new ProviderServiceSurfaceId("chat"), new ProviderEndpointId("endpoint"), new Uri("https://provider.example/"), new ProviderApiVersion("2026-01"), default, ExtensionData.Empty));
        defaultHash.ParamName.ShouldBe("configurationFingerprint");
        var serviceSurface = Should.Throw<ArgumentOutOfRangeException>(() => new ProviderEndpointProfileSnapshot(EndpointReference(), new ProviderId("provider"), default, new ProviderEndpointId("endpoint"), new Uri("https://provider.example/"), null, new ContentHash("sha256:endpoint"), ExtensionData.Empty));
        serviceSurface.ParamName.ShouldBe("serviceSurface");
        var endpoint = Should.Throw<ArgumentOutOfRangeException>(() => new ProviderEndpointProfileSnapshot(EndpointReference(), new ProviderId("provider"), new ProviderServiceSurfaceId("chat"), default, new Uri("https://provider.example/"), null, new ContentHash("sha256:endpoint"), ExtensionData.Empty));
        endpoint.ParamName.ShouldBe("endpointId");
        var extensions = Should.Throw<ArgumentNullException>(() => new ProviderEndpointProfileSnapshot(EndpointReference(), new ProviderId("provider"), new ProviderServiceSurfaceId("chat"), new ProviderEndpointId("endpoint"), new Uri("https://provider.example/"), null, new ContentHash("sha256:endpoint"), null!));
        extensions.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void Snapshots_WhenReferenceExtensionsOrRequiredIdentityInvalid_ThrowExactParameter()
    {
        var endpointReference = Should.Throw<ArgumentNullException>(() => new ProviderEndpointProfileSnapshot(null!, new ProviderId("provider"), new ProviderServiceSurfaceId("chat"), new ProviderEndpointId("endpoint"), new Uri("https://provider.example/"), null, new ContentHash("sha256:endpoint"), ExtensionData.Empty));
        endpointReference.ParamName.ShouldBe("reference");
        var endpointProvider = Should.Throw<ArgumentOutOfRangeException>(() => new ProviderEndpointProfileSnapshot(EndpointReference(), default, new ProviderServiceSurfaceId("chat"), new ProviderEndpointId("endpoint"), new Uri("https://provider.example/"), null, new ContentHash("sha256:endpoint"), ExtensionData.Empty));
        endpointProvider.ParamName.ShouldBe("providerId");
    }

    [Fact]
    public void EndpointSnapshot_WhenNonHttpUriProvided_PreservesConfiguredValue()
    {
        var address = new Uri("unix+http://provider/socket", UriKind.Absolute);
        var snapshot = Endpoint(baseAddress: address);
        snapshot.BaseAddress.ShouldBeSameAs(address);
    }

    [Fact]
    public void Snapshots_WhenOptionalValuesAbsent_RetainAbsence()
    {
        var endpoint = new ProviderEndpointProfileSnapshot(EndpointReference(), new ProviderId("provider"), new ProviderServiceSurfaceId("chat"), new ProviderEndpointId("endpoint"), new Uri("https://provider.example/"), null, new ContentHash("sha256:endpoint"), ExtensionData.Empty);
        endpoint.ApiVersion.ShouldBeNull();
    }

    [Fact]
    public void Snapshots_WhenIndependentlyConstructed_AreEqualAndCopyPreservesEvidence()
    {
        var endpoint = Endpoint();
        var same = Endpoint();
        var copy = endpoint with
        {
        };
        endpoint.ShouldBe(same);
        endpoint.GetHashCode().ShouldBe(same.GetHashCode());
        copy.ShouldBe(endpoint);
        copy.ShouldNotBeSameAs(endpoint);
    }

    private static ProviderEndpointProfileReference EndpointReference() => new(new ProviderEndpointProfileKey("endpoint-profile"), new ProviderEndpointProfileVersion(1));
    private static ProviderEndpointProfileSnapshot Endpoint(Uri? baseAddress = null, ProviderApiVersion? apiVersion = null, ContentHash? configurationFingerprint = null) => new(EndpointReference(), new ProviderId("provider"), new ProviderServiceSurfaceId("chat"), new ProviderEndpointId("endpoint"), baseAddress ?? new Uri("https://provider.example/"), apiVersion ?? new ProviderApiVersion("2026-01"), configurationFingerprint ?? new ContentHash("sha256:endpoint"), ExtensionData.Empty);
}
