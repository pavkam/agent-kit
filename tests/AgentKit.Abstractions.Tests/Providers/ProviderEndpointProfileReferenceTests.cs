// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ProviderEndpointProfileReference behavior and contracts.</summary>
public sealed class ProviderEndpointProfileReferenceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ProfileReference_WhenKeyOrVersionDefault_ThrowsArgumentOutOfRangeException(bool endpoint)
    {
        var exception = endpoint ? Should.Throw<ArgumentOutOfRangeException>(() => new ProviderEndpointProfileReference(default, new ProviderEndpointProfileVersion(1))) : Should.Throw<ArgumentOutOfRangeException>(() => new ProviderCredentialProfileReference(default, new ProviderCredentialProfileVersion(1)));
        exception.ParamName.ShouldBe("key");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ProfileReference_WhenVersionDefault_ThrowsArgumentOutOfRangeException(bool endpoint)
    {
        var exception = endpoint ? Should.Throw<ArgumentOutOfRangeException>(() => new ProviderEndpointProfileReference(new ProviderEndpointProfileKey("endpoint"), default)) : Should.Throw<ArgumentOutOfRangeException>(() => new ProviderCredentialProfileReference(new ProviderCredentialProfileKey("credential"), default));
        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void Snapshots_WhenIndependentlyConstructed_AreEqualAndCopyPreservesEvidence()
    {
        var referenceCopy = EndpointReference() with
        {
        };
        referenceCopy.ShouldBe(EndpointReference());
        referenceCopy.GetHashCode().ShouldBe(EndpointReference().GetHashCode());
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var reference = EndpointReference();
        reference.Key.ShouldBe(new ProviderEndpointProfileKey("endpoint-profile"));
        reference.Version.ShouldBe(new ProviderEndpointProfileVersion(1));
    }

    private static ProviderEndpointProfileReference EndpointReference() => new(new ProviderEndpointProfileKey("endpoint-profile"), new ProviderEndpointProfileVersion(1));
}
