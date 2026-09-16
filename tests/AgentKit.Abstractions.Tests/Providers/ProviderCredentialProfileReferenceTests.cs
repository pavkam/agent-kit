// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ProviderCredentialProfileReference behavior and contracts.</summary>
public sealed class ProviderCredentialProfileReferenceTests
{
    [Fact]
    public void Snapshots_WhenIndependentlyConstructed_AreEqualAndCopyPreservesEvidence()
    {
        var credentialReferenceCopy = CredentialReference() with
        {
        };
        credentialReferenceCopy.ShouldBe(CredentialReference());
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var reference = CredentialReference();
        reference.Key.ShouldBe(new ProviderCredentialProfileKey("credential-profile"));
        reference.Version.ShouldBe(new ProviderCredentialProfileVersion(1));
    }

    private static ProviderCredentialProfileReference CredentialReference() => new(new ProviderCredentialProfileKey("credential-profile"), new ProviderCredentialProfileVersion(1));
}
