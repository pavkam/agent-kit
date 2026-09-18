# Web research assistant

An analyst wants an agent that can answer "what changed in the last three
releases of library X" by searching the web, fetching the pages it finds, and
writing a sourced summary. The security team's condition is that the agent can
only reach hosts on an allow-list, over HTTPS, never to a private address, and
that every fetch is bounded in size and time. Search goes through the company's
existing search API, not through the model provider.

## What the agent needs

| Need                       | AgentKit part                                                                                                |
| -------------------------- | ------------------------------------------------------------------------------------------------------------ |
| Fetch a page               | `AddWebFetchTool` (`web_fetch`) over `AddAgentNetwork`                                                       |
| Only approved destinations | `NetworkDestinationPolicy` on `AgentNetworkOptions`, plus a security policy over `NetworkEndpoint` resources |
| Search                     | `AddWebSearchTool` (`web_search`) with your `IWebSearchProvider`                                             |
| Notes the analyst can keep | `UseWorkspace(notesRoot)` for `write_file` into a scratch directory                                          |

## Compose the engine

```csharp
static AgentEngine CreateResearcher(string notesRoot, string apiKey, ISearchApi searchApi)
{
    var builder = AgentEngine.CreateBuilder()
        .UseLocalDevelopmentDefaults()
        .UseOpenAI(apiKey, "gpt-4o-mini")
        .UseWorkspace(notesRoot)
        .WithInstructions(
            "You research technical questions. Search first, fetch only the most " +
            "relevant results, and write a summary to notes/<topic>.md with a " +
            "'Sources' section listing every URL you used.")
        .WithMaxTurns(30);

    // Outbound network with a closed destination list.
    builder.Services.AddAgentNetwork(o =>
    {
        o.DestinationPolicy = new NetworkDestinationPolicy(
            allowedSchemes: ["https"],
            allowedHosts:
            [
                new NormalizedHost("github.com"),
                new NormalizedHost("docs.example-library.org"),
                new NormalizedHost("pypi.org"),
            ],
            allowPrivateAddresses: false);
    });
    builder.Services.AddWebFetchTool(o =>
    {
        o.DefaultMaximumCharacters = 30_000;
        o.MaximumResponseBytes = 1024 * 1024;
        o.DefaultTimeout = TimeSpan.FromSeconds(15);
    });

    // Search through the organization's own API.
    builder.Services.AddSingleton<IWebSearchProvider>(new CompanySearchProvider(searchApi));
    builder.Services.AddWebSearchTool(o => o.DefaultMaximumResults = 5);

    // Second line of defence: a policy that sees the resolved endpoint.
    builder.Services.AddSingleton<ISecurityPolicy, ApprovedHostsPolicy>();

    return builder.Build();
}
```

`NetworkDestinationPolicy` is a structural check inside the transport: scheme,
host, and the resolved IP addresses (private, loopback, link-local, and
NAT-mapped ranges are refused unless you allow them). The security policy is a
separate, earlier decision over the `ProtectedResource` of kind
`NetworkEndpoint`, which is where you would add per-user or per-tenant rules:

```csharp
sealed class ApprovedHostsPolicy : ISecurityPolicy
{
    static readonly HashSet<string> Hosts = ["github.com", "docs.example-library.org", "pypi.org"];

    public ValueTask<SecurityPolicyResult> EvaluateAsync(SecurityRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Kind != SecurityOperationKind.Network)
        {
            return ValueTask.FromResult(new SecurityPolicyResult(SecurityPolicyResultKind.Abstain, null, null));
        }

        // The first resource is the destination ("https://host:443" or the full route);
        // the rest are the resolved "address:port" pairs the transport may connect to.
        var destination = request.Resources[0].Identifier;
        var approved = Uri.TryCreate(destination, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && Hosts.Contains(uri.Host);

        return ValueTask.FromResult(approved
            ? new SecurityPolicyResult(SecurityPolicyResultKind.Allow, "research.approved-host", "Destination is on the research allow-list.")
            : new SecurityPolicyResult(SecurityPolicyResultKind.Deny, "research.unknown-host", "Destination is not on the research allow-list."));
    }
}
```

The search provider adapts your API to the contract the tool expects. It
declares its own provider identity, the audience it authorizes against, and the
single destination it talks to, so the same egress rules apply to it:

```csharp
sealed class CompanySearchProvider(ISearchApi api) : IWebSearchProvider
{
    public ProviderId ProviderId { get; } = new("acme-search");
    public ComponentId SecurityAudience { get; } = new("acme.search");
    public ProtectedResource Destination { get; } = new(ProtectedResourceKind.NetworkEndpoint, "https://search.acme.internal");

    public async Task<WebSearchProviderResult> SearchAsync(WebSearchRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var hits = await api.QueryAsync(request.Query, request.MaximumResults, cancellationToken);
            return new WebSearchSucceeded(
                request.Id,
                [.. hits.Select(h => new WebSearchItem(h.Title, new Uri(h.Url), h.Snippet, h.PublishedAt))],
                complete: true);
        }
        catch (HttpRequestException)
        {
            return new WebSearchFailed(request.Id, "The search service did not answer.");
        }
    }
}
```

The request carries the `SecurityGrant` the tool already obtained for
`Destination`, the deadline, and any domain filter the model asked for. The
result is a typed outcome (`WebSearchSucceeded`, `WebSearchFailed`,
`WebSearchDenied`, `WebSearchUnavailable`) and the safe message is what the
model sees; do not put the upstream exception text in it.

## Use it

```csharp
await using var engine = CreateResearcher("/data/research", apiKey, searchApi);

var result = await engine.SendAsync(
    "What changed between example-library 3.1 and 3.4 that would affect our upgrade?",
    cancellationToken);

foreach (var fetch in result.Events.OfType<ConversationToolCallEvent>().Where(e => e.ToolName == "web_fetch"))
{
    Console.WriteLine($"fetched: {fetch.ArgumentsJson}");
}
```

The `Sources` section in the written note and the `web_fetch` calls in the
events give you two independent records of what the answer was built from.

## What the framework guarantees

- **Every request is authorized and then enforced.** The tool asks the security
  authority for the resolved endpoint; the transport validates the grant again
  and applies the destination policy and address checks before connecting.
  Redirects are re-checked per hop and capped by `MaximumRedirects`.
- **Responses are bounded.** Body bytes, extracted characters, header count and
  size, and timeouts are enforced by the transport and the tool; an oversized
  page is reported as a limit, not silently truncated.
- **Fetched content is data.** Page text enters the conversation as a tool
  result, never as an instruction with system authority, and a page cannot grant
  the model anything.
- **Search results carry provenance.** Each result keeps its URL, so the note's
  sources can be verified.

## Status

`AgentKit.Tools.WebSearch` ships the tool and the contract but no first-party
search provider; you supply one as above. The `web_fetch` path (network package,
destination policy, and tool) is complete.

## What lives where

| Concern                        | Package                                                                  |
| ------------------------------ | ------------------------------------------------------------------------ |
| Name resolution and transport  | [AgentKit.Network](../../src/AgentKit.Network/README.md)                 |
| `web_fetch` tool               | [AgentKit.Tools.Web](../../src/AgentKit.Tools.Web/README.md)             |
| `web_search` tool and contract | [AgentKit.Tools.WebSearch](../../src/AgentKit.Tools.WebSearch/README.md) |
| Normative egress rules         | [Network access and egress](../concepts/network-access-and-egress.md)    |

Next: [Background ticket-triage worker](ticket-triage-worker.md) ·
[Permissions and approvals](../guides/permissions.md)
