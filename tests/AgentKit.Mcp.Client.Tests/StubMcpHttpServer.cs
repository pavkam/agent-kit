// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client.Tests;

using System.Net;
using System.Net.Http;
using System.Text;

/// <summary>A scripted Streamable HTTP MCP server with no tools, answering each exchange in-process.</summary>
internal sealed class StubMcpHttpServer: HttpMessageHandler
{
    internal List<string> Exchanges { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Get)
        {
            Exchanges.Add("GET");
            return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
        }

        if (request.Method == HttpMethod.Delete)
        {
            Exchanges.Add("DELETE");
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        var text = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        var method = root.GetProperty("method").GetString()!;
        Exchanges.Add(method);
        if (!root.TryGetProperty("id", out var id))
        {
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }

        if (method is not ("initialize" or "tools/list"))
        {
            var error = $$$"""{"jsonrpc":"2.0","id":{{{id.GetRawText()}}},"error":{"code":-32601,"message":"Method not found"}}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(error, Encoding.UTF8, "application/json"),
            };
        }

        var result = method switch
        {
            "initialize" => JsonSerializer.Serialize(new
            {
                protocolVersion = root.GetProperty("params").GetProperty("protocolVersion").GetString(),
                capabilities = new { tools = new { } },
                serverInfo = new { name = "stub", version = "1" },
            }),
            _ => /*lang=json,strict*/ """{"tools":[]}""",
        };
        var body = $$"""{"jsonrpc":"2.0","id":{{id.GetRawText()}},"result":{{result}}}""";
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        _ = response.Headers.TryAddWithoutValidation("Mcp-Session-Id", "stub-session");
        return response;
    }
}
