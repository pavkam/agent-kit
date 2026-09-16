// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ModelDescriptor behavior and contracts.</summary>
public sealed class ModelDescriptorTests
{
    [Fact]
    public void Constructor_WhenCapabilitiesIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ModelDescriptor(new ModelAlias("chat"), new ProviderId("provider"), new ApiFamilyId("api"), new ModelId("model"), null, null!, ProvidersTestData.Limits(), null, ExtensionData.Empty)).ParamName.ShouldBe("capabilities");

    [Fact]
    public void Constructor_WhenLimitsIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ModelDescriptor(new ModelAlias("chat"), new ProviderId("provider"), new ApiFamilyId("api"), new ModelId("model"), null, ProvidersTestData.Capabilities(), null!, null, ExtensionData.Empty)).ParamName.ShouldBe("limits");

    [Fact]
    public void Constructor_WhenExtensionsIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ModelDescriptor(new ModelAlias("chat"), new ProviderId("provider"), new ApiFamilyId("api"), new ModelId("model"), null, ProvidersTestData.Capabilities(), ProvidersTestData.Limits(), null, null!)).ParamName.ShouldBe("extensions");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var descriptor = ProvidersTestData.Descriptor();
        descriptor.Alias.ShouldBe(new ModelAlias("chat"));
        descriptor.ProviderId.ShouldBe(new ProviderId("test-provider"));
        descriptor.ApiFamily.ShouldBe(new ApiFamilyId("test-api"));
        descriptor.ModelId.ShouldBe(new ModelId("test-model"));
        descriptor.DeploymentId.ShouldBeNull();
        descriptor.Capabilities.ShouldBe(ProvidersTestData.Capabilities());
        descriptor.Limits.ShouldBe(ProvidersTestData.Limits());
        descriptor.Pricing.ShouldBeNull();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = ProvidersTestData.Descriptor();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
