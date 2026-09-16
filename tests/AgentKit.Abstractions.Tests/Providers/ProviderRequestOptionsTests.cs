// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ProviderRequestOptions behavior and contracts.</summary>
public sealed class ProviderRequestOptionsTests
{
    [Fact]
    public void ProviderRequestOptions_Constructor_WhenExtensionsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ProviderRequestOptions(null!));
        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void ProviderRequestOptions_Equality_WhenSameValues_InstancesAreEqual() => new ProviderRequestOptions(ExtensionData.Empty).ShouldBe(new ProviderRequestOptions(ExtensionData.Empty));

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ProviderRequestOptions(ExtensionData.Empty);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
