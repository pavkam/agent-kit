// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies <see cref="ProviderCredentialResolved"/> validation and that it never renders its lease.</summary>
public sealed class ProviderCredentialResolvedTests
{
    [Fact]
    public void Constructor_WhenLeaseIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ProviderCredentialResolved(null!)).ParamName.ShouldBe("credential");

    [Fact]
    public void ToString_WhenLeaseRendersSecrets_PrintsOnlyTheTypeName()
    {
        var resolved = new ProviderCredentialResolved(new SecretLease());

        resolved.ToString().ShouldBe(nameof(ProviderCredentialResolved));
        resolved.ToString().ShouldNotContain("super-secret");
    }

    [Fact]
    public void Credential_WhenRead_ReturnsTheOwnedLease()
    {
        var lease = new SecretLease();

        new ProviderCredentialResolved(lease).Credential.ShouldBeSameAs(lease);
    }

    private sealed class SecretLease: IProviderCredentialLease
    {
        public override string ToString() => "super-secret";

        public ValueTask<ProviderFailure?> ApplyAsync(IProviderAuthenticationTarget target, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<ProviderFailure?>(null);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
