// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.XAI.Tests;



/// <summary>Verifies ProviderId behavior and contracts.</summary>
public sealed class ProviderIdTests
{
    [Fact]
    public void ProviderId_IsStableXAIIdentity() => XAIProviderDefaults.ProviderId.ShouldBe(new ProviderId("xai"));
}
