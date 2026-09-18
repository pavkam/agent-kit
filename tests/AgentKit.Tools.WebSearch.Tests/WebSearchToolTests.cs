// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.WebSearch.Tests;



/// <summary>Verifies WebSearchTool behavior and contracts.</summary>
public sealed class WebSearchToolTests
{
    [Theory]
    [InlineData( /*lang=json,strict*/"{}")]
    [InlineData( /*lang=json,strict*/"{\"query\":\"\"}")]
    [InlineData( /*lang=json,strict*/"{\"query\":\"q\",\"domains\":[\"bad host\"]}")]
    [InlineData( /*lang=json,strict*/"{\"query\":\"q\",\"domains\":[\"example.com\",\"example.com\"]}")]
    [InlineData( /*lang=json,strict*/"{\"query\":\"q\",\"maximum_results\":0}")]
    [InlineData( /*lang=json,strict*/"{\"query\":\"q\",\"freshness\":\"century\"}")]
    [InlineData( /*lang=json,strict*/"{\"query\":\"q\",\"extra\":true}")]
    [InlineData( /*lang=json,strict*/"{\"query\":\"q\",\"domains\":\"not-an-array\"}")]
    [InlineData( /*lang=json,strict*/"{\"query\":\"q\",\"domains\":[\"a.com\",\"b.com\",\"c.com\",\"d.com\",\"e.com\",\"f.com\",\"g.com\",\"h.com\",\"i.com\",\"j.com\",\"k.com\"]}")]
    [InlineData( /*lang=json,strict*/"{\"query\":\"q\",\"freshness\":1}")]
    [InlineData( /*lang=json,strict*/"{\"query\":\"q\",\"timeout_seconds\":0}")]
    [InlineData( /*lang=json,strict*/"{\"query\":\"q\",\"timeout_seconds\":-1}")]
    [InlineData( /*lang=json,strict*/"{\"query\":\"q\",\"timeout_seconds\":999999}")]
    [InlineData( /*lang=json,strict*/"{\"query\":\"q\",\"timeout_seconds\":\"soon\"}")]
    public async Task InvokeAsync_WhenArgumentsInvalid_PerformsNoIdentityAllocationAuthorizationOrSearch(string json)
    {
        var provider = new EnforcingSearchProvider();
        var authority = new RecordingSecurityAuthority();
        var ids = new FixedSearchRequestIdGenerator();
        var result = await Tool(provider, authority, ids).InvokeAsync(Request(json), TestContext.Current.CancellationToken);
        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        ids.Calls.ShouldBe(0);
        authority.Requests.ShouldBeEmpty();
        provider.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthorized_BindsAndReenforcesExactClassifiedEgress()
    {
        var provider = new EnforcingSearchProvider();
        var authority = new RecordingSecurityAuthority();
        const string arguments = /*lang=json,strict*/ """
            {"query":"agent frameworks","domains":["Docs.Example.com."],"freshness":"week","maximum_results":3,"timeout_seconds":45}
            """;
        var result = await Tool(provider, authority).InvokeAsync(Request(arguments), TestContext.Current.CancellationToken);
        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        var security = authority.Requests.ShouldHaveSingleItem();
        security.Audience.ShouldBe(provider.SecurityAudience);
        security.Kind.ShouldBe(SecurityOperationKind.Network);
        security.Effect.ShouldBe(SecurityEffect.Egress);
        security.Resources.ShouldBe([provider.Destination]);
        var request = provider.Requests.ShouldHaveSingleItem();
        request.Domains.ShouldBe([new NormalizedHost("docs.example.com")]);
        request.Freshness.ShouldBe(WebSearchFreshness.Week);
        request.MaximumResults.ShouldBe(3);
        request.Deadline.ShouldBe(DateTimeOffset.UnixEpoch.AddSeconds(45));
        request.Grant.InputFingerprint.ShouldBe(security.InputFingerprint);
        provider.GrantMatched.ShouldBeTrue();
    }

    [Theory]
    [InlineData("any", WebSearchFreshness.Any)]
    [InlineData("day", WebSearchFreshness.Day)]
    [InlineData("month", WebSearchFreshness.Month)]
    [InlineData("year", WebSearchFreshness.Year)]
    public async Task InvokeAsync_WhenFreshnessSpecified_ForwardsExactFreshnessToProvider(string freshness, WebSearchFreshness expected)
    {
        var provider = new EnforcingSearchProvider();
        var json = JsonSerializer.Serialize(new { query = "q", freshness });

        var result = await Tool(provider, new RecordingSecurityAuthority()).InvokeAsync(Request(json), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        provider.Requests.ShouldHaveSingleItem().Freshness.ShouldBe(expected);
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthorityDenies_ReturnsRejectedWithoutProviderCall()
    {
        var provider = new EnforcingSearchProvider();
        var result = await Tool(provider, new RecordingSecurityAuthority(false)).InvokeAsync(Request( /*lang=json,strict*/"{\"query\":\"agent frameworks\"}"), TestContext.Current.CancellationToken);
        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        provider.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenSuccessful_ProjectsUntrustedProviderIdentityAndResults()
    {
        var provider = new EnforcingSearchProvider();
        var result = await Tool(provider, new RecordingSecurityAuthority()).InvokeAsync(Request( /*lang=json,strict*/"{\"query\":\"agent frameworks\"}"), TestContext.Current.CancellationToken);
        using var json = Json(result);
        json.RootElement.GetProperty("provider").GetString().ShouldBe(provider.ProviderId.Value);
        json.RootElement.GetProperty("instruction_authority").GetBoolean().ShouldBeFalse();
        json.RootElement.GetProperty("complete").GetBoolean().ShouldBeTrue();
        json.RootElement.GetProperty("results")[0].GetProperty("url").GetString().ShouldBe("https://docs.example.com/agentkit");
    }

    [Fact]
    public async Task InvokeAsync_WhenProviderReturnsOutsideDomainFilter_FailsClosed()
    {
        var provider = new EnforcingSearchProvider();
        var result = await Tool(provider, new RecordingSecurityAuthority()).InvokeAsync(Request( /*lang=json,strict*/"{\"query\":\"q\",\"domains\":[\"allowed.example\"]}"), TestContext.Current.CancellationToken);
        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.FailureReason!.ShouldContain("outside");
        result.Content.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenProviderReturnsSubdomainOfFilter_AcceptsIt()
    {
        var provider = new EnforcingSearchProvider
        {
            Result = static request => new WebSearchSucceeded(request.Id, [new WebSearchItem("Title", new Uri("https://sub.example.com/page"), "Snippet.", null)], true),
        };
        var result = await Tool(provider, new RecordingSecurityAuthority()).InvokeAsync(Request( /*lang=json,strict*/"{\"query\":\"q\",\"domains\":[\"example.com\"]}"), TestContext.Current.CancellationToken);
        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
    }

    [Fact]
    public async Task InvokeAsync_WhenProviderExceedsProjectionBounds_TruncatesExplicitlyAndClearsCompleteness()
    {
        var provider = new EnforcingSearchProvider
        {
            Result = static request => new WebSearchSucceeded(request.Id, [new WebSearchItem("Long title", new Uri("https://example.com/1"), "Long snippet", null), new WebSearchItem("Second", new Uri("https://example.com/2"), "Second", null),], true),
        };
        var options = new WebSearchToolOptions
        {
            DefaultMaximumResults = 1,
            MaximumResults = 1,
            MaximumTitleCharacters = 4,
            MaximumSnippetCharacters = 4,
        };
        var result = await Tool(provider, new RecordingSecurityAuthority(), options: options).InvokeAsync(Request( /*lang=json,strict*/"{\"query\":\"q\"}"), TestContext.Current.CancellationToken);
        using var json = Json(result);
        json.RootElement.GetProperty("truncated").GetBoolean().ShouldBeTrue();
        json.RootElement.GetProperty("complete").GetBoolean().ShouldBeFalse();
        json.RootElement.GetProperty("results").GetArrayLength().ShouldBe(1);
        json.RootElement.GetProperty("results")[0].GetProperty("title").GetString().ShouldBe("Long");
    }

    [Fact]
    public async Task InvokeAsync_WhenTruncationBoundaryLandsInsideASurrogatePair_BacksOffInsteadOfEmittingALoneSurrogate()
    {
        // Truncate sliced on UTF-16 code units. "AB\U0001F600" is ['A','B',HighSurrogate,LowSurrogate] (4 code
        // units); a maximum of 3 lands exactly between the high and low surrogate. Cutting there must back off
        // to 2 instead of emitting a lone high surrogate that JsonSerializer would render as replacement or
        // escaped garbage.
        var provider = new EnforcingSearchProvider
        {
            Result = static request => new WebSearchSucceeded(
                request.Id, [new WebSearchItem("AB\U0001F600", new Uri("https://example.com/1"), "s", null)], true),
        };
        var options = new WebSearchToolOptions
        {
            DefaultMaximumResults = 1,
            MaximumResults = 1,
            MaximumTitleCharacters = 3,
            MaximumSnippetCharacters = 3,
        };

        var result = await Tool(provider, new RecordingSecurityAuthority(), options: options).InvokeAsync(
            Request( /*lang=json,strict*/"{\"query\":\"q\"}"), TestContext.Current.CancellationToken);

        using var json = Json(result);
        json.RootElement.GetProperty("results")[0].GetProperty("title").GetString().ShouldBe("AB");
    }

    [Fact]
    public async Task InvokeAsync_WhenProviderReturnsDifferentRequest_FailsClosed()
    {
        var provider = new EnforcingSearchProvider
        {
            Result = static _ => new WebSearchSucceeded(new WebSearchRequestId(Guid.Parse("90000000-0000-0000-0000-000000000009")), [], true),
        };
        var result = await Tool(provider, new RecordingSecurityAuthority()).InvokeAsync(Request( /*lang=json,strict*/"{\"query\":\"q\"}"), TestContext.Current.CancellationToken);
        result.Outcome.FailureReason!.ShouldContain("different request");
    }

    [Theory]
    [InlineData("denied")]
    [InlineData("unavailable")]
    [InlineData("failed")]
    public async Task InvokeAsync_WhenProviderDoesNotSucceed_PreservesTypedFailure(string kind)
    {
        var provider = new EnforcingSearchProvider
        {
            Result = request => kind switch
            {
                "denied" => new WebSearchDenied(request.Id, "Provider denied."),
                "unavailable" => new WebSearchUnavailable(request.Id, "Provider unavailable."),
                _ => new WebSearchFailed(request.Id, "Provider failed."),
            },
        };
        var result = await Tool(provider, new RecordingSecurityAuthority()).InvokeAsync(Request( /*lang=json,strict*/"{\"query\":\"q\"}"), TestContext.Current.CancellationToken);
        result.Outcome.Kind.ShouldBe(kind == "denied" ? ToolCallOutcomeKind.Rejected : ToolCallOutcomeKind.Failed);
        result.Content.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenDestinationIsNotNetworkEndpoint_RejectsComposition()
    {
        var provider = new EnforcingSearchProvider();
        var invalid = new InvalidDestinationProvider(provider);
        var action = () => Tool(invalid, new RecordingSecurityAuthority());
        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe("provider.Destination");
    }

    private static WebSearchTool Tool(IWebSearchProvider provider, ISecurityAuthority authority, FixedSearchRequestIdGenerator? searchIds = null, WebSearchToolOptions? options = null) => new(provider, authority, new FixedSecurityRequestIdGenerator(), searchIds ?? new FixedSearchRequestIdGenerator(), new FixedTimeProvider(), Options.Create(options ?? new WebSearchToolOptions()));
    private static ToolInvocationRequest Request(string json) => new(TestData.Context, JsonDocument.Parse(json).RootElement, DateTimeOffset.UnixEpoch);
    private static JsonDocument Json(ToolInvocationResult result) => JsonDocument.Parse(result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);
}
