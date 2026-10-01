// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests.Credentials;

using AgentKit.Providers.Credentials;
using AgentKit.TestSupport;

/// <summary>Verifies that <see cref="ProviderCredentialLease"/> applies, hides, and releases an opaque credential.</summary>
public sealed class ProviderCredentialLeaseTests
{
    private const string Secret = "sk-lease-secret-value";

    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ApplyAsync_WhenApiKeyAndBearerScheme_SetsTheAuthorizationHeader()
    {
        var lease = new ProviderCredentialLease(new ApiKeyProviderCredential(Secret));
        var target = new RecordingAuthenticationTarget(ProviderAuthorizationScheme.BearerToken, Now);

        var failure = await lease.ApplyAsync(target, TestContext.Current.CancellationToken);

        failure.ShouldBeNull();
        target.Headers["Authorization"].ShouldBe($"Bearer {Secret}");
    }

    [Fact]
    public async Task ApplyAsync_WhenApiKeyAndDedicatedHeaderScheme_SetsThatHeaderWithTheBareKey()
    {
        var lease = new ProviderCredentialLease(new ApiKeyProviderCredential(Secret));
        var target = new RecordingAuthenticationTarget(ProviderAuthorizationScheme.ForApiKeyHeader("x-api-key"), Now);

        var failure = await lease.ApplyAsync(target, TestContext.Current.CancellationToken);

        failure.ShouldBeNull();
        target.Headers["x-api-key"].ShouldBe(Secret);
    }

    [Fact]
    public async Task ApplyAsync_WhenTokenExpiredAtTheTargetInstant_ReturnsAnAuthenticationFailureWithoutTheToken()
    {
        var lease = new ProviderCredentialLease(new OAuthTokenProviderCredential(Secret, Now));
        var target = new RecordingAuthenticationTarget(utcNow: Now);

        var failure = await lease.ApplyAsync(target, TestContext.Current.CancellationToken);

        var denied = failure.ShouldNotBeNull();
        denied.Kind.ShouldBe(ProviderFailureKind.Authentication);
        denied.SafeMessage.ShouldNotContain(Secret);
        target.Headers.ShouldBeEmpty();
    }

    [Fact]
    public async Task ApplyAsync_WhenTokenIsStillValidAtTheTargetInstant_SetsABearerHeader()
    {
        var lease = new ProviderCredentialLease(new OAuthTokenProviderCredential(Secret, Now.AddSeconds(1)));
        var target = new RecordingAuthenticationTarget(utcNow: Now);

        (await lease.ApplyAsync(target, TestContext.Current.CancellationToken)).ShouldBeNull();

        target.Headers["Authorization"].ShouldBe($"Bearer {Secret}");
    }

    [Fact]
    public async Task ApplyAsync_WhenApiKeyAndTokenOnlyScheme_ReturnsAnAuthenticationFailure()
    {
        var lease = new ProviderCredentialLease(new ApiKeyProviderCredential(Secret));
        var target = new RecordingAuthenticationTarget(ProviderAuthorizationScheme.OAuthTokenOnly, Now);

        var failure = await lease.ApplyAsync(target, TestContext.Current.CancellationToken);

        failure.ShouldNotBeNull().Kind.ShouldBe(ProviderFailureKind.Authentication);
        target.Headers.ShouldBeEmpty();
    }

    [Fact]
    public async Task ApplyAsync_WhenLeaseWasDisposed_ThrowsObjectDisposedExceptionAndSetsNothing()
    {
        var lease = new ProviderCredentialLease(new ApiKeyProviderCredential(Secret));
        var target = new RecordingAuthenticationTarget();
        await lease.DisposeAsync();

        _ = await Should.ThrowAsync<ObjectDisposedException>(async () => await lease.ApplyAsync(target, TestContext.Current.CancellationToken));

        target.Headers.ShouldBeEmpty();
    }

    [Fact]
    public async Task DisposeAsync_WhenCalledTwice_DoesNotThrow()
    {
        var lease = new ProviderCredentialLease(new ApiKeyProviderCredential(Secret));

        await lease.DisposeAsync();
        await lease.DisposeAsync();
    }

    [Fact]
    public void ToString_WhenCalled_NeverContainsTheCredential()
    {
        var lease = new ProviderCredentialLease(new ApiKeyProviderCredential(Secret));

        lease.ToString().ShouldBe(nameof(ProviderCredentialLease));
        $"{lease}".ShouldNotContain(Secret);
    }

    [Fact]
    public async Task ApplyAsync_WhenCancelled_ThrowsBeforeSettingAnyHeader()
    {
        var lease = new ProviderCredentialLease(new ApiKeyProviderCredential(Secret));
        var target = new RecordingAuthenticationTarget();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await lease.ApplyAsync(target, cts.Token));

        target.Headers.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenCredentialIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ProviderCredentialLease(null!)).ParamName.ShouldBe("credential");

    [Fact]
    public async Task ApplyAsync_WhenTargetIsNull_ThrowsArgumentNullException()
    {
        var lease = new ProviderCredentialLease(new ApiKeyProviderCredential(Secret));

        var exception = await Should.ThrowAsync<ArgumentNullException>(async () => await lease.ApplyAsync(null!));

        exception.ParamName.ShouldBe("target");
    }
}
