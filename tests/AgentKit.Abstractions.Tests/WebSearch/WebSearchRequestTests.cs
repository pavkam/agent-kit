// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.WebSearch;

using AgentKit.TestSupport;

/// <summary>Verifies WebSearchRequest behavior and contracts.</summary>
public sealed class WebSearchRequestTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var id = RequestId();
        var context = Context();
        var grant = Grant();
        var domains = Domains();
        var request = new WebSearchRequest(id, context, "query", domains, WebSearchFreshness.Week, 5, DateTimeOffset.UnixEpoch, grant);
        request.Id.ShouldBe(id);
        request.Context.ShouldBe(context);
        request.Query.ShouldBe("query");
        request.Domains.ShouldBe(domains);
        request.Freshness.ShouldBe(WebSearchFreshness.Week);
        request.MaximumResults.ShouldBe(5);
        request.Deadline.ShouldBe(DateTimeOffset.UnixEpoch);
        request.Grant.ShouldBe(grant);
    }

    [Fact]
    public void Constructor_WhenContextIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new WebSearchRequest(RequestId(), null!, "query", Domains(), WebSearchFreshness.Any, 5, DateTimeOffset.UnixEpoch, Grant()));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void Constructor_WhenQueryIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new WebSearchRequest(RequestId(), Context(), " ", Domains(), WebSearchFreshness.Any, 5, DateTimeOffset.UnixEpoch, Grant()));
        exception.ParamName.ShouldBe("query");
    }

    [Fact]
    public void Constructor_WhenDomainsIsDefault_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new WebSearchRequest(RequestId(), Context(), "query", default, WebSearchFreshness.Any, 5, DateTimeOffset.UnixEpoch, Grant()));
        exception.ParamName.ShouldBe("domains");
    }

    [Fact]
    public void Constructor_WhenFreshnessIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new WebSearchRequest(RequestId(), Context(), "query", Domains(), (WebSearchFreshness) 999, 5, DateTimeOffset.UnixEpoch, Grant()));
        exception.ParamName.ShouldBe("freshness");
    }

    [Fact]
    public void Constructor_WhenMaximumResultsIsNotPositive_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new WebSearchRequest(RequestId(), Context(), "query", Domains(), WebSearchFreshness.Any, 0, DateTimeOffset.UnixEpoch, Grant()));
        exception.ParamName.ShouldBe("maximumResults");
    }

    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new WebSearchRequest(RequestId(), Context(), "query", Domains(), WebSearchFreshness.Any, 5, DateTimeOffset.UnixEpoch, null!));
        exception.ParamName.ShouldBe("grant");
    }

    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var first = new WebSearchRequest(RequestId(), Context(), "query", Domains(), WebSearchFreshness.Week, 5, DateTimeOffset.UnixEpoch, Grant());
        var second = new WebSearchRequest(RequestId(), Context(), "query", Domains(), WebSearchFreshness.Week, 5, DateTimeOffset.UnixEpoch, Grant());
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_WhenDomainsDiffer_IsNotEqual()
    {
        var first = new WebSearchRequest(RequestId(), Context(), "query", Domains(), WebSearchFreshness.Week, 5, DateTimeOffset.UnixEpoch, Grant());
        var second = new WebSearchRequest(RequestId(), Context(), "query", [], WebSearchFreshness.Week, 5, DateTimeOffset.UnixEpoch, Grant());
        first.ShouldNotBe(second);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new WebSearchRequest(RequestId(), Context(), "query", Domains(), WebSearchFreshness.Week, 5, DateTimeOffset.UnixEpoch, Grant());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static WebSearchRequestId RequestId() => new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    private static AgentId AgentId() => new(Guid.Parse("20000000-0000-0000-0000-000000000002"));
    private static SessionId SessionId() => new(Guid.Parse("30000000-0000-0000-0000-000000000003"));
    private static OperationId OperationId() => new(Guid.Parse("40000000-0000-0000-0000-000000000004"));
    private static RunId RunId() => new(Guid.Parse("50000000-0000-0000-0000-000000000005"));
    private static InRunOperationCorrelation Correlation() => new(OperationId(), RunId(), null);
    private static ToolCallId ToolCallId() => new(Guid.Parse("60000000-0000-0000-0000-000000000006"));
    private static ExecutionIdentity Identity() => TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
    private static ToolExecutionContext Context() => TestSecurityEvidence.ToolContext(AgentId(), SessionId(), ToolCallId(), Correlation(), Identity());
    private static ImmutableArray<NormalizedHost> Domains() => [new("example.com")];
    private static SecurityGrant Grant() => new(
        new GrantId(Guid.Parse("70000000-0000-0000-0000-000000000007")),
        new SecurityRequestId(Guid.Parse("80000000-0000-0000-0000-000000000008")),
        new SecurityAuthorizationScope(AgentId(), SessionId(), Correlation()),
        Identity(),
        new ComponentId("web-search"),
        SecurityOperationKind.Network,
        SecurityEffect.Egress,
        [new ProtectedResource(ProtectedResourceKind.NetworkEndpoint, "https://search.example/")],
        new InputFingerprint("sha256:test"),
        new SecurityPolicyVersion(1),
        new SecurityRevocationVersion(1),
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch.AddMinutes(1),
        1);
}
