// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ProviderContentDelta behavior and contracts.</summary>
public sealed class ProviderContentDeltaTests
{
    [Fact]
    public void ProviderContentDelta_Constructor_WhenExtensionsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ProviderContentDelta(new ProviderId("openai"), null!));
        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void ProviderContentDelta_Constructor_WhenValid_RoundTripsProperties()
    {
        var providerId = new ProviderId("openai");
        var delta = new ProviderContentDelta(providerId, ExtensionData.Empty);
        delta.ProviderId.ShouldBe(providerId);
        delta.Extensions.ShouldBe(ExtensionData.Empty);
    }

    [Fact]
    public void ProviderContentDelta_Equality_WhenSameValues_InstancesAreEqual()
    {
        var providerId = new ProviderId("openai");
        new ProviderContentDelta(providerId, ExtensionData.Empty).ShouldBe(new ProviderContentDelta(providerId, ExtensionData.Empty));
    }
}
