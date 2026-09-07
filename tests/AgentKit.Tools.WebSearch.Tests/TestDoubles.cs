// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.WebSearch.Tests;

internal sealed class EnforcingSearchProvider: IWebSearchProvider
{
    internal List<WebSearchRequest> Requests { get; } = [];
    internal Func<WebSearchRequest, WebSearchProviderResult> Result { get; set; } = static request =>
        new WebSearchSucceeded(
            request.Id,
            [new WebSearchItem("AgentKit", new Uri("https://docs.example.com/agentkit"), "Documentation.", null)],
            true);
    internal bool GrantMatched { get; private set; }

    public ProviderId ProviderId { get; } = new("scripted-search");
    public ComponentId SecurityAudience { get; } = new("test.web-search.provider");
    public ProtectedResource Destination { get; } = new(
        ProtectedResourceKind.NetworkEndpoint,
        "https://search.example.com/");

    public Task<WebSearchProviderResult> SearchAsync(
        WebSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        GrantMatched = request.Grant.Audience == SecurityAudience
            && request.Grant.Kind == SecurityOperationKind.Network
            && request.Grant.Effect == SecurityEffect.Egress
            && request.Grant.Resources.SequenceEqual([Destination])
            && request.Grant.InputFingerprint == WebSearchSecurityBinding.Fingerprint(
                request.Id,
                ProviderId,
                Destination,
                request.Query,
                request.Domains,
                request.Freshness,
                request.MaximumResults,
                request.Deadline);
        return Task.FromResult(GrantMatched
            ? Result(request)
            : new WebSearchDenied(request.Id, "Grant mismatch."));
    }
}

internal sealed class RecordingSecurityAuthority(bool allow = true): ISecurityAuthority
{
    internal List<SecurityRequest> Requests { get; } = [];

    public ValueTask<SecurityDecision> AuthorizeAsync(SecurityRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return !allow
            ? ValueTask.FromResult<SecurityDecision>(new SecurityDenied(
                request.Id,
                new SecurityPolicyVersion(1),
                new SecurityDenial("test.denied", "Denied.")))
            : ValueTask.FromResult<SecurityDecision>(new SecurityAllowed(
                request.Id,
                new SecurityPolicyVersion(1),
                new SecurityGrant(
                    new GrantId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
                    request.Id,
                    request.Scope,
                    request.Identity,
                    request.Audience,
                    request.Kind,
                    request.Effect,
                    request.Resources,
                    request.InputFingerprint,
                    new SecurityPolicyVersion(1),
                    new SecurityRevocationVersion(1),
                    DateTimeOffset.UnixEpoch,
                    request.Deadline,
                    1)));
    }
}

internal sealed class FixedSecurityRequestIdGenerator: IIdentifierGenerator<SecurityRequestId>
{
    public SecurityRequestId Create() => new(Guid.Parse("20000000-0000-0000-0000-000000000002"));
}

internal sealed class FixedSearchRequestIdGenerator: IIdentifierGenerator<WebSearchRequestId>
{
    internal int Calls { get; private set; }

    public WebSearchRequestId Create()
    {
        Calls++;
        return TestData.SearchId;
    }
}

internal sealed class FixedTimeProvider: TimeProvider
{
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
}

internal static class TestData
{
    internal static WebSearchRequestId SearchId { get; } = new(Guid.Parse("30000000-0000-0000-0000-000000000003"));
    internal static ToolExecutionContext Context { get; } = new(
        new AgentId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
        new SessionId(Guid.Parse("50000000-0000-0000-0000-000000000005")),
        new ToolCallId(Guid.Parse("60000000-0000-0000-0000-000000000006")),
        new InRunOperationCorrelation(
            new OperationId(Guid.Parse("70000000-0000-0000-0000-000000000007")),
            new RunId(Guid.Parse("80000000-0000-0000-0000-000000000008")),
            null),
        AgentKit.TestSupport.TestExecutionIdentity.Create(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Human));
}

internal sealed class InvalidDestinationProvider(IWebSearchProvider inner): IWebSearchProvider
{
    public ProviderId ProviderId => inner.ProviderId;
    public ComponentId SecurityAudience => inner.SecurityAudience;
    public ProtectedResource Destination { get; } = new(ProtectedResourceKind.ApplicationState, "invalid");
    public Task<WebSearchProviderResult> SearchAsync(WebSearchRequest request, CancellationToken cancellationToken = default) =>
        inner.SearchAsync(request, cancellationToken);
}
