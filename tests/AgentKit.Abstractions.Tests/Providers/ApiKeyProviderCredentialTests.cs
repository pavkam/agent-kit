// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ApiKeyProviderCredential behavior and contracts.</summary>
public sealed class ApiKeyProviderCredentialTests: Conformance.SingleMessageLeafConformanceTests<ApiKeyProviderCredential>
{
    [Fact]
    public void ApiKeyProviderCredential_Constructor_WhenApiKeyInvalid_ThrowsArgumentException() => _ = Should.Throw<ArgumentException>(() => new ApiKeyProviderCredential(" "));
    [Fact]
    public void ApiKeyProviderCredential_Equality_WhenSameValues_InstancesAreEqual() => new ApiKeyProviderCredential("secret").ShouldBe(new ApiKeyProviderCredential("secret"));

    /// <inheritdoc/>
    protected override ApiKeyProviderCredential Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(ApiKeyProviderCredential subject) => subject.ApiKey;
}
