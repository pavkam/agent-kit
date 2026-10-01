// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Tests.Credentials;

using AgentKit.TestSupport;

/// <summary>Verifies that <see cref="AwsSigV4CredentialLease"/> signs the described request and releases its credential.</summary>
public sealed class AwsSigV4CredentialLeaseTests
{
    private const string SecretKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY";

    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static AwsSigV4CredentialLease CreateLease(string? sessionToken = null) =>
        new(new AwsSigV4Credential("AKIAIOSFODNN7EXAMPLE", SecretKey, sessionToken), "us-east-1", "bedrock");

    [Fact]
    public async Task ApplyAsync_WhenTargetIsDescribed_SetsSigV4HeadersStampedWithTheTargetInstant()
    {
        var lease = CreateLease();
        var target = new RecordingAuthenticationTarget(utcNow: Now, body: Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"a\":1}"));

        var failure = await lease.ApplyAsync(target, TestContext.Current.CancellationToken);

        failure.ShouldBeNull();
        target.Headers["x-amz-date"].ShouldBe("20250601T120000Z");
        target.Headers["x-amz-content-sha256"].ShouldNotBeNullOrWhiteSpace();
        target.Headers["Authorization"].ShouldStartWith("AWS4-HMAC-SHA256 Credential=AKIAIOSFODNN7EXAMPLE/20250601/us-east-1/bedrock/aws4_request");
        target.Headers["Authorization"].ShouldContain("content-type");
        target.Headers.ContainsKey("x-amz-security-token").ShouldBeFalse();
    }

    [Fact]
    public async Task ApplyAsync_WhenCredentialHasASessionToken_SetsTheSecurityTokenHeader()
    {
        var target = new RecordingAuthenticationTarget(utcNow: Now);

        _ = await CreateLease("session-token-value").ApplyAsync(target, TestContext.Current.CancellationToken);

        target.Headers["x-amz-security-token"].ShouldBe("session-token-value");
    }

    [Fact]
    public async Task ApplyAsync_WhenBodyDiffers_ProducesADifferentSignature()
    {
        var first = new RecordingAuthenticationTarget(utcNow: Now, body: Encoding.UTF8.GetBytes("one"));
        var second = new RecordingAuthenticationTarget(utcNow: Now, body: Encoding.UTF8.GetBytes("two"));

        _ = await CreateLease().ApplyAsync(first, TestContext.Current.CancellationToken);
        _ = await CreateLease().ApplyAsync(second, TestContext.Current.CancellationToken);

        first.Headers["Authorization"].ShouldNotBe(second.Headers["Authorization"]);
    }

    [Fact]
    public async Task ApplyAsync_WhenSigned_NeverPlacesTheSecretKeyInAnyHeader()
    {
        var target = new RecordingAuthenticationTarget(utcNow: Now);

        _ = await CreateLease().ApplyAsync(target, TestContext.Current.CancellationToken);

        target.Headers.Values.ShouldAllBe(value => !value.Contains(SecretKey, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ApplyAsync_WhenLeaseWasDisposed_ThrowsObjectDisposedExceptionAndSetsNothing()
    {
        var lease = CreateLease();
        var target = new RecordingAuthenticationTarget(utcNow: Now);
        await lease.DisposeAsync();

        _ = await Should.ThrowAsync<ObjectDisposedException>(async () => await lease.ApplyAsync(target, TestContext.Current.CancellationToken));

        target.Headers.ShouldBeEmpty();
    }

    [Fact]
    public async Task ApplyAsync_WhenCancelled_ThrowsBeforeSigning()
    {
        var target = new RecordingAuthenticationTarget(utcNow: Now);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await CreateLease().ApplyAsync(target, cts.Token));

        target.Headers.ShouldBeEmpty();
    }

    [Fact]
    public void ToString_WhenCalled_PrintsOnlyTheTypeName() =>
        CreateLease("session").ToString().ShouldBe(nameof(AwsSigV4CredentialLease));

    [Fact]
    public async Task ApplyAsync_WhenTargetIsNull_ThrowsArgumentNullException()
    {
        var exception = await Should.ThrowAsync<ArgumentNullException>(async () => await CreateLease().ApplyAsync(null!));

        exception.ParamName.ShouldBe("target");
    }

    [Fact]
    public void Constructor_WhenArgumentIsInvalid_ThrowsTheExactException()
    {
        var credential = new AwsSigV4Credential("id", "secret", null);

        Should.Throw<ArgumentNullException>(() => new AwsSigV4CredentialLease(null!, "r", "s")).ParamName.ShouldBe("credential");
        Should.Throw<ArgumentException>(() => new AwsSigV4CredentialLease(credential, " ", "s")).ParamName.ShouldBe("region");
        Should.Throw<ArgumentException>(() => new AwsSigV4CredentialLease(credential, "r", "")).ParamName.ShouldBe("service");
    }
}
