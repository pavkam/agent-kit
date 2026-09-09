// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

public sealed class ProviderOperationProfileBindingTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ProfileReference_WhenKeyOrVersionDefault_ThrowsArgumentOutOfRangeException(bool endpoint)
    {
        var exception = endpoint
            ? Should.Throw<ArgumentOutOfRangeException>(() => new ProviderEndpointProfileReference(default, new ProviderEndpointProfileVersion(1)))
            : Should.Throw<ArgumentOutOfRangeException>(() => new ProviderCredentialProfileReference(default, new ProviderCredentialProfileVersion(1)));
        exception.ParamName.ShouldBe("key");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ProfileReference_WhenVersionDefault_ThrowsArgumentOutOfRangeException(bool endpoint)
    {
        var exception = endpoint
            ? Should.Throw<ArgumentOutOfRangeException>(
                () => new ProviderEndpointProfileReference(new ProviderEndpointProfileKey("endpoint"), default))
            : Should.Throw<ArgumentOutOfRangeException>(
                () => new ProviderCredentialProfileReference(new ProviderCredentialProfileKey("credential"), default));

        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void OperationBinding_WhenReferenceNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ProviderOperationBinding(null!, CredentialReference()));
        exception.ParamName.ShouldBe("endpoint");
    }

    [Fact]
    public void OperationBinding_WhenCredentialIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ProviderOperationBinding(EndpointReference(), null!));

        exception.ParamName.ShouldBe("credential");
    }

    [Fact]
    public void EndpointSnapshot_WhenRequiredValuesInvalid_ThrowsExactParameter()
    {
        var nullUri = Should.Throw<ArgumentNullException>(() => new ProviderEndpointProfileSnapshot(
            EndpointReference(),
            new ProviderId("provider"),
            new ProviderServiceSurfaceId("chat"),
            new ProviderEndpointId("endpoint"),
            null!,
            new ProviderApiVersion("2026-01"),
            new ContentHash("sha256:endpoint"),
            ExtensionData.Empty));
        nullUri.ParamName.ShouldBe("baseAddress");
        var defaultVersion = Should.Throw<ArgumentOutOfRangeException>(() => Endpoint(apiVersion: default(ProviderApiVersion)));
        defaultVersion.ParamName.ShouldBe("apiVersion");
        var defaultHash = Should.Throw<ArgumentOutOfRangeException>(() => new ProviderEndpointProfileSnapshot(
            EndpointReference(),
            new ProviderId("provider"),
            new ProviderServiceSurfaceId("chat"),
            new ProviderEndpointId("endpoint"),
            new Uri("https://provider.example/"),
            new ProviderApiVersion("2026-01"),
            default,
            ExtensionData.Empty));
        defaultHash.ParamName.ShouldBe("configurationFingerprint");

        var serviceSurface = Should.Throw<ArgumentOutOfRangeException>(() => new ProviderEndpointProfileSnapshot(
            EndpointReference(), new ProviderId("provider"), default, new ProviderEndpointId("endpoint"),
            new Uri("https://provider.example/"), null, new ContentHash("sha256:endpoint"), ExtensionData.Empty));
        serviceSurface.ParamName.ShouldBe("serviceSurface");
        var endpoint = Should.Throw<ArgumentOutOfRangeException>(() => new ProviderEndpointProfileSnapshot(
            EndpointReference(), new ProviderId("provider"), new ProviderServiceSurfaceId("chat"), default,
            new Uri("https://provider.example/"), null, new ContentHash("sha256:endpoint"), ExtensionData.Empty));
        endpoint.ParamName.ShouldBe("endpointId");
        var extensions = Should.Throw<ArgumentNullException>(() => new ProviderEndpointProfileSnapshot(
            EndpointReference(), new ProviderId("provider"), new ProviderServiceSurfaceId("chat"), new ProviderEndpointId("endpoint"),
            new Uri("https://provider.example/"), null, new ContentHash("sha256:endpoint"), null!));
        extensions.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void Snapshots_WhenReferenceExtensionsOrRequiredIdentityInvalid_ThrowExactParameter()
    {
        var endpointReference = Should.Throw<ArgumentNullException>(() => new ProviderEndpointProfileSnapshot(
            null!, new ProviderId("provider"), new ProviderServiceSurfaceId("chat"), new ProviderEndpointId("endpoint"), new Uri("https://provider.example/"), null, new ContentHash("sha256:endpoint"), ExtensionData.Empty));
        endpointReference.ParamName.ShouldBe("reference");
        var endpointProvider = Should.Throw<ArgumentOutOfRangeException>(() => new ProviderEndpointProfileSnapshot(
            EndpointReference(), default, new ProviderServiceSurfaceId("chat"), new ProviderEndpointId("endpoint"), new Uri("https://provider.example/"), null, new ContentHash("sha256:endpoint"), ExtensionData.Empty));
        endpointProvider.ParamName.ShouldBe("providerId");
        var credentialExtensions = Should.Throw<ArgumentNullException>(() => new ProviderCredentialProfileSnapshot(
            CredentialReference(), new ProviderId("provider"), new ProviderServiceSurfaceId("chat"), new ProviderCredentialSourceKey("source"), null, TimeSpan.Zero, new ContentHash("sha256:credential"), null!));
        credentialExtensions.ParamName.ShouldBe("extensions");
        var credentialSource = Should.Throw<ArgumentOutOfRangeException>(() => new ProviderCredentialProfileSnapshot(
            CredentialReference(), new ProviderId("provider"), new ProviderServiceSurfaceId("chat"), default, null, TimeSpan.Zero, new ContentHash("sha256:credential"), ExtensionData.Empty));
        credentialSource.ParamName.ShouldBe("sourceKey");
        var credentialReference = Should.Throw<ArgumentNullException>(() => new ProviderCredentialProfileSnapshot(
            null!, new ProviderId("provider"), new ProviderServiceSurfaceId("chat"), new ProviderCredentialSourceKey("source"),
            null, TimeSpan.Zero, new ContentHash("sha256:credential"), ExtensionData.Empty));
        credentialReference.ParamName.ShouldBe("reference");
        var credentialProvider = Should.Throw<ArgumentOutOfRangeException>(() => new ProviderCredentialProfileSnapshot(
            CredentialReference(), default, new ProviderServiceSurfaceId("chat"), new ProviderCredentialSourceKey("source"),
            null, TimeSpan.Zero, new ContentHash("sha256:credential"), ExtensionData.Empty));
        credentialProvider.ParamName.ShouldBe("providerId");
        var credentialSurface = Should.Throw<ArgumentOutOfRangeException>(() => new ProviderCredentialProfileSnapshot(
            CredentialReference(), new ProviderId("provider"), default, new ProviderCredentialSourceKey("source"),
            null, TimeSpan.Zero, new ContentHash("sha256:credential"), ExtensionData.Empty));
        credentialSurface.ParamName.ShouldBe("serviceSurface");
        var credentialHash = Should.Throw<ArgumentOutOfRangeException>(() => new ProviderCredentialProfileSnapshot(
            CredentialReference(), new ProviderId("provider"), new ProviderServiceSurfaceId("chat"), new ProviderCredentialSourceKey("source"),
            null, TimeSpan.Zero, default, ExtensionData.Empty));
        credentialHash.ParamName.ShouldBe("configurationFingerprint");
    }

    [Fact]
    public void EndpointSnapshot_WhenNonHttpUriProvided_PreservesConfiguredValue()
    {
        var address = new Uri("unix+http://provider/socket", UriKind.Absolute);
        var snapshot = Endpoint(baseAddress: address);
        snapshot.BaseAddress.ShouldBeSameAs(address);
    }

    [Fact]
    public void CredentialSnapshot_WhenOptionalAccountDefaultOrSkewNegative_ThrowsExactParameter()
    {
        var account = Should.Throw<ArgumentOutOfRangeException>(() => Credential(accountId: default(ProviderAccountId)));
        account.ParamName.ShouldBe("accountId");
        var skew = Should.Throw<ArgumentOutOfRangeException>(() => Credential(refreshSkew: TimeSpan.FromTicks(-1)));
        skew.ParamName.ShouldBe("refreshSkew");
    }

    [Fact]
    public void CredentialSnapshot_WhenZeroOrMaximumSkew_PreservesExactValue()
    {
        Credential(refreshSkew: TimeSpan.Zero).RefreshSkew.ShouldBe(TimeSpan.Zero);
        Credential(refreshSkew: TimeSpan.MaxValue).RefreshSkew.ShouldBe(TimeSpan.MaxValue);
    }

    [Fact]
    public void Snapshots_WhenOptionalValuesAbsent_RetainAbsence()
    {
        var endpoint = new ProviderEndpointProfileSnapshot(
            EndpointReference(), new ProviderId("provider"), new ProviderServiceSurfaceId("chat"), new ProviderEndpointId("endpoint"),
            new Uri("https://provider.example/"), null, new ContentHash("sha256:endpoint"), ExtensionData.Empty);
        var credential = new ProviderCredentialProfileSnapshot(
            CredentialReference(), new ProviderId("provider"), new ProviderServiceSurfaceId("chat"), new ProviderCredentialSourceKey("source"),
            null, TimeSpan.Zero, new ContentHash("sha256:credential"), ExtensionData.Empty);

        endpoint.ApiVersion.ShouldBeNull();
        credential.AccountId.ShouldBeNull();
    }

    [Fact]
    public void Snapshots_WhenIndependentlyConstructed_AreEqualAndCopyPreservesEvidence()
    {
        var endpoint = Endpoint();
        var same = Endpoint();
        var copy = endpoint with { };
        endpoint.ShouldBe(same);
        endpoint.GetHashCode().ShouldBe(same.GetHashCode());
        copy.ShouldBe(endpoint);
        copy.ShouldNotBeSameAs(endpoint);
        var credential = Credential();
        var binding = new ProviderOperationBinding(EndpointReference(), CredentialReference());
        var referenceCopy = EndpointReference() with { };
        var credentialReferenceCopy = CredentialReference() with { };
        var bindingCopy = binding with { };
        var credentialCopy = credential with { };

        referenceCopy.ShouldBe(EndpointReference());
        referenceCopy.GetHashCode().ShouldBe(EndpointReference().GetHashCode());
        credentialReferenceCopy.ShouldBe(CredentialReference());
        bindingCopy.ShouldBe(binding);
        bindingCopy.ShouldNotBeSameAs(binding);
        credentialCopy.ShouldBe(credential);
        credentialCopy.ShouldNotBeSameAs(credential);
        binding.Endpoint.ShouldBe(EndpointReference());
        binding.Credential.ShouldBe(CredentialReference());
        credential.ProviderId.ShouldBe(new ProviderId("provider"));
        credential.SourceKey.ShouldBe(new ProviderCredentialSourceKey("source"));
    }

    private static ProviderEndpointProfileReference EndpointReference() => new(new ProviderEndpointProfileKey("endpoint-profile"), new ProviderEndpointProfileVersion(1));
    private static ProviderCredentialProfileReference CredentialReference() => new(new ProviderCredentialProfileKey("credential-profile"), new ProviderCredentialProfileVersion(1));
    private static ProviderEndpointProfileSnapshot Endpoint(Uri? baseAddress = null, ProviderApiVersion? apiVersion = null, ContentHash? configurationFingerprint = null) => new(EndpointReference(), new ProviderId("provider"), new ProviderServiceSurfaceId("chat"), new ProviderEndpointId("endpoint"), baseAddress ?? new Uri("https://provider.example/"), apiVersion ?? new ProviderApiVersion("2026-01"), configurationFingerprint ?? new ContentHash("sha256:endpoint"), ExtensionData.Empty);
    private static ProviderCredentialProfileSnapshot Credential(ProviderAccountId? accountId = null, TimeSpan? refreshSkew = null) => new(CredentialReference(), new ProviderId("provider"), new ProviderServiceSurfaceId("chat"), new ProviderCredentialSourceKey("source"), accountId ?? new ProviderAccountId("account"), refreshSkew ?? TimeSpan.FromMinutes(5), new ContentHash("sha256:credential"), ExtensionData.Empty);
}
