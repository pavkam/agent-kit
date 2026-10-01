// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies <see cref="ProviderCredentialUnavailable"/> and the closed <see cref="ProviderCredentialResolutionResult"/> hierarchy.</summary>
public sealed class ProviderCredentialUnavailableTests
{
    private static ProviderFailure Failure() =>
        new(ProviderFailureKind.Authorization, new ProviderId("p"), null, null, null, null, "denied", null, ExtensionData.Empty);

    [Fact]
    public void Constructor_WhenFailureIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ProviderCredentialUnavailable(null!)).ParamName.ShouldBe("failure");

    [Fact]
    public void Failure_WhenRead_ReturnsTheNormalizedFailure()
    {
        var failure = Failure();

        new ProviderCredentialUnavailable(failure).Failure.ShouldBeSameAs(failure);
    }

    [Fact]
    public void Results_WhenSwitchedOver_AreExactlyResolvedOrUnavailable()
    {
        ProviderCredentialResolutionResult unavailable = new ProviderCredentialUnavailable(Failure());

        (unavailable is ProviderCredentialResolved or ProviderCredentialUnavailable).ShouldBeTrue();
        typeof(ProviderCredentialResolutionResult).GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).ShouldBeEmpty();
    }
}
