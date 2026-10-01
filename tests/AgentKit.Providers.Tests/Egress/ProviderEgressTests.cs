// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests.Egress;

using System.Net;
using System.Net.Http;
using System.Text;

using AgentKit.Providers.Egress;
using AgentKit.TestSupport;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

/// <summary>Verifies <see cref="ProviderEgress"/> grant requests, fail-closed refusal, streaming, cancellation, and secrecy.</summary>
public sealed class ProviderEgressTests
{
    private const string Secret = "sk-egress-secret-value";

    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly ProviderOperationBinding Binding = new(
        new ProviderEndpointProfileReference(new ProviderEndpointProfileKey("openai-chat-endpoint"), new ProviderEndpointProfileVersion(3)),
        new ProviderCredentialProfileReference(new ProviderCredentialProfileKey("openai-credential"), new ProviderCredentialProfileVersion(5)));

    private static CallbackHandler Ok() =>
        new(static (_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") }));

    private static HttpRequestMessage Message(string uri = "https://api.provider.test/v1/chat/completions?api-version=2025-01-01")
    {
        var message = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(/*lang=json,strict*/ """{"prompt":"hello"}""", Encoding.UTF8, "application/json"),
        };
        _ = message.Headers.TryAddWithoutValidation("Authorization", $"Bearer {Secret}");
        _ = message.Headers.TryAddWithoutValidation("x-custom", "value");
        return message;
    }

    private static ProviderEgressRequest Request(
        HttpRequestMessage message,
        ProviderEgressOperation kind = ProviderEgressOperation.Conversation,
        ProtectedSemanticOperationContext? operation = null,
        ProviderOperationBinding? binding = null,
        string model = "gpt-test",
        int attempt = 1,
        DateTimeOffset? deadline = null,
        bool streaming = false,
        bool unauthenticated = false) =>
        new(
            unauthenticated ? null : operation ?? ProviderEgressHarness.Operation,
            kind,
            new ProviderId("openai"),
            new ApiFamilyId("openai-chat"),
            new ProviderServiceSurfaceId("chat"),
            new ProviderEndpointId("default"),
            binding ?? Binding,
            new ModelId(model),
            deploymentId: null,
            modelRevision: "7",
            attempt,
            deadline ?? Now.AddMinutes(1),
            streaming,
            message);

    [Fact]
    public async Task SendAsync_WhenConversationSucceeds_RequestsEgressResolutionAndSendGrantsInOrder()
    {
        var handler = Ok();
        var harness = ProviderEgressHarness.Create(handler, new FakeTimeProvider(Now));
        using var message = Message();

        var result = await harness.Egress.SendAsync(Request(message), TestContext.Current.CancellationToken);

        var sent = result.ShouldBeOfType<ProviderEgressSent>();
        await using var response = sent.Response;
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var requests = harness.Authority.Requests;
        requests.Select(static request => request.Audience).ShouldBe(
        [
            ProviderEgress.SecurityAudience,
            harness.Resolver.SecurityAudience,
            harness.Transport.SecurityAudience,
        ]);
        requests.ShouldAllBe(static request => request.Kind == SecurityOperationKind.Network && request.Effect == SecurityEffect.Egress);
        requests.Select(static request => request.InputFingerprint).Distinct().Count().ShouldBe(3);
        harness.Grants.Enforcements.Select(static enforcement => enforcement.Audience).ShouldBe(
            requests.Select(static request => request.Audience));
    }

    [Fact]
    public async Task SendAsync_WhenEgressGrantRequested_BindsDestinationBindingModelAndPayloadWithoutSecrets()
    {
        var harness = ProviderEgressHarness.Create(Ok(), new FakeTimeProvider(Now));
        using var message = Message();

        _ = await harness.Egress.SendAsync(Request(message), TestContext.Current.CancellationToken);

        var egress = harness.Authority.Requests[0];
        egress.Audience.ShouldBe(ProviderEgress.SecurityAudience);
        var resource = egress.Resources.ShouldHaveSingleItem();
        resource.Kind.ShouldBe(ProtectedResourceKind.NetworkEndpoint);
        resource.Identifier.ShouldStartWith("https://api.provider.test:443/v1/chat/completions?query=");
        resource.Identifier.ShouldNotContain("api-version");
        egress.Deadline.ShouldBe(Now.AddMinutes(1));
        egress.RequestedUses.ShouldBe(1);
        egress.Authorization.ShouldBe(ProviderEgressHarness.Operation.Authorization);
        egress.Identity.ShouldBe(ProviderEgressHarness.Operation.Identity);
        foreach (var request in harness.Authority.Requests)
        {
            request.InputFingerprint.Value.ShouldNotContain(Secret);
            request.Resources.ShouldAllBe(item => !item.Identifier.Contains(Secret, StringComparison.Ordinal));
        }

        harness.Audit.Records.ShouldNotBeEmpty();
        harness.Audit.Records.ShouldAllBe(record => !record.ToString()!.Contains(Secret, StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendAsync_WhenAnyBoundValueChanges_ProducesADifferentEgressFingerprint()
    {
        var variants = new Func<HttpRequestMessage, ProviderEgressRequest>[]
        {
            static message => Request(message),
            static message => Request(message, ProviderEgressOperation.Embedding),
            static message => Request(message, ProviderEgressOperation.Reranking),
            static message => Request(message, model: "gpt-other"),
            static message => Request(message, attempt: 2),
            static message => Request(message, streaming: true),
            static message => Request(message, deadline: Now.AddMinutes(2)),
            static message => Request(message, binding: new ProviderOperationBinding(
                Binding.Endpoint,
                new ProviderCredentialProfileReference(new ProviderCredentialProfileKey("openai-credential"), new ProviderCredentialProfileVersion(6)))),
            static message => Request(message, binding: new ProviderOperationBinding(
                new ProviderEndpointProfileReference(new ProviderEndpointProfileKey("openai-chat-endpoint"), new ProviderEndpointProfileVersion(4)),
                Binding.Credential)),
            static message => Request(message, binding: null!),
        };
        var fingerprints = new List<InputFingerprint>();
        foreach (var variant in variants)
        {
            var harness = ProviderEgressHarness.Create(Ok(), new FakeTimeProvider(Now));
            using var message = Message();
            var request = variant(message);
            _ = await harness.Egress.SendAsync(request, TestContext.Current.CancellationToken);
            fingerprints.Add(harness.Authority.Requests[0].InputFingerprint);
        }

        fingerprints.Distinct().Count().ShouldBe(variants.Length - 1);
    }

    [Theory]
    [InlineData("https://api.provider.test/v1/other")]
    [InlineData("https://api.provider.test:8443/v1/chat/completions?api-version=2025-01-01")]
    [InlineData("http://api.provider.test/v1/chat/completions?api-version=2025-01-01")]
    [InlineData("https://other.provider.test/v1/chat/completions?api-version=2025-01-01")]
    public async Task SendAsync_WhenDestinationChanges_BindsADifferentResourceAndFingerprint(string uri)
    {
        var baseline = ProviderEgressHarness.Create(Ok(), new FakeTimeProvider(Now));
        var changed = ProviderEgressHarness.Create(Ok(), new FakeTimeProvider(Now));
        using var first = Message();
        using var second = Message(uri);

        _ = await baseline.Egress.SendAsync(Request(first), TestContext.Current.CancellationToken);
        _ = await changed.Egress.SendAsync(Request(second), TestContext.Current.CancellationToken);

        changed.Authority.Requests[0].Resources[0].ShouldNotBe(baseline.Authority.Requests[0].Resources[0]);
        changed.Authority.Requests[0].InputFingerprint.ShouldNotBe(baseline.Authority.Requests[0].InputFingerprint);
    }

    [Fact]
    public async Task SendAsync_WhenPayloadOrClassificationChanges_ProducesADifferentEgressFingerprint()
    {
        var baseline = ProviderEgressHarness.Create(Ok(), new FakeTimeProvider(Now));
        var otherBody = ProviderEgressHarness.Create(Ok(), new FakeTimeProvider(Now));
        var otherClassification = ProviderEgressHarness.Create(
            Ok(),
            new FakeTimeProvider(Now),
            static options => options.Classification = NetworkDataClassification.Restricted);
        using var first = Message();
        using var second = Message();
        second.Content = new StringContent(/*lang=json,strict*/ """{"prompt":"different"}""", Encoding.UTF8, "application/json");
        using var third = Message();

        _ = await baseline.Egress.SendAsync(Request(first), TestContext.Current.CancellationToken);
        _ = await otherBody.Egress.SendAsync(Request(second), TestContext.Current.CancellationToken);
        _ = await otherClassification.Egress.SendAsync(Request(third), TestContext.Current.CancellationToken);

        var fingerprints = new[] { baseline, otherBody, otherClassification }
            .Select(static harness => harness.Authority.Requests[0].InputFingerprint)
            .ToArray();
        fingerprints.Distinct().Count().ShouldBe(3);
    }

    [Fact]
    public async Task SendAsync_WhenEgressGrantDenied_RefusesBeforeResolutionOrTransport()
    {
        var harness = ProviderEgressHarness.Create(Ok(), new FakeTimeProvider(Now));
        harness.Authority.Deny = static request => request.Audience == ProviderEgress.SecurityAudience;
        using var message = Message();

        var result = await harness.Egress.SendAsync(Request(message), TestContext.Current.CancellationToken);

        var refused = result.ShouldBeOfType<ProviderEgressRefused>();
        refused.Failure.Kind.ShouldBe(ProviderFailureKind.Authorization);
        refused.Failure.ProviderId.ShouldBe(new ProviderId("openai"));
        harness.Authority.Requests.Count.ShouldBe(1);
        harness.Resolver.Resolved.ShouldBeEmpty();
        ((HandlerNetworkTransport) harness.Transport).Requests.ShouldBeEmpty();
        harness.Grants.Enforcements.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenResolutionGrantDenied_RefusesBeforeTransport()
    {
        var harness = ProviderEgressHarness.Create(Ok(), new FakeTimeProvider(Now));
        harness.Authority.Deny = request => request.Audience == harness.Resolver.SecurityAudience;
        using var message = Message();

        var result = await harness.Egress.SendAsync(Request(message), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ProviderEgressRefused>().Failure.Kind.ShouldBe(ProviderFailureKind.Authorization);
        harness.Resolver.Resolved.ShouldBeEmpty();
        ((HandlerNetworkTransport) harness.Transport).Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenSendGrantDenied_RefusesBeforeTransmission()
    {
        var harness = ProviderEgressHarness.Create(Ok(), new FakeTimeProvider(Now));
        harness.Authority.Deny = request => request.Audience == harness.Transport.SecurityAudience;
        using var message = Message();

        var result = await harness.Egress.SendAsync(Request(message), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ProviderEgressRefused>().Failure.Kind.ShouldBe(ProviderFailureKind.Authorization);
        ((HandlerNetworkTransport) harness.Transport).Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenOperationContextIsMissing_RefusesWithoutAskingAnyAuthority()
    {
        var harness = ProviderEgressHarness.Create(Ok(), new FakeTimeProvider(Now));
        using var message = Message();

        var result = await harness.Egress.SendAsync(Request(message, unauthenticated: true), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ProviderEgressRefused>().Failure.Kind.ShouldBe(ProviderFailureKind.Authorization);
        harness.Authority.Requests.ShouldBeEmpty();
        harness.Resolver.Resolved.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenAuthorityIsUnavailable_RefusesFailClosedRetainingTheCause()
    {
        var harness = ProviderEgressHarness.Create(Ok(), new FakeTimeProvider(Now));
        var fault = new InvalidOperationException("policy store offline");
        harness.Authority.Fault = fault;
        using var message = Message();

        var result = await harness.Egress.SendAsync(Request(message), TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<ProviderEgressRefused>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Authorization);
        failure.DiagnosticCause.ShouldBe(fault);
        failure.SafeMessage.ShouldNotContain("policy store offline");
        harness.Resolver.Resolved.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenGrantStoreIsUnavailable_RefusesFailClosedBeforeResolution()
    {
        var harness = ProviderEgressHarness.Create(Ok(), new FakeTimeProvider(Now));
        harness.Grants.ConsumptionFault = new SecurityGrantStoreUnavailableException(
            SecurityGrantStoreFailureKind.Busy,
            "The grant store is busy.");
        using var message = Message();

        var result = await harness.Egress.SendAsync(Request(message), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ProviderEgressRefused>().Failure.Kind.ShouldBe(ProviderFailureKind.Authorization);
        harness.Resolver.Resolved.ShouldBeEmpty();
        ((HandlerNetworkTransport) harness.Transport).Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SendAsync_WhenRequiredAuditIsUnavailable_RefusesBeforeAnyIo(bool unavailable)
    {
        var harness = ProviderEgressHarness.Create(Ok(), new FakeTimeProvider(Now));
        harness.Audit.Result = unavailable
            ? new SecurityAuditUnavailable("sink offline")
            : new SecurityAuditFailed("sink failed");
        using var message = Message();

        var result = await harness.Egress.SendAsync(Request(message), TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<ProviderEgressRefused>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Authorization);
        failure.SafeMessage.ShouldBe("Required security audit failed.");
        harness.Resolver.Resolved.ShouldBeEmpty();
        ((HandlerNetworkTransport) harness.Transport).Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenTransportDeniesItsOwnGrant_RefusesAsAuthorization()
    {
        var harness = ProviderEgressHarness.Create(
            grants => new CallbackNetworkTransport(grants, static (_, _) => ValueTask.FromResult<NetworkSendResult>(new NetworkDenied("Excluded by network policy."))),
            new FakeTimeProvider(Now));
        using var message = Message();

        var result = await harness.Egress.SendAsync(Request(message), TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<ProviderEgressRefused>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.Authorization);
        failure.SafeMessage.ShouldBe("Excluded by network policy.");
    }

    [Fact]
    public async Task SendAsync_WhenDeadlineAlreadyElapsed_RefusesAsTimeoutWithoutRequestingAnyGrant()
    {
        var harness = ProviderEgressHarness.Create(Ok(), new FakeTimeProvider(Now));
        using var message = Message();

        var result = await harness.Egress.SendAsync(Request(message, deadline: Now), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ProviderEgressRefused>().Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
        harness.Authority.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenCancelledBeforeStart_RefusesAsCancellationWithoutIo()
    {
        var harness = ProviderEgressHarness.Create(Ok(), new FakeTimeProvider(Now));
        using var message = Message();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await harness.Egress.SendAsync(Request(message), cancellation.Token);

        result.ShouldBeOfType<ProviderEgressRefused>().Failure.Kind.ShouldBe(ProviderFailureKind.Cancellation);
        harness.Resolver.Resolved.ShouldBeEmpty();
        ((HandlerNetworkTransport) harness.Transport).Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenCancelledWhileAwaitingResponse_RefusesAsCancellation()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new CallbackHandler(
            async (_, token) =>
            {
                entered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
        var harness = ProviderEgressHarness.Create(handler, new FakeTimeProvider(Now));
        using var message = Message();
        using var cancellation = new CancellationTokenSource();

        var pending = harness.Egress.SendAsync(Request(message), cancellation.Token).AsTask();
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        var result = await pending;

        result.ShouldBeOfType<ProviderEgressRefused>().Failure.Kind.ShouldBe(ProviderFailureKind.Cancellation);
    }

    [Fact]
    public async Task SendAsync_WhenDeadlineElapsesWhileAwaitingResponse_RefusesAsTimeoutNotCancellation()
    {
        var time = new FakeTimeProvider(Now);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new CallbackHandler(
            async (_, token) =>
            {
                entered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
        var harness = ProviderEgressHarness.Create(handler, time);
        using var message = Message();

        var pending = harness.Egress.SendAsync(Request(message, deadline: Now.AddSeconds(5)), TestContext.Current.CancellationToken).AsTask();
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(6));
        var result = await pending;

        result.ShouldBeOfType<ProviderEgressRefused>().Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
    }

    [Theory]
    [InlineData(NetworkFailureKind.Timeout, ProviderFailureKind.Timeout)]
    [InlineData(NetworkFailureKind.ConnectionFailed, ProviderFailureKind.Unavailable)]
    [InlineData(NetworkFailureKind.TlsFailure, ProviderFailureKind.Unavailable)]
    [InlineData(NetworkFailureKind.DnsResolutionFailed, ProviderFailureKind.Unavailable)]
    [InlineData(NetworkFailureKind.ProtocolViolation, ProviderFailureKind.ProtocolViolation)]
    [InlineData(NetworkFailureKind.UnsupportedScheme, ProviderFailureKind.InvalidRequest)]
    public async Task SendAsync_WhenTransportFails_MapsToTheStableProviderTaxonomy(NetworkFailureKind network, ProviderFailureKind expected)
    {
        var harness = ProviderEgressHarness.Create(
            grants => new CallbackNetworkTransport(
                grants,
                (_, _) => ValueTask.FromResult<NetworkSendResult>(new NetworkRequestFailed(network, "Transport failed.", sideEffectCertain: true))),
            new FakeTimeProvider(Now));
        using var message = Message();

        var result = await harness.Egress.SendAsync(Request(message), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ProviderEgressRefused>().Failure.Kind.ShouldBe(expected);
    }

    [Fact]
    public async Task SendAsync_WhenProviderRedirects_RefusesAndNeverFollowsOrForwardsCredentials()
    {
        var redirected = new NetworkDestination("https", new NormalizedHost("elsewhere.test"), 443, NetworkRoute.Root);
        var harness = ProviderEgressHarness.Create(
            grants => new CallbackNetworkTransport(
                grants,
                (_, _) => ValueTask.FromResult<NetworkSendResult>(new NetworkRedirectReceived(redirected, crossOrigin: true))),
            new FakeTimeProvider(Now));
        using var message = Message();

        var result = await harness.Egress.SendAsync(Request(message), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ProviderEgressRefused>().Failure.Kind.ShouldBe(ProviderFailureKind.ProtocolViolation);
        var transport = (CallbackNetworkTransport) harness.Transport;
        transport.Requests.ShouldHaveSingleItem().Bounds.MaximumRedirects.ShouldBe(0);
        harness.Resolver.Resolved.ShouldHaveSingleItem().Host.Value.ShouldBe("api.provider.test");
    }

    [Fact]
    public async Task SendAsync_WhenAdmitted_SendsFrozenBodyHeadersAndBoundsThroughTheTransport()
    {
        var harness = ProviderEgressHarness.Create(Ok(), new FakeTimeProvider(Now));
        using var message = Message();

        await using var response = (await harness.Egress.SendAsync(Request(message), TestContext.Current.CancellationToken))
            .ShouldBeOfType<ProviderEgressSent>().Response;

        var sent = ((HandlerNetworkTransport) harness.Transport).Requests.ShouldHaveSingleItem();
        sent.Method.ShouldBe(NetworkMethod.Post);
        sent.Destination.ToString().ShouldBe("https://api.provider.test:443/v1/chat/completions?api-version=2025-01-01");
        sent.Classification.ShouldBe(NetworkDataClassification.Confidential);
        sent.Headers.GetValues("Authorization").ShouldBe([$"Bearer {Secret}"]);
        sent.Content.ShouldBeOfType<NetworkRequestContent>().ContentType.ShouldStartWith("application/json");
        Encoding.UTF8.GetString(sent.Content.ShouldBeOfType<NetworkRequestContent>().Body.Span).ShouldBe(/*lang=json,strict*/ """{"prompt":"hello"}""");
        sent.Bounds.MaximumRedirects.ShouldBe(0);
        sent.Bounds.ResponseTimeout.ShouldBe(TimeSpan.FromMinutes(1));
        sent.Bounds.MaximumResponseBytes.ShouldBe(new ProviderEgressOptions().MaximumResponseBytes);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(64)]
    [InlineData(4096)]
    public async Task SendAsync_WhenBodyIsFragmented_StreamsEveryByteIncrementallyWithoutBuffering(int chunkSize)
    {
        var payload = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Range(0, 40).Select(static index => $"data: {{\"n\":{index}}}\n\n")));
        var harness = ProviderEgressHarness.Create(
            grants => new CallbackNetworkTransport(
                grants,
                (_, _) => ValueTask.FromResult<NetworkSendResult>(new NetworkResponseReceived(new StreamNetworkResponse(
                    200,
                    new ChunkedStream(payload, chunkSize),
                    new NetworkHeader("Content-Type", "text/event-stream"),
                    new NetworkHeader("x-request-id", "req-1"))))),
            new FakeTimeProvider(Now));
        using var message = Message();

        await using var response = (await harness.Egress.SendAsync(Request(message, streaming: true), TestContext.Current.CancellationToken))
            .ShouldBeOfType<ProviderEgressSent>().Response;
        var body = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        var buffer = new byte[8192];
        var first = await body.ReadAsync(buffer, TestContext.Current.CancellationToken);
        using var collected = new MemoryStream();
        collected.Write(buffer, 0, first);
        int read;
        while ((read = await body.ReadAsync(buffer, TestContext.Current.CancellationToken)) > 0)
        {
            collected.Write(buffer, 0, read);
        }

        first.ShouldBeLessThanOrEqualTo(chunkSize);
        collected.ToArray().ShouldBe(payload);
        response.Headers.TryGetValues("x-request-id", out var ids).ShouldBeTrue();
        ids.ShouldBe(["req-1"]);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");
    }

    [Fact]
    public async Task SendAsync_WhenBodyOverrunsStreamed_SurfacesTheTransportExceptionFromTheBody()
    {
        var harness = ProviderEgressHarness.Create(
            grants => new CallbackNetworkTransport(
                grants,
                (_, _) => ValueTask.FromResult<NetworkSendResult>(new NetworkResponseReceived(new StreamNetworkResponse(
                    200,
                    new FaultingReadStream(static () => new NetworkResponseTooLargeException(4, 5), [1, 2]))))),
            new FakeTimeProvider(Now));
        using var message = Message();

        await using var response = (await harness.Egress.SendAsync(Request(message), TestContext.Current.CancellationToken))
            .ShouldBeOfType<ProviderEgressSent>().Response;
        var body = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);

        _ = await Should.ThrowAsync<NetworkResponseTooLargeException>(async () =>
            await body.CopyToAsync(Stream.Null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendAsync_WhenResponseDisposed_ReleasesTheNetworkResponseExactlyOnce()
    {
        StreamNetworkResponse? network = null;
        var harness = ProviderEgressHarness.Create(
            grants => new CallbackNetworkTransport(
                grants,
                (_, _) => ValueTask.FromResult<NetworkSendResult>(
                    new NetworkResponseReceived(network = new StreamNetworkResponse(200, new MemoryStream([1]))))),
            new FakeTimeProvider(Now));
        using var message = Message();
        var response = (await harness.Egress.SendAsync(Request(message), TestContext.Current.CancellationToken))
            .ShouldBeOfType<ProviderEgressSent>().Response;

        network!.IsDisposed.ShouldBeFalse();
        await response.DisposeAsync();
        await response.DisposeAsync();

        network.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task SendAsync_WhenRequestBodyExceedsConfiguredBound_RefusesBeforeAnyGrant()
    {
        var harness = ProviderEgressHarness.Create(
            Ok(),
            new FakeTimeProvider(Now),
            static options => options.MaximumRequestBytes = 4);
        using var message = Message();

        var result = await harness.Egress.SendAsync(Request(message), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ProviderEgressRefused>().Failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        harness.Authority.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("ftp://api.provider.test/v1")]
    [InlineData("https://user:pass@api.provider.test/v1")]
    [InlineData("https://api.provider.test/v1#fragment")]
    public async Task SendAsync_WhenEndpointIsNotAPlainHttpAddress_RefusesAsInvalidRequestWithoutIo(string uri)
    {
        var harness = ProviderEgressHarness.Create(Ok(), new FakeTimeProvider(Now));
        using var message = Message(uri);

        var result = await harness.Egress.SendAsync(Request(message), TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<ProviderEgressRefused>().Failure;
        failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
        failure.SafeMessage.ShouldNotContain("pass");
        harness.Authority.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenRefused_LeavesNoSecretsInLogsActivitiesMetricsOrFailure()
    {
        var logger = new RecordingLogger<ProviderEgress>();
        var harness = ProviderEgressHarness.Create(Ok(), new FakeTimeProvider(Now), logger: logger);
        harness.Authority.Fault = new InvalidOperationException("authority unavailable");
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            static observation => observation.OperationName == AgentKitActivityNames.ProviderEgress);
        using var metrics = new MetricCollector(AgentKitMetricNames.ProviderEgressCount);
        using var message = Message();

        var result = await harness.Egress.SendAsync(Request(message), TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<ProviderEgressRefused>().Failure;
        failure.SafeMessage.ShouldNotContain(Secret);
        SignalAssertions.ShouldNotContainContent(
            activities.Snapshot(),
            logger.Snapshot(),
            metrics.Snapshot(),
            Secret,
            "hello",
            "api-version",
            "/v1/chat/completions");
        var log = logger.Snapshot().ShouldHaveSingleItem();
        log.Level.ShouldBe(LogLevel.Warning);
        log.EventId.Id.ShouldBe(6121);
        activities.Snapshot().ShouldHaveSingleItem().Status.ShouldBe(ActivityStatusCode.Error);
        metrics.Snapshot().ShouldHaveSingleItem().Tags[AgentKitTagNames.Outcome].ShouldBe("authorization");
    }

    [Fact]
    public async Task SendAsync_WhenAdmitted_RecordsSuccessSignalsWithBoundedDimensionsOnly()
    {
        var logger = new RecordingLogger<ProviderEgress>();
        var harness = ProviderEgressHarness.Create(Ok(), new FakeTimeProvider(Now), logger: logger);
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            static observation => observation.OperationName == AgentKitActivityNames.ProviderEgress);
        using var metrics = new MetricCollector(AgentKitMetricNames.ProviderEgressCount);
        using var message = Message();

        await using var response = (await harness.Egress.SendAsync(Request(message), TestContext.Current.CancellationToken))
            .ShouldBeOfType<ProviderEgressSent>().Response;

        var activity = activities.Snapshot().ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.Tags[AgentKitTagNames.ProviderName].ShouldBe("openai");
        activity.Tags[AgentKitTagNames.ProviderOperation].ShouldBe("chat");
        var metric = metrics.Snapshot().ShouldHaveSingleItem();
        metric.Tags.Keys.Order().ShouldBe([AgentKitTagNames.Outcome, AgentKitTagNames.ProviderOperation]);
        metric.Tags[AgentKitTagNames.Outcome].ShouldBe("sent");
        logger.Snapshot().ShouldHaveSingleItem().EventId.Id.ShouldBe(6120);
        SignalAssertions.ShouldNotContainContent(activities.Snapshot(), logger.Snapshot(), metrics.Snapshot(), Secret, "hello");
    }

    [Fact]
    public void Constructor_WhenCollaboratorIsNull_ThrowsArgumentNullException()
    {
        var harness = ProviderEgressHarness.Create(Ok(), new FakeTimeProvider(Now));

        var exception = Should.Throw<ArgumentNullException>(() => new ProviderEgress(
            null!,
            harness.Grants,
            harness.Audit,
            harness.Resolver,
            harness.Transport,
            new SequenceIds<SecurityRequestId>(static value => new SecurityRequestId(value)),
            new SequenceIds<NetworkOperationId>(static value => new NetworkOperationId(value)),
            new SequenceIds<SecurityEnforcementIntentId>(static value => new SecurityEnforcementIntentId(value)),
            new SequenceIds<SecurityAuditRecordId>(static value => new SecurityAuditRecordId(value)),
            TimeProvider.System,
            Options.Create(new ProviderEgressOptions())));

        exception.ParamName.ShouldBe("authoritySelector");
    }

    [Theory]
    [InlineData("connect")]
    [InlineData("request")]
    [InlineData("response")]
    [InlineData("classification")]
    public void Constructor_WhenOptionIsInvalid_ThrowsArgumentOutOfRangeException(string option)
    {
        var harness = ProviderEgressHarness.Create(Ok(), new FakeTimeProvider(Now));
        var options = new ProviderEgressOptions();
        switch (option)
        {
            case "connect":
                options.ConnectTimeout = TimeSpan.Zero;
                break;
            case "request":
                options.MaximumRequestBytes = 0;
                break;
            case "response":
                options.MaximumResponseBytes = -1;
                break;
            default:
                options.Classification = (NetworkDataClassification) 99;
                break;
        }

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ProviderEgress(
            new FixedSecurityAuthoritySelector(harness.Authority),
            harness.Grants,
            harness.Audit,
            harness.Resolver,
            harness.Transport,
            new SequenceIds<SecurityRequestId>(static value => new SecurityRequestId(value)),
            new SequenceIds<NetworkOperationId>(static value => new NetworkOperationId(value)),
            new SequenceIds<SecurityEnforcementIntentId>(static value => new SecurityEnforcementIntentId(value)),
            new SequenceIds<SecurityAuditRecordId>(static value => new SecurityAuditRecordId(value)),
            TimeProvider.System,
            Options.Create(options)));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public async Task SendAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var harness = ProviderEgressHarness.Create(Ok(), new FakeTimeProvider(Now));

        var exception = await Should.ThrowAsync<ArgumentNullException>(async () =>
            await harness.Egress.SendAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public void ProviderEgressRequestConstructor_WhenUriIsRelative_ThrowsArgumentException()
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri("/relative", UriKind.Relative));

        var exception = Should.Throw<ArgumentException>(() => Request(message));

        exception.ParamName.ShouldBe("message");
    }

    [Fact]
    public void ProviderEgressRequestConstructor_WhenAttemptIsBelowOne_ThrowsArgumentOutOfRangeException()
    {
        using var message = Message();

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Request(message, attempt: 0));

        exception.ParamName.ShouldBe("attempt");
    }

    [Fact]
    public async Task AddAgentProviders_WhenTransportReplaced_ProviderEgressSendsThroughTheReplacement()
    {
        var time = new FakeTimeProvider(Now);
        var grants = new ConsumingGrantStore();
        var authority = new GrantingSecurityAuthority(grants);
        var transport = new CallbackNetworkTransport(
            grants,
            static (_, _) => ValueTask.FromResult<NetworkSendResult>(new NetworkResponseReceived(new StreamNetworkResponse(202, new MemoryStream([1])))));
        var services = new ServiceCollection();
        _ = services.AddSingleton<TimeProvider>(time);
        _ = services.AddSingleton<ISecurityAuthoritySelector>(new FixedSecurityAuthoritySelector(authority));
        _ = services.AddSingleton<ISecurityGrantStore>(grants);
        _ = services.AddSingleton<ISecurityAuditDispatcher>(new RecordingAuditDispatcher());
        _ = services.AddSingleton<INetworkNameResolver>(new FixedAddressNameResolver(grants, time));
        _ = services.AddSingleton<INetworkTransport>(transport);
        _ = services.AddAgentProviders();
        await using var provider = services.BuildServiceProvider();
        using var message = Message();

        var egress = provider.GetRequiredService<ProviderEgress>();
        var result = await egress.SendAsync(Request(message), TestContext.Current.CancellationToken);

        await using var response = result.ShouldBeOfType<ProviderEgressSent>().Response;
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _ = transport.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public void AddAgentProviders_WhenNoTransportRegistered_ResolvingProviderEgressFailsInsteadOfFallingBack()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentProviders();
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<InvalidOperationException>(provider.GetRequiredService<ProviderEgress>);
    }

    [Fact]
    public void AddAgentProviders_WhenEgressOptionsInvalid_ValidationFailsOnResolution()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentProviders();
        _ = services.Configure<ProviderEgressOptions>(static options => options.ConnectTimeout = TimeSpan.Zero);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<ProviderEgressOptions>>().Value);
    }

    private sealed class SequenceIds<TId>(Func<Guid, TId> factory): IIdentifierGenerator<TId>
        where TId : struct
    {
        private int _value;

        public TId Create() => factory(Guid.Parse($"60000000-0000-0000-0000-{Interlocked.Increment(ref _value):D12}"));
    }

    private sealed class CallbackHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback): HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            callback(request, cancellationToken);
    }
}
