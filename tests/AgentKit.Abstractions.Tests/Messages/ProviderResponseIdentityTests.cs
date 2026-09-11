// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

using AgentKit;

/// <summary>Verifies ProviderResponseIdentity behavior and contracts.</summary>
public sealed class ProviderResponseIdentityTests
{
    [Fact]
    public void ProviderResponseIdentity_WhenConstructed_ExposesResolvedModel()
    {
        var identity = new ProviderResponseIdentity(new ProviderId("openai"), null, new ApiFamilyId("chat-completions"), new ModelId("gpt-latest"), new ModelId("gpt-2024-01"), null, new ProviderRequestId("req_123"), new ProviderResponseId("resp_456"));
        identity.RequestedModelId.ShouldBe(new ModelId("gpt-latest"));
        identity.ResolvedModelId.ShouldBe(new ModelId("gpt-2024-01"));
        identity.UpstreamProviderId.ShouldBeNull();
    }

    [Fact]
    public void ProviderResponseIdentity_Constructor_WhenValid_RoundTripsProperties()
    {
        var identity = ResponseIdentity();
        identity.ProviderId.ShouldBe(new ProviderId("openai"));
    }

    [Fact]
    public void ProviderResponseIdentity_Equality_WhenSameValues_InstancesAreEqual() => ResponseIdentity().ShouldBe(ResponseIdentity());
    private static ProviderResponseIdentity ResponseIdentity() => new(new ProviderId("openai"), null, new ApiFamilyId("chat"), new ModelId("gpt"), new ModelId("gpt"), null, null, null);
}
