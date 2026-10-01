// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests.Credentials;

using AgentKit.Providers.Credentials;
using AgentKit.TestSupport;

/// <summary>Verifies the secret-free resource and fingerprint that a credential-read grant binds.</summary>
public sealed class ProviderCredentialReadBindingTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static ProviderCredentialResolutionRequest Request() =>
        ProviderCredentialProbe.CreateRequest(new StaticProviderCredentialSource(new ApiKeyProviderCredential("secret-value")), Now);

    private static ProviderCredentialProfileSnapshot Credential(
        ProviderCredentialResolutionRequest request,
        ProviderCredentialProfileVersion? version = null,
        ProviderAccountId? account = null,
        ProviderCredentialSourceKey? source = null,
        string? fingerprint = null) =>
        new(
            new ProviderCredentialProfileReference(request.Credential.Reference.Key, version ?? request.Credential.Reference.Version),
            request.Credential.ProviderId,
            request.Credential.ServiceSurface,
            source ?? request.Credential.SourceKey,
            account ?? request.Credential.AccountId,
            request.Credential.RefreshSkew,
            fingerprint is null ? request.Credential.ConfigurationFingerprint : new ContentHash(fingerprint),
            request.Credential.Extensions);

    [Fact]
    public void Resources_WhenCalled_ReturnsOneApplicationStateResourceNamingProfileRevisionAndSource()
    {
        var request = Request();

        var resource = ProviderCredentialReadBinding.Resources(request.Credential).ShouldHaveSingleItem();

        resource.Kind.ShouldBe(ProtectedResourceKind.ApplicationState);
        resource.Identifier.ShouldBe("provider-credential:probe-provider/probe-surface/test.credential@1/source:test.credentials");
    }

    [Fact]
    public void Fingerprint_WhenEvidenceIsIdentical_IsStable()
    {
        var request = Request();

        var first = ProviderCredentialReadBinding.Fingerprint(request.Endpoint, request.Credential, 1, request.Deadline);
        var second = ProviderCredentialReadBinding.Fingerprint(request.Endpoint, request.Credential, 1, request.Deadline);

        first.ShouldBe(second);
        first.Value.ShouldStartWith("sha256:");
    }

    [Fact]
    public void Fingerprint_WhenAnyBoundValueChanges_ProducesADifferentFingerprint()
    {
        var request = Request();
        var baseline = ProviderCredentialReadBinding.Fingerprint(request.Endpoint, request.Credential, 1, request.Deadline);
        var variants = new[]
        {
            ProviderCredentialReadBinding.Fingerprint(request.Endpoint, request.Credential, 2, request.Deadline),
            ProviderCredentialReadBinding.Fingerprint(request.Endpoint, request.Credential, 1, request.Deadline.AddTicks(1)),
            ProviderCredentialReadBinding.Fingerprint(request.Endpoint, Credential(request, version: new ProviderCredentialProfileVersion(9)), 1, request.Deadline),
            ProviderCredentialReadBinding.Fingerprint(request.Endpoint, Credential(request, account: new ProviderAccountId("other")), 1, request.Deadline),
            ProviderCredentialReadBinding.Fingerprint(request.Endpoint, Credential(request, source: new ProviderCredentialSourceKey("other.source")), 1, request.Deadline),
            ProviderCredentialReadBinding.Fingerprint(request.Endpoint, Credential(request, fingerprint: "credential:changed"), 1, request.Deadline),
        };

        variants.ShouldAllBe(variant => variant != baseline);
        variants.Distinct().Count().ShouldBe(variants.Length);
    }

    [Fact]
    public void Fingerprint_WhenCalled_NeverContainsIdentifiersInTheClear()
    {
        var request = Request();

        var fingerprint = ProviderCredentialReadBinding.Fingerprint(request.Endpoint, request.Credential, 1, request.Deadline);

        fingerprint.Value.ShouldNotContain("probe-account");
        fingerprint.Value.ShouldNotContain("test.credentials");
    }

    [Fact]
    public void Constants_WhenRead_NameTheStateReadObserveEffect()
    {
        ProviderCredentialReadBinding.OperationKind.ShouldBe(SecurityOperationKind.StateRead);
        ProviderCredentialReadBinding.Effect.ShouldBe(SecurityEffect.Observe);
    }

    [Fact]
    public void Arguments_WhenInvalid_ThrowTheExactException()
    {
        var request = Request();

        Should.Throw<ArgumentNullException>(() => ProviderCredentialReadBinding.Resources(null!)).ParamName.ShouldBe("credential");
        Should.Throw<ArgumentNullException>(() => ProviderCredentialReadBinding.Fingerprint(null!, request.Credential, 1, Now)).ParamName.ShouldBe("endpoint");
        Should.Throw<ArgumentNullException>(() => ProviderCredentialReadBinding.Fingerprint(request.Endpoint, null!, 1, Now)).ParamName.ShouldBe("credential");
        Should.Throw<ArgumentOutOfRangeException>(() => ProviderCredentialReadBinding.Fingerprint(request.Endpoint, request.Credential, 0, Now)).ParamName.ShouldBe("attempt");
    }
}
