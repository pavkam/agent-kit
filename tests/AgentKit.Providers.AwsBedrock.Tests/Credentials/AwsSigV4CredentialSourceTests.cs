// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Tests.Credentials;

using System.Net;
using System.Net.Http;

using AgentKit.TestSupport;

/// <summary>Verifies that <see cref="AwsSigV4CredentialSource"/> reads the AWS credential only under a validated grant.</summary>
public sealed class AwsSigV4CredentialSourceTests
{
    private const string SecretKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY";

    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly ProviderCredentialSourceKey Key = new("aws.source");

    private static ProviderEgressHarness CreateHarness() => ProviderEgressHarness.Create(new StubHandler(), new FakeTimeProvider(Now));

    private static AwsSigV4CredentialSource CreateSource(ProviderEgressHarness harness, IAwsCredentialSource supplier) =>
        new(Key, supplier, "us-east-1", "bedrock", harness.Gate);

    [Fact]
    public async Task ResolveAsync_WhenGrantIsValid_ReadsTheSupplierOnceAndReleasesASigningLease()
    {
        var harness = CreateHarness();
        var supplier = new CountingSupplier(new AwsSigV4Credential("AKIAEXAMPLE", SecretKey, null));

        var probe = await ProviderCredentialProbe.ProbeAsync(CreateSource(harness, supplier), harness.Authority, utcNow: Now, cancellationToken: TestContext.Current.CancellationToken);

        supplier.Calls.ShouldBe(1);
        probe.Headers["Authorization"].ShouldStartWith("AWS4-HMAC-SHA256 Credential=AKIAEXAMPLE/20250601/us-east-1/bedrock/aws4_request");
        probe.Headers.Values.ShouldAllBe(value => !value.Contains(SecretKey, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResolveAsync_WhenGrantIsDenied_NeverReadsTheSupplier()
    {
        var harness = CreateHarness();
        var supplier = new CountingSupplier(new AwsSigV4Credential("AKIAEXAMPLE", SecretKey, null));
        var source = CreateSource(harness, supplier);
        var request = await ProviderCredentialProbe.CreateAuthorizedRequestAsync(source, harness.Authority, Now, TestContext.Current.CancellationToken);

        var probe = await ProviderCredentialProbe.ResolveAndApplyAsync(source, request with { Attempt = 5 }, cancellationToken: TestContext.Current.CancellationToken);

        _ = probe.Resolution.ShouldBeOfType<ProviderCredentialUnavailable>();
        supplier.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ResolveAsync_WhenSupplierThrows_ReturnsAuthenticationFailureWithFixedMessage()
    {
        var harness = CreateHarness();
        var fault = new InvalidOperationException("imds said " + SecretKey);
        var source = CreateSource(harness, new CountingSupplier(null, fault));
        var request = await ProviderCredentialProbe.CreateAuthorizedRequestAsync(source, harness.Authority, Now, TestContext.Current.CancellationToken);

        var probe = await ProviderCredentialProbe.ResolveAndApplyAsync(source, request, cancellationToken: TestContext.Current.CancellationToken);

        var failure = probe.Resolution.ShouldBeOfType<ProviderCredentialUnavailable>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        failure.SafeMessage.ShouldBe("The AWS credential could not be resolved.");
        failure.DiagnosticCause.ShouldBeSameAs(fault);
    }

    [Fact]
    public async Task ResolveAsync_WhenSupplierReturnsNull_ReturnsAuthenticationFailure()
    {
        var harness = CreateHarness();
        var source = CreateSource(harness, new CountingSupplier(null));
        var request = await ProviderCredentialProbe.CreateAuthorizedRequestAsync(source, harness.Authority, Now, TestContext.Current.CancellationToken);

        var probe = await ProviderCredentialProbe.ResolveAndApplyAsync(source, request, cancellationToken: TestContext.Current.CancellationToken);

        probe.Resolution.ShouldBeOfType<ProviderCredentialUnavailable>().Failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
    }

    [Fact]
    public void ToString_WhenCalled_NamesTheKeyAndNeverTheCredential() =>
        CreateSource(CreateHarness(), new CountingSupplier(new AwsSigV4Credential("id", SecretKey, null))).ToString()
            .ShouldBe("AwsSigV4CredentialSource { Key = aws.source }");

    [Fact]
    public void Constructor_WhenArgumentIsInvalid_ThrowsTheExactException()
    {
        var gate = CreateHarness().Gate;
        var supplier = new CountingSupplier(null);

        Should.Throw<ArgumentOutOfRangeException>(() => new AwsSigV4CredentialSource(default, supplier, "r", "s", gate)).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => new AwsSigV4CredentialSource(Key, null!, "r", "s", gate)).ParamName.ShouldBe("supplier");
        Should.Throw<ArgumentException>(() => new AwsSigV4CredentialSource(Key, supplier, " ", "s", gate)).ParamName.ShouldBe("region");
        Should.Throw<ArgumentException>(() => new AwsSigV4CredentialSource(Key, supplier, "r", "", gate)).ParamName.ShouldBe("service");
        Should.Throw<ArgumentNullException>(() => new AwsSigV4CredentialSource(Key, supplier, "r", "s", null!)).ParamName.ShouldBe("gate");
    }

    [Fact]
    public async Task ResolveAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var source = CreateSource(CreateHarness(), new CountingSupplier(null));

        var exception = await Should.ThrowAsync<ArgumentNullException>(async () => await source.ResolveAsync(null!));

        exception.ParamName.ShouldBe("request");
    }

    private sealed class CountingSupplier(AwsSigV4Credential? credential, Exception? fault = null): IAwsCredentialSource
    {
        public int Calls { get; private set; }

        public ValueTask<AwsSigV4Credential> GetCredentialAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return fault is null ? ValueTask.FromResult(credential!) : throw fault;
        }
    }

    private sealed class StubHandler: HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
