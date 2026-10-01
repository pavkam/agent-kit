// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.WebSearch.Tests;

using System.Net;
using System.Net.Http;
using System.Text;

using AgentKit.Observability;
using AgentKit.TestSupport;

using Microsoft.Extensions.Time.Testing;

/// <summary>Verifies <see cref="NetworkWebSearchProvider"/> grant binding, protected network egress, and wire parsing.</summary>
public sealed class NetworkWebSearchProviderTests
{
    private const string ValidBody = /*lang=json,strict*/ """
        {
          "complete": true,
          "results": [
            {
              "title": "AgentKit",
              "url": "https://example.com/docs",
              "snippet": "Documentation.",
              "published_at": "2024-01-02T03:04:05Z"
            }
          ]
        }
        """;

    private const string Needle = "classified-needle-query";

    private static readonly DateTimeOffset Epoch = DateTimeOffset.UnixEpoch;

    [Fact]
    public async Task SearchAsync_WhenResponseValid_ReturnsSucceededResults()
    {
        var harness = NetworkWebSearchProviderHarness.Create(Json(ValidBody));
        var request = await harness.AuthorizedRequestAsync();

        var result = await harness.Provider.SearchAsync(request, TestContext.Current.CancellationToken);

        var success = result.ShouldBeOfType<WebSearchSucceeded>();
        success.Complete.ShouldBeTrue();
        var item = success.Items.ShouldHaveSingleItem();
        item.Title.ShouldBe("AgentKit");
        item.PublishedAt.ShouldBe(new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero));
    }

    [Fact]
    public async Task SearchAsync_WhenAuthorized_SendsOneBoundedGetWithoutRedirectsOrBody()
    {
        var harness = NetworkWebSearchProviderHarness.Create(
            Json(ValidBody),
            configure: static options =>
            {
                options.MaximumResponseBytes = 4096;
                options.ConnectTimeout = TimeSpan.FromSeconds(7);
                options.Classification = NetworkDataClassification.Restricted;
            });
        var request = await harness.AuthorizedRequestAsync(
            Needle,
            [new NormalizedHost("docs.example.com")],
            WebSearchFreshness.Week,
            maximumResults: 3);

        _ = await harness.Provider.SearchAsync(request, TestContext.Current.CancellationToken);

        var sent = harness.Requests.ShouldHaveSingleItem();
        sent.Method.ShouldBe(NetworkMethod.Get);
        sent.Destination.Scheme.ShouldBe("https");
        sent.Destination.Host.Value.ShouldBe("search.example.test");
        sent.Destination.Port.ShouldBe(443);
        sent.Destination.Route.Value.ShouldBe(
            $"/query?q={Needle}&maximum_results=3&freshness=week&domains=docs.example.com");
        sent.Content.ShouldBeNull();
        sent.Classification.ShouldBe(NetworkDataClassification.Restricted);
        sent.Bounds.Response.MaximumRedirects.ShouldBe(0);
        sent.Bounds.Response.MaximumResponseBytes.ShouldBe(4096);
        sent.Bounds.Request.ConnectTimeout.ShouldBe(TimeSpan.FromSeconds(7));
        sent.Bounds.Resolution.ResolutionTimeout.ShouldBe(TimeSpan.FromSeconds(7));
        sent.Bounds.Response.ResponseTimeout.ShouldBe(TimeSpan.FromMinutes(1));
        sent.Headers.Headers.ShouldHaveSingleItem().ShouldBe(new NetworkHeader("Accept", "application/json"));
    }

    [Fact]
    public async Task SearchAsync_WhenAuthorized_ObtainsThreeDistinctGrantsWithExactContents()
    {
        var harness = NetworkWebSearchProviderHarness.Create(Json(ValidBody));
        var request = await harness.AuthorizedRequestAsync(Needle);

        _ = await harness.Provider.SearchAsync(request, TestContext.Current.CancellationToken);

        var requests = harness.Authority.Requests;
        requests.Count.ShouldBe(3);
        requests.ShouldAllBe(static item => item.Kind == SecurityOperationKind.Network && item.Effect == SecurityEffect.Egress);
        requests.ShouldAllBe(static item => item.ToolCallId == TestData.Context.ToolCallId);
        requests.ShouldAllBe(item => item.Deadline == request.Deadline);
        requests.ShouldAllBe(static item => item.Authorization == TestData.Context.Authorization);
        requests.Select(static item => item.Audience).ShouldBe(
            [harness.Provider.SecurityAudience, harness.Resolver.SecurityAudience, harness.Transport.SecurityAudience]);

        var resolution = requests[1];
        resolution.Resources.ShouldHaveSingleItem().Identifier.ShouldBe("https://search.example.test:443");

        var send = requests[2];
        send.Resources.Length.ShouldBe(2);
        var route = send.Resources[0].Identifier;
        route.ShouldStartWith("https://search.example.test:443/query?query=");
        send.Resources[1].Identifier.ShouldBe("93.184.216.34:443");
        send.InputFingerprint.ShouldBe(NetworkSecurityBinding.RequestFingerprint(harness.Requests.Single()));

        foreach (var item in requests.Skip(1))
        {
            item.Resources.ShouldAllBe(resource => !resource.Identifier.Contains(Needle, StringComparison.Ordinal));
        }

        harness.Grants.Enforcements.Select(static item => item.Audience).ShouldBe(
            [harness.Provider.SecurityAudience, harness.Resolver.SecurityAudience, harness.Transport.SecurityAudience]);
        harness.Grants.Enforcements[0].InputFingerprint.ShouldBe(request.Grant.InputFingerprint);
    }

    [Fact]
    public async Task SearchAsync_WhenAuthorized_ConsumesSearchGrantWithRequiredAuditBeforeAnyNetworkEffect()
    {
        var harness = NetworkWebSearchProviderHarness.Create(Json(ValidBody));
        var request = await harness.AuthorizedRequestAsync(Needle);

        _ = await harness.Provider.SearchAsync(request, TestContext.Current.CancellationToken);

        var audit = harness.Audit.Records.ShouldHaveSingleItem();
        audit.EventKind.ShouldBe(SecurityAuditEventKind.GrantConsumptionIntent);
        audit.GrantId.ShouldBe(request.Grant.Id);
        harness.Grants.Enforcements[0].Audience.ShouldBe(harness.Provider.SecurityAudience);
    }

    [Fact]
    public async Task SearchAsync_WhenGrantMismatches_ReturnsDeniedBeforeAnyAuthorityOrNetworkCall()
    {
        var harness = NetworkWebSearchProviderHarness.Create(Json(ValidBody));
        var request = await harness.AuthorizedRequestAsync();
        var requestsBefore = harness.Authority.Requests.Count;
        var tampered = request with { Grant = request.Grant with { InputFingerprint = new InputFingerprint("deadbeef") } };

        var result = await harness.Provider.SearchAsync(tampered, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<WebSearchDenied>();
        harness.Authority.Requests.Count.ShouldBe(requestsBefore);
        harness.Grants.Enforcements.ShouldBeEmpty();
        harness.Resolver.Resolved.ShouldBeEmpty();
        harness.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_WhenGrantAudienceDiffers_ReturnsDeniedBeforeAnyEffect()
    {
        var harness = NetworkWebSearchProviderHarness.Create(Json(ValidBody));
        var request = await harness.AuthorizedRequestAsync();
        var foreign = request with { Grant = request.Grant with { Audience = new ComponentId("another.component") } };

        var result = await harness.Provider.SearchAsync(foreign, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<WebSearchDenied>();
        harness.Grants.Enforcements.ShouldBeEmpty();
        harness.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_WhenSearchGrantAlreadyConsumed_DeniesReplayBeforeResolution()
    {
        var harness = NetworkWebSearchProviderHarness.Create(Json(ValidBody));
        var request = await harness.AuthorizedRequestAsync();
        _ = await harness.Provider.SearchAsync(request, TestContext.Current.CancellationToken);

        var replay = await harness.Provider.SearchAsync(request, TestContext.Current.CancellationToken);

        _ = replay.ShouldBeOfType<WebSearchDenied>();
        _ = harness.Resolver.Resolved.ShouldHaveSingleItem();
        _ = harness.Requests.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("test.network.resolver", 0, 0)]
    [InlineData("test.network.transport", 1, 0)]
    public async Task SearchAsync_WhenAuthorityDeniesNetworkGrant_ReturnsDeniedWithoutLaterEffects(
        string deniedAudience,
        int expectedResolutions,
        int expectedSends)
    {
        var harness = NetworkWebSearchProviderHarness.Create(Json(ValidBody));
        harness.Authority.Deny = item => item.Audience.Value == deniedAudience;
        var request = await harness.AuthorizedRequestAsync();

        var result = await harness.Provider.SearchAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<WebSearchDenied>();
        harness.Resolver.Resolved.Count.ShouldBe(expectedResolutions);
        harness.Requests.Count.ShouldBe(expectedSends);
    }

    [Fact]
    public async Task SearchAsync_WhenAuthorityThrowsAfterGrantIssued_ReturnsDeniedWithoutResolutionOrSend()
    {
        var harness = NetworkWebSearchProviderHarness.Create(Json(ValidBody));
        var request = await harness.AuthorizedRequestAsync();
        harness.Authority.Fault = new InvalidOperationException("authority offline " + Needle);

        var result = await harness.Provider.SearchAsync(request, TestContext.Current.CancellationToken);

        var denied = result.ShouldBeOfType<WebSearchDenied>();
        denied.SafeMessage.ShouldNotContain("offline");
        denied.SafeMessage.ShouldNotContain(Needle);
        harness.Resolver.Resolved.ShouldBeEmpty();
        harness.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SearchAsync_WhenAuthoritySelectionUnavailable_ReturnsDeniedBeforeAnyEffect(bool throws)
    {
        var selector = new UnavailableAuthoritySelector { Fault = throws ? new InvalidOperationException("boom") : null };
        var harness = NetworkWebSearchProviderHarness.Create(Json(ValidBody), selector: selector);
        var request = await harness.AuthorizedRequestAsync();

        var result = await harness.Provider.SearchAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<WebSearchDenied>();
        harness.Grants.Enforcements.ShouldBeEmpty();
        harness.Resolver.Resolved.ShouldBeEmpty();
        harness.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_WhenRequiredAuditUnavailable_ReturnsDeniedBeforeResolution()
    {
        var harness = NetworkWebSearchProviderHarness.Create(Json(ValidBody));
        harness.Audit.Result = new SecurityAuditUnavailable("audit sink down");
        var request = await harness.AuthorizedRequestAsync();

        var result = await harness.Provider.SearchAsync(request, TestContext.Current.CancellationToken);

        var denied = result.ShouldBeOfType<WebSearchDenied>();
        denied.SafeMessage.ShouldBe("Required security audit failed.");
        harness.Resolver.Resolved.ShouldBeEmpty();
        harness.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_WhenGrantStoreThrows_ReturnsDeniedBeforeResolution()
    {
        var harness = NetworkWebSearchProviderHarness.Create(Json(ValidBody));
        var request = await harness.AuthorizedRequestAsync();
        harness.Grants.ConsumptionFault = new InvalidOperationException("store offline");

        var result = await harness.Provider.SearchAsync(request, TestContext.Current.CancellationToken);

        var denied = result.ShouldBeOfType<WebSearchDenied>();
        denied.SafeMessage.ShouldNotContain("offline");
        harness.Resolver.Resolved.ShouldBeEmpty();
        harness.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_WhenTransportDeniesSend_ReturnsDenied()
    {
        var harness = NetworkWebSearchProviderHarness.Create(
            grants => new CallbackNetworkTransport(
                grants,
                static (_, _) => ValueTask.FromResult<NetworkSendResult>(new NetworkDenied("Policy denied the destination."))));
        var request = await harness.AuthorizedRequestAsync();

        var result = await harness.Provider.SearchAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<WebSearchDenied>().SafeMessage.ShouldBe("Policy denied the destination.");
        _ = harness.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SearchAsync_WhenCancelledBeforeStart_ThrowsWithoutAuthorityOrNetworkCall()
    {
        var harness = NetworkWebSearchProviderHarness.Create(Json(ValidBody));
        var request = await harness.AuthorizedRequestAsync();
        var requestsBefore = harness.Authority.Requests.Count;
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var action = () => harness.Provider.SearchAsync(request, cancelled.Token);

        _ = await action.ShouldThrowAsync<OperationCanceledException>();
        harness.Authority.Requests.Count.ShouldBe(requestsBefore);
        harness.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_WhenCallerCancelsDuringSend_PropagatesCancellation()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var harness = NetworkWebSearchProviderHarness.Create(
            grants => new CallbackNetworkTransport(
                grants,
                async (_, token) =>
                {
                    entered.SetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    return new NetworkCancelled(sideEffectCertain: false);
                }));
        var request = await harness.AuthorizedRequestAsync();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var search = harness.Provider.SearchAsync(request, cancellation.Token);
        await entered.Task;
        await cancellation.CancelAsync();

        _ = await ((Func<Task>) (() => search)).ShouldThrowAsync<OperationCanceledException>();
        harness.Logger.Snapshot().Last().EventId.Id.ShouldBe(34412);
    }

    [Fact]
    public async Task SearchAsync_WhenNetworkReportsCancelled_PropagatesCallerCancellation()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var harness = NetworkWebSearchProviderHarness.Create(
            grants => new CallbackNetworkTransport(
                grants,
                async (_, _) =>
                {
                    await cancellation.CancelAsync();
                    return new NetworkCancelled(sideEffectCertain: true);
                }));
        var request = await harness.AuthorizedRequestAsync();

        var action = () => harness.Provider.SearchAsync(request, cancellation.Token);

        _ = await action.ShouldThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SearchAsync_WhenDeadlineElapsesDuringSend_ReturnsTypedTimeoutFailure()
    {
        var clock = new FakeTimeProvider(Epoch);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var harness = NetworkWebSearchProviderHarness.Create(
            grants => new CallbackNetworkTransport(
                grants,
                async (_, token) =>
                {
                    entered.SetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    return new NetworkCancelled(sideEffectCertain: false);
                }),
            clock);
        var request = await harness.AuthorizedRequestAsync();

        var search = harness.Provider.SearchAsync(request, TestContext.Current.CancellationToken);
        await entered.Task;
        clock.Advance(TimeSpan.FromMinutes(2));

        var result = await search;
        result.ShouldBeOfType<WebSearchFailed>().SafeMessage.ShouldContain("deadline");
    }

    [Fact]
    public async Task SearchAsync_WhenDeadlineAlreadyElapsed_ReturnsFailedWithoutAuthorityOrNetworkCall()
    {
        var clock = new FakeTimeProvider(Epoch.AddMinutes(5));
        var harness = NetworkWebSearchProviderHarness.Create(Json(ValidBody), clock);
        var request = await harness.AuthorizedRequestAsync();
        var requestsBefore = harness.Authority.Requests.Count;

        var result = await harness.Provider.SearchAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<WebSearchFailed>();
        harness.Authority.Requests.Count.ShouldBe(requestsBefore);
        harness.Grants.Enforcements.ShouldBeEmpty();
        harness.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_WhenEndpointRedirects_FailsWithoutFollowing()
    {
        var harness = NetworkWebSearchProviderHarness.Create(new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Found);
            response.Headers.Location = new Uri("https://elsewhere.example.test/query");
            return response;
        }));
        var request = await harness.AuthorizedRequestAsync();

        var result = await harness.Provider.SearchAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<WebSearchFailed>().SafeMessage.ShouldContain("redirect");
        _ = harness.Requests.ShouldHaveSingleItem();
        _ = harness.Resolver.Resolved.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task SearchAsync_WhenServiceReturnsErrorStatus_ReturnsFailed(HttpStatusCode status)
    {
        var harness = NetworkWebSearchProviderHarness.Create(new StubHttpMessageHandler(_ => new HttpResponseMessage(status)));
        var request = await harness.AuthorizedRequestAsync();

        var result = await harness.Provider.SearchAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<WebSearchFailed>().SafeMessage.ShouldBe("The search service returned an error.");
    }

    [Fact]
    public async Task SearchAsync_WhenDeclaredResponseExceedsLimit_ReturnsFailedBeforeReadingBody()
    {
        var harness = NetworkWebSearchProviderHarness.Create(
            Json(new string(' ', 2048) + ValidBody),
            configure: static options => options.MaximumResponseBytes = 512);
        var request = await harness.AuthorizedRequestAsync();

        var result = await harness.Provider.SearchAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<WebSearchFailed>().SafeMessage.ShouldContain("size limit");
    }

    [Fact]
    public async Task SearchAsync_WhenStreamedResponseOverrunsLimit_ReturnsFailedNotTruncatedSuccess()
    {
        var payload = Encoding.UTF8.GetBytes(new string(' ', 2048) + ValidBody);
        var harness = NetworkWebSearchProviderHarness.Create(
            grants => new CallbackNetworkTransport(
                grants,
                (_, _) => ValueTask.FromResult<NetworkSendResult>(
                    new NetworkResponseReceived(new StreamNetworkResponse(200, new ChunkedStream(payload, 100))))),
            configure: static options => options.MaximumResponseBytes = 512);
        var request = await harness.AuthorizedRequestAsync();

        var result = await harness.Provider.SearchAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<WebSearchFailed>().SafeMessage.ShouldContain("size limit");
    }

    [Theory]
    [MemberData(nameof(BodyFaults))]
    public async Task SearchAsync_WhenBodyStreamFaults_ReturnsTypedFailureAndDisposesResponse(
        Func<Exception> fault,
        string expectedFragment)
    {
        StreamNetworkResponse? response = null;
        var harness = NetworkWebSearchProviderHarness.Create(
            grants => new CallbackNetworkTransport(
                grants,
                (_, _) =>
                {
                    response = new StreamNetworkResponse(200, new FaultingReadStream(fault, Encoding.UTF8.GetBytes("{\"comp")));
                    return ValueTask.FromResult<NetworkSendResult>(new NetworkResponseReceived(response));
                }));
        var request = await harness.AuthorizedRequestAsync();

        var result = await harness.Provider.SearchAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<WebSearchFailed>().SafeMessage.ShouldContain(expectedFragment);
        response.ShouldNotBeNull().IsDisposed.ShouldBeTrue();
    }

    public static TheoryData<Func<Exception>, string> BodyFaults() => new()
    {
        { static () => new NetworkResponseTooLargeException(512, 513), "size limit" },
        { static () => new NetworkResponseTimedOutException(), "deadline" },
        { static () => new IOException("reset"), "could not be read" },
    };

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData(/*lang=json,strict*/ """{"results":[{"title":"","url":"https://a.example","snippet":"s"}]}""")]
    [InlineData(/*lang=json,strict*/ """{"results":[{"title":"t","url":"not a url","snippet":"s"}]}""")]
    public async Task SearchAsync_WhenBodyIsMalformed_ReturnsFailed(string body)
    {
        var harness = NetworkWebSearchProviderHarness.Create(Json(body));
        var request = await harness.AuthorizedRequestAsync();

        var result = await harness.Provider.SearchAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<WebSearchFailed>();
    }

    [Fact]
    public async Task SearchAsync_WhenResultCountExceedsMaximum_ReturnsIncompleteTruncatedItems()
    {
        const string body = /*lang=json,strict*/ """
            {"complete":true,"results":[
              {"title":"a","url":"https://a.example","snippet":"s"},
              {"title":"b","url":"https://b.example","snippet":"s"}]}
            """;
        var harness = NetworkWebSearchProviderHarness.Create(Json(body));
        var request = await harness.AuthorizedRequestAsync(maximumResults: 1);

        var result = await harness.Provider.SearchAsync(request, TestContext.Current.CancellationToken);

        var success = result.ShouldBeOfType<WebSearchSucceeded>();
        _ = success.Items.ShouldHaveSingleItem();
        success.Complete.ShouldBeFalse();
    }

    [Fact]
    public async Task SearchAsync_WhenAnswered_LeavesQueryAndEndpointOutOfEverySignal()
    {
        var harness = NetworkWebSearchProviderHarness.Create(Json(ValidBody));
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            static _ => true);
        using var metrics = new MetricCollector(AgentKitMetricNames.ProviderEgressCount);
        var request = await harness.AuthorizedRequestAsync(Needle);

        _ = await harness.Provider.SearchAsync(request, TestContext.Current.CancellationToken);

        var log = harness.Logger.Snapshot().ShouldHaveSingleItem();
        log.EventId.Id.ShouldBe(34413);
        log.Level.ShouldBe(LogLevel.Debug);
        SignalAssertions.ShouldNotContainContent(
            activities.Snapshot(),
            harness.Logger.Snapshot(),
            metrics.Snapshot(),
            Needle,
            "search.example.test",
            "docs.example.com");
    }

    [Fact]
    public async Task SearchAsync_WhenRefused_LogsStageOnlyAndLeavesQueryOutOfMessage()
    {
        var harness = NetworkWebSearchProviderHarness.Create(Json(ValidBody));
        var request = await harness.AuthorizedRequestAsync(Needle);
        harness.Authority.Fault = new InvalidOperationException("secret fault " + Needle);

        var result = await harness.Provider.SearchAsync(request, TestContext.Current.CancellationToken);

        var denied = result.ShouldBeOfType<WebSearchDenied>();
        denied.SafeMessage.ShouldNotContain(Needle);
        var log = harness.Logger.Snapshot().ShouldHaveSingleItem();
        log.EventId.Id.ShouldBe(34410);
        log.Level.ShouldBe(LogLevel.Information);
        SignalAssertions.ShouldNotContainContent([], harness.Logger.Snapshot(), [], Needle, "secret fault", "search.example.test");
    }

    [Fact]
    public async Task SearchAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var harness = NetworkWebSearchProviderHarness.Create(Json(ValidBody));

        var action = () => harness.Provider.SearchAsync(null!, TestContext.Current.CancellationToken);

        var exception = await action.ShouldThrowAsync<ArgumentNullException>();
        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public void Properties_WhenEndpointCarriesQuery_ExposeQueryFreeDestinationAndStableIdentity()
    {
        var harness = NetworkWebSearchProviderHarness.Create(
            Json(ValidBody),
            configure: static options =>
            {
                options.Endpoint = new Uri("https://search.example.test/query?tenant=a");
                options.ProviderId = new ProviderId("company-search");
            });

        harness.Provider.ProviderId.ShouldBe(new ProviderId("company-search"));
        harness.Provider.SecurityAudience.ShouldBe(new ComponentId("agentkit.tools.websearch.network"));
        harness.Provider.Destination.ShouldBe(
            new ProtectedResource(ProtectedResourceKind.NetworkEndpoint, "https://search.example.test/query"));
    }

    [Fact]
    public async Task SearchAsync_WhenEndpointAlreadyCarriesQuery_AppendsSearchParametersAfterIt()
    {
        var harness = NetworkWebSearchProviderHarness.Create(
            Json(ValidBody),
            configure: static options => options.Endpoint = new Uri("https://search.example.test/query?tenant=a"));
        var request = await harness.AuthorizedRequestAsync("two words");

        _ = await harness.Provider.SearchAsync(request, TestContext.Current.CancellationToken);

        harness.Requests.ShouldHaveSingleItem().Destination.Route.Value
            .ShouldBe("/query?tenant=a&q=two%20words&maximum_results=5&freshness=any");
    }

    [Fact]
    public void Constructor_WhenCollaboratorIsNull_ThrowsArgumentNullException()
    {
        var harness = NetworkWebSearchProviderHarness.Create(Json(ValidBody));
        var options = Options.Create(new NetworkWebSearchProviderOptions { Endpoint = new Uri("https://search.example.test/") });
        var clock = new FixedTimeProvider();
        var selector = new FixedSecurityAuthoritySelector(harness.Authority);
        var requestIds = new FixedSecurityRequestIdGenerator();
        var operationIds = new FixedIdentifierGenerator<NetworkOperationId>(default);
        var intentIds = new FixedIdentifierGenerator<SecurityEnforcementIntentId>(default);
        var auditIds = new FixedIdentifierGenerator<SecurityAuditRecordId>(default);

        _ = new NetworkWebSearchProvider(
            selector, harness.Grants, harness.Audit, harness.Resolver, harness.Transport, requestIds, operationIds, intentIds, auditIds, clock, options);
        Should.Throw<ArgumentNullException>(() => new NetworkWebSearchProvider(
            null!, harness.Grants, harness.Audit, harness.Resolver, harness.Transport, requestIds, operationIds, intentIds, auditIds, clock, options)).ParamName.ShouldBe("authoritySelector");
        Should.Throw<ArgumentNullException>(() => new NetworkWebSearchProvider(
            selector, null!, harness.Audit, harness.Resolver, harness.Transport, requestIds, operationIds, intentIds, auditIds, clock, options)).ParamName.ShouldBe("grantStore");
        Should.Throw<ArgumentNullException>(() => new NetworkWebSearchProvider(
            selector, harness.Grants, null!, harness.Resolver, harness.Transport, requestIds, operationIds, intentIds, auditIds, clock, options)).ParamName.ShouldBe("auditDispatcher");
        Should.Throw<ArgumentNullException>(() => new NetworkWebSearchProvider(
            selector, harness.Grants, harness.Audit, null!, harness.Transport, requestIds, operationIds, intentIds, auditIds, clock, options)).ParamName.ShouldBe("resolver");
        Should.Throw<ArgumentNullException>(() => new NetworkWebSearchProvider(
            selector, harness.Grants, harness.Audit, harness.Resolver, null!, requestIds, operationIds, intentIds, auditIds, clock, options)).ParamName.ShouldBe("transport");
        Should.Throw<ArgumentNullException>(() => new NetworkWebSearchProvider(
            selector, harness.Grants, harness.Audit, harness.Resolver, harness.Transport, null!, operationIds, intentIds, auditIds, clock, options)).ParamName.ShouldBe("securityRequestIds");
        Should.Throw<ArgumentNullException>(() => new NetworkWebSearchProvider(
            selector, harness.Grants, harness.Audit, harness.Resolver, harness.Transport, requestIds, null!, intentIds, auditIds, clock, options)).ParamName.ShouldBe("operationIds");
        Should.Throw<ArgumentNullException>(() => new NetworkWebSearchProvider(
            selector, harness.Grants, harness.Audit, harness.Resolver, harness.Transport, requestIds, operationIds, null!, auditIds, clock, options)).ParamName.ShouldBe("intentIds");
        Should.Throw<ArgumentNullException>(() => new NetworkWebSearchProvider(
            selector, harness.Grants, harness.Audit, harness.Resolver, harness.Transport, requestIds, operationIds, intentIds, null!, clock, options)).ParamName.ShouldBe("auditRecordIds");
        Should.Throw<ArgumentNullException>(() => new NetworkWebSearchProvider(
            selector, harness.Grants, harness.Audit, harness.Resolver, harness.Transport, requestIds, operationIds, intentIds, auditIds, null!, options)).ParamName.ShouldBe("timeProvider");
        Should.Throw<ArgumentNullException>(() => new NetworkWebSearchProvider(
            selector, harness.Grants, harness.Audit, harness.Resolver, harness.Transport, requestIds, operationIds, intentIds, auditIds, clock, null!)).ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenEndpointIsMissing_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => NetworkWebSearchProviderHarness.Create(
                Json(ValidBody),
                configure: static options => options.Endpoint = null))
            .ParamName.ShouldBe("options");
    }

    [Theory]
    [InlineData("http://search.example.test/query")]
    [InlineData("https://user:pass@search.example.test/query")]
    [InlineData("https://search.example.test/query#fragment")]
    public void Constructor_WhenEndpointIsInvalid_ThrowsArgumentException(string endpoint)
    {
        var exception = Should.Throw<ArgumentException>(() => NetworkWebSearchProviderHarness.Create(
            Json(ValidBody),
            configure: options => options.Endpoint = new Uri(endpoint)));
        exception.ParamName.ShouldBe("options");
        exception.Message.ShouldNotContain("pass");
    }

    [Fact]
    public void Constructor_WhenBoundIsInvalid_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => NetworkWebSearchProviderHarness.Create(
                Json(ValidBody),
                configure: static options => options.MaximumResponseBytes = 0))
            .ParamName.ShouldBe("options");
        Should.Throw<ArgumentOutOfRangeException>(() => NetworkWebSearchProviderHarness.Create(
                Json(ValidBody),
                configure: static options => options.ConnectTimeout = TimeSpan.Zero))
            .ParamName.ShouldBe("options");
        Should.Throw<ArgumentOutOfRangeException>(() => NetworkWebSearchProviderHarness.Create(
                Json(ValidBody),
                configure: static options => options.Classification = (NetworkDataClassification) 99))
            .ParamName.ShouldBe("options");
    }

    private static StubHttpMessageHandler Json(string body) => new(_ => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    });
}
