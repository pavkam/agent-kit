// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ProviderCredentialProfileSnapshot behavior and contracts.</summary>
public sealed class ProviderCredentialProfileSnapshotTests
{
    [Fact]
    public void Snapshots_WhenReferenceExtensionsOrRequiredIdentityInvalid_ThrowExactParameter()
    {
        var credentialExtensions = Should.Throw<ArgumentNullException>(() => new ProviderCredentialProfileSnapshot(CredentialReference(), new ProviderId("provider"), new ProviderServiceSurfaceId("chat"), new ProviderCredentialSourceKey("source"), null, TimeSpan.Zero, new ContentHash("sha256:credential"), null!));
        credentialExtensions.ParamName.ShouldBe("extensions");
        var credentialSource = Should.Throw<ArgumentOutOfRangeException>(() => new ProviderCredentialProfileSnapshot(CredentialReference(), new ProviderId("provider"), new ProviderServiceSurfaceId("chat"), default, null, TimeSpan.Zero, new ContentHash("sha256:credential"), ExtensionData.Empty));
        credentialSource.ParamName.ShouldBe("sourceKey");
        var credentialReference = Should.Throw<ArgumentNullException>(() => new ProviderCredentialProfileSnapshot(null!, new ProviderId("provider"), new ProviderServiceSurfaceId("chat"), new ProviderCredentialSourceKey("source"), null, TimeSpan.Zero, new ContentHash("sha256:credential"), ExtensionData.Empty));
        credentialReference.ParamName.ShouldBe("reference");
        var credentialProvider = Should.Throw<ArgumentOutOfRangeException>(() => new ProviderCredentialProfileSnapshot(CredentialReference(), default, new ProviderServiceSurfaceId("chat"), new ProviderCredentialSourceKey("source"), null, TimeSpan.Zero, new ContentHash("sha256:credential"), ExtensionData.Empty));
        credentialProvider.ParamName.ShouldBe("providerId");
        var credentialSurface = Should.Throw<ArgumentOutOfRangeException>(() => new ProviderCredentialProfileSnapshot(CredentialReference(), new ProviderId("provider"), default, new ProviderCredentialSourceKey("source"), null, TimeSpan.Zero, new ContentHash("sha256:credential"), ExtensionData.Empty));
        credentialSurface.ParamName.ShouldBe("serviceSurface");
        var credentialHash = Should.Throw<ArgumentOutOfRangeException>(() => new ProviderCredentialProfileSnapshot(CredentialReference(), new ProviderId("provider"), new ProviderServiceSurfaceId("chat"), new ProviderCredentialSourceKey("source"), null, TimeSpan.Zero, default, ExtensionData.Empty));
        credentialHash.ParamName.ShouldBe("configurationFingerprint");
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
        var credential = new ProviderCredentialProfileSnapshot(CredentialReference(), new ProviderId("provider"), new ProviderServiceSurfaceId("chat"), new ProviderCredentialSourceKey("source"), null, TimeSpan.Zero, new ContentHash("sha256:credential"), ExtensionData.Empty);
        credential.AccountId.ShouldBeNull();
    }

    [Fact]
    public void Snapshots_WhenIndependentlyConstructed_AreEqualAndCopyPreservesEvidence()
    {
        var credential = Credential();
        var credentialCopy = credential with
        {
        };
        credentialCopy.ShouldBe(credential);
        credentialCopy.ShouldNotBeSameAs(credential);
        credential.ProviderId.ShouldBe(new ProviderId("provider"));
        credential.SourceKey.ShouldBe(new ProviderCredentialSourceKey("source"));
        credential.Reference.ShouldBe(CredentialReference());
        credential.ServiceSurface.ShouldBe(new ProviderServiceSurfaceId("chat"));
        credential.ConfigurationFingerprint.ShouldBe(new ContentHash("sha256:credential"));
        credential.Extensions.ShouldBe(ExtensionData.Empty);
    }

    private static ProviderCredentialProfileReference CredentialReference() => new(new ProviderCredentialProfileKey("credential-profile"), new ProviderCredentialProfileVersion(1));
    private static ProviderCredentialProfileSnapshot Credential(ProviderAccountId? accountId = null, TimeSpan? refreshSkew = null) => new(CredentialReference(), new ProviderId("provider"), new ProviderServiceSurfaceId("chat"), new ProviderCredentialSourceKey("source"), accountId ?? new ProviderAccountId("account"), refreshSkew ?? TimeSpan.FromMinutes(5), new ContentHash("sha256:credential"), ExtensionData.Empty);
}
