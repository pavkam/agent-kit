// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests.Egress;

using AgentKit.Providers.Egress;
using AgentKit.TestSupport;

/// <summary>Verifies the validation and value semantics of <see cref="ProviderEgressCredential"/>.</summary>
public sealed class ProviderEgressCredentialTests
{
    private static IProviderProfileRuntimeLease Lease() =>
        new StaticProviderProfileRuntimeSelector(new StaticProviderCredentialSource(new ApiKeyProviderCredential("secret")))
            .CreateLease(StaticProviderProfileRuntimeSelector.Binding);

    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesTheRuntimeAndScheme()
    {
        var lease = Lease();

        var credential = new ProviderEgressCredential(lease, ProviderAuthorizationScheme.OAuthTokenOnly);

        credential.Runtime.ShouldBeSameAs(lease);
        credential.Scheme.ShouldBe(ProviderAuthorizationScheme.OAuthTokenOnly);
    }

    [Fact]
    public void Constructor_WhenArgumentIsNull_ThrowsArgumentNullExceptionWithParamName()
    {
        Should.Throw<ArgumentNullException>(() => new ProviderEgressCredential(null!, ProviderAuthorizationScheme.BearerToken)).ParamName.ShouldBe("runtime");
        Should.Throw<ArgumentNullException>(() => new ProviderEgressCredential(Lease(), null!)).ParamName.ShouldBe("scheme");
    }

    [Fact]
    public void ToString_WhenRendered_NeverContainsTheSecret()
    {
        var credential = new ProviderEgressCredential(Lease(), ProviderAuthorizationScheme.BearerToken);

        credential.ToString().ShouldNotContain("secret");
    }
}
