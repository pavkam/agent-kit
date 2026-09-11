// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ProviderOperationBinding behavior and contracts.</summary>
public sealed class ProviderOperationBindingTests
{
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

    private static ProviderEndpointProfileReference EndpointReference() => new(new ProviderEndpointProfileKey("endpoint-profile"), new ProviderEndpointProfileVersion(1));
    private static ProviderCredentialProfileReference CredentialReference() => new(new ProviderCredentialProfileKey("credential-profile"), new ProviderCredentialProfileVersion(1));
    [Fact]
    public void Snapshots_WhenIndependentlyConstructed_AreEqualAndCopyPreservesEvidence()
    {
        var binding = new ProviderOperationBinding(EndpointReference(), CredentialReference());
        var bindingCopy = binding with
        {
        };
        bindingCopy.ShouldBe(binding);
        bindingCopy.ShouldNotBeSameAs(binding);
        binding.Endpoint.ShouldBe(EndpointReference());
        binding.Credential.ShouldBe(CredentialReference());
    }
}
